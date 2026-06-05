extends CompositorEffect

## Color of outlines
@export var outline_color := Color.GOLD

## Thickness of outline in pixels
@export_range(0, 181) var thickness : int= 4 :
	set(value):
		value = clampi(value, 0, 181)
		thickness = value
		_passes = 0
		while value > 0:
			value = value >> 1
			_passes += 1

## Stencil value that denotes pixels to be outlined
@export var stencil_value := 1

## Stencil mask to use when checking the stencil value
@export var stencil_mask := 1

## Enable hot-reload of shaders; only set this if you're actively editing the
## shaders.
var _hot_reload := false

## Number of jump-flood _passes to run to make the outline; automatically set
## by the thickness setter.
var _passes := 1

## GLSL shader definitions for each of our shaders
var _shader_dir = get_script().get_path().get_base_dir() + "/"
var jf_shader_file = _shader_dir + "jump_flood.glsl"
var sc_shader_file = _shader_dir + "stencil_copy.glsl"
var do_shader_file = _shader_dir + "draw_outline.glsl"

var rd: RenderingDevice

## Shared shader modules and compute pipelines (not tied to a viewport size).
var sc_shader: RID
var sc_uniform_set: RID
var do_shader: RID
var do_pipeline: RID
var jf_shader: RID
var jf_pipeline: RID
var _global_pipelines_valid := false

## Vertex array for the stencil copy pipeline
var scdo_vertex_format : int
var scdo_vertex_buffer : RID
var scdo_vertex_array : RID

## Uniform buffer with render resolution for the stencil-copy pass
var scdo_uniform_buffer : RID

## Per SubViewport / render-scene-buffers GPU state (avoids rebuild ping-pong
## when multiple viewports render each frame).
var _buffer_states: Dictionary = {}

## Exposed Texture2Ds to allow debugging of the various textures used in this
## CompositorEffect (updated to the viewport currently being rendered).
var debug_textures := [Texture2DRD.new(), Texture2DRD.new(), Texture2DRD.new()]

## mutex for rebuild_pipelines
var mutex := Mutex.new()
## Set when the shader is dirty and needs to be rebuilt
@export var rebuild_pipelines := true :
	set(value):
		mutex.lock()
		rebuild_pipelines = value
		mutex.unlock()

## Tracks the highest modification time for any of the shaders to trigger a
## reload
var _shader_mtime := 0


class PerBufferState:
	var color_texture := RID()
	var depth_texture := RID()
	var resolution := Vector2i(1, 1)
	var textures := [RID(), RID(), RID()]
	var sc_framebuffer := RID()
	var sc_pipeline := RID()
	var jf_uniform_sets := [RID(), RID()]

	func free_gpu(device: RenderingDevice) -> void:
		if sc_pipeline.is_valid():
			device.free_rid(sc_pipeline)
			sc_pipeline = RID()
		if sc_framebuffer.is_valid():
			if device.framebuffer_is_valid(sc_framebuffer):
				device.free_rid(sc_framebuffer)
			sc_framebuffer = RID()
		for i in range(textures.size()):
			if textures[i].is_valid():
				device.free_rid(textures[i])
				textures[i] = RID()
		jf_uniform_sets = [RID(), RID()]


## Check if any of the shaders have been updated, and if so, kick off a rebuild
## of the pipelines
func check_for_shader_changes() -> void:
	var rebuild = false
	for path in [sc_shader_file, jf_shader_file, do_shader_file]:
		var mtime = FileAccess.get_modified_time(path)
		if mtime > _shader_mtime:
			rebuild = true
			_shader_mtime = mtime

	if rebuild:
		rebuild_pipelines = true

# Called when this resource is constructed.
func _init():
	# Run after the transparent pass so the outline composites on top of
	# transparent overlays like the editor grid. Compositing at POST_OPAQUE
	# would let the transparent pass alpha-blend the grid over the outline,
	# making the outline look translucent where grid lines cross it.
	# The depth/stencil buffer is preserved through the transparent pass
	# (nothing in that pass writes stencil), so stencil-copy still finds
	# the selected mesh's silhouette correctly.
	#
	# If you add other transparent overlays (gizmo billboards, ghost previews,
	# selection widgets, etc.) that must stay *on top of* the outline, you will
	# need a different compositor ordering—e.g. a second effect after those
	# passes, or drawing those overlays in a later callback. POST_TRANSPARENT is
	# correct while the editor grid is the only relevant transparent layer.
	effect_callback_type = CompositorEffect.EFFECT_CALLBACK_TYPE_POST_TRANSPARENT

	# GDScript does NOT run a property's setter for its declaration-default
	# assignment, so changing the literal default of `thickness` above would
	# leave `_passes` stuck at its own declared default (1) and the outline
	# would only ever be ~1px regardless of the new value. Reassigning here
	# forces the setter to run and keeps `_passes` in sync. Scene-instance
	# overrides go through the setter on load already, so this only matters
	# for the script default.
	thickness = thickness

	# Grab the rendering device
	rd = RenderingServer.get_rendering_device()

	## We create the vertex & index arrays to draw a full-screen quad.

	# build the vertex format
	var vertex_attr = RDVertexAttribute.new()
	vertex_attr.location = 0
	vertex_attr.format = RenderingDevice.DATA_FORMAT_R32G32B32_SFLOAT
	vertex_attr.stride = 4 * 3
	scdo_vertex_format = rd.vertex_format_create([vertex_attr])

	# These vertex points make a triangle that cover the entire screen.  The
	# points are declared in counter-clockwise winding order so that the front
	# of the quad is facing the camera.  This is important for the stencil
	# operations set in _build_sc_pipeline(), as we only set stencil front_ops.
	var vertex_data = PackedVector3Array([
		Vector3(-1, -1, 0),
		Vector3(3, -1, 0),
		Vector3(-1, 3, 0),
	])
	var vertex_bytes = vertex_data.to_byte_array()
	scdo_vertex_buffer = rd.vertex_buffer_create(vertex_bytes.size(), vertex_bytes)
	scdo_vertex_array = rd.vertex_array_create(3, scdo_vertex_format, [scdo_vertex_buffer])

	# Create the uniform buffer for the screen resolution used for both the
	# stencil copy, and draw outline pipelines.  Each pipeline will have its
	# own uniform set for this buffer.
	var buffer = PackedFloat32Array([1, 1, 0, 0]).to_byte_array()
	scdo_uniform_buffer = rd.uniform_buffer_create(buffer.size(), buffer)

	## mark ourselves as dirty so everything else is created when we know the
	## render resolution
	rebuild_pipelines = true

# System notifications, we want to react on the notification that
# alerts us we are about to be destroyed.
func _notification(what):
	if what == NOTIFICATION_PREDELETE:
		for state in _buffer_states.values():
			(state as PerBufferState).free_gpu(rd)
		_buffer_states.clear()
		if jf_shader.is_valid():
			rd.free_rid(jf_shader)
		if sc_shader.is_valid():
			rd.free_rid(sc_shader)
		if do_shader.is_valid():
			rd.free_rid(do_shader)
		if scdo_vertex_buffer.is_valid():
			rd.free_rid(scdo_vertex_buffer)
		if scdo_uniform_buffer.is_valid():
			rd.free_rid(scdo_uniform_buffer)

## Load GLSL from a specific resource path
## Returns:
##  false: failed to load or compile shader
##  RDShaderSPIRV: compiled shader
func _load_glsl_from_file(path) -> Variant:
	# hot-reload of shaders via RDShaderFile does not work by default.  See
	# https://github.com/godotengine/godot/issues/110468 for details.
	if not _hot_reload:
		var shader_file: RDShaderFile = ResourceLoader.load(path)
		return shader_file.get_spirv()

	# Manually reload & compile the shader using RDShaderSource
	var lines = []
	if not FileAccess.file_exists(path):
		push_error("_load_glsl_from_file() file not found: ", path)
		return null
	
	var file = FileAccess.open(path, FileAccess.READ)
	if not file:
		push_error("_load_glsl_from_file() failed to open `", path, "`: ", FileAccess.get_open_error())
		return

	while not file.eof_reached():
		lines.append(file.get_line())

	file.close()

	var source := RDShaderSource.new()
	source.language = RenderingDevice.SHADER_LANGUAGE_GLSL
	source.source_vertex = ""
	source.source_fragment = ""
	source.source_compute = ""

	var type = null
	for line in lines:
		if line == "#[vertex]":
			type = "vertex"
		elif line == "#[fragment]":
			type = "fragment"
		elif line == "#[compute]":
			type = "compute"
		elif type == "vertex":
			source.source_vertex += line + "\n"
		elif type == "fragment":
			source.source_fragment += line + "\n"
		elif type == "compute":
			source.source_compute += line + "\n"

	var spirv: RDShaderSPIRV = rd.shader_compile_spirv_from_source(source)
	if spirv.compile_error_vertex != "":
		push_error("Failed to compile shader: ", path, "\n",
				   spirv.compile_error_vertex)
		return
	if spirv.compile_error_fragment != "":
		push_error("Failed to compile shader: ", path, "\n",
				   spirv.compile_error_fragment)
		return
	if spirv.compile_error_compute != "":
		push_error("Failed to compile shader: ", path, "\n",
				   spirv.compile_error_compute)
		return
	return spirv


func _free_global_shaders() -> void:
	_global_pipelines_valid = false
	if jf_shader.is_valid():
		rd.free_rid(jf_shader)
		jf_shader = RID()
	if sc_shader.is_valid():
		rd.free_rid(sc_shader)
		sc_shader = RID()
	if do_shader.is_valid():
		rd.free_rid(do_shader)
		do_shader = RID()
	jf_pipeline = RID()
	do_pipeline = RID()
	sc_uniform_set = RID()
	for state in _buffer_states.values():
		var st := state as PerBufferState
		if st.sc_pipeline.is_valid():
			rd.free_rid(st.sc_pipeline)
			st.sc_pipeline = RID()
		st.jf_uniform_sets = [RID(), RID()]


func _ensure_global_pipelines() -> void:
	if _global_pipelines_valid:
		return

	if sc_shader.is_valid():
		rd.free_rid(sc_shader)
		sc_shader = RID()
	if jf_shader.is_valid():
		rd.free_rid(jf_shader)
		jf_shader = RID()
	if do_shader.is_valid():
		rd.free_rid(do_shader)
		do_shader = RID()

	var sc_spirv = _load_glsl_from_file(sc_shader_file)
	if not sc_spirv:
		push_error("failed to load stencil copy shader")
		return
	sc_shader = rd.shader_create_from_spirv(sc_spirv)
	if not sc_shader.is_valid():
		return

	assert(scdo_uniform_buffer.is_valid())
	var uniform = RDUniform.new()
	uniform.uniform_type = RenderingDevice.UNIFORM_TYPE_UNIFORM_BUFFER
	uniform.binding = 0
	uniform.add_id(scdo_uniform_buffer)
	sc_uniform_set = rd.uniform_set_create([uniform], sc_shader, 0)

	var jf_spirv = _load_glsl_from_file(jf_shader_file)
	if not jf_spirv:
		push_error("failed to load jump flood shader")
		return
	jf_shader = rd.shader_create_from_spirv(jf_spirv)
	if not jf_shader.is_valid():
		push_error("failed to create jump flood shader")
		return
	jf_pipeline = rd.compute_pipeline_create(jf_shader)
	if not jf_pipeline.is_valid():
		push_error("failed to create jump flood compute pipeline")
		return

	var do_spirv = _load_glsl_from_file(do_shader_file)
	if not do_spirv:
		push_error("failed to load draw outline shader")
		return
	do_shader = rd.shader_create_from_spirv(do_spirv)
	if not do_shader.is_valid():
		return
	do_pipeline = rd.compute_pipeline_create(do_shader)
	if not do_pipeline.is_valid():
		push_error("failed to create draw-outline compute pipeline")
		return

	_global_pipelines_valid = true


## build the stencil-copy render pipeline for one viewport's buffers
func _build_sc_pipeline(state: PerBufferState) -> void:
	if state.sc_pipeline.is_valid():
		rd.free_rid(state.sc_pipeline)
		state.sc_pipeline = RID()
	if state.sc_framebuffer.is_valid():
		if rd.framebuffer_is_valid(state.sc_framebuffer):
			rd.free_rid(state.sc_framebuffer)
		state.sc_framebuffer = RID()

	if not sc_shader.is_valid():
		return

	assert(state.textures[0].is_valid())
	assert(state.depth_texture.is_valid())

	var attachments = []
	var attachment_format = RDAttachmentFormat.new()

	var texture_format = rd.texture_get_format(state.textures[0])
	attachment_format.format = texture_format.format
	attachment_format.usage_flags = texture_format.usage_bits
	attachment_format.samples = RenderingDevice.TEXTURE_SAMPLES_1
	attachments.push_back(attachment_format)

	var depth_format = rd.texture_get_format(state.depth_texture)
	attachment_format = RDAttachmentFormat.new()
	attachment_format.format = depth_format.format
	attachment_format.usage_flags = depth_format.usage_bits
	attachment_format.samples = RenderingDevice.TEXTURE_SAMPLES_1
	attachments.push_back(attachment_format)

	var format = rd.framebuffer_format_create(attachments)
	state.sc_framebuffer = rd.framebuffer_create(
		[state.textures[0], state.depth_texture], format)
	if not state.sc_framebuffer.is_valid():
		push_error("failed to create stencil copy framebuffer")
		return

	var blend := RDPipelineColorBlendState.new()
	var blend_attachment := RDPipelineColorBlendStateAttachment.new()
	blend.attachments.push_back(blend_attachment)

	var stencil_state = RDPipelineDepthStencilState.new()
	stencil_state.enable_stencil = true
	stencil_state.front_op_compare = RenderingDevice.COMPARE_OP_EQUAL
	stencil_state.front_op_compare_mask = stencil_mask
	stencil_state.front_op_reference = stencil_value
	stencil_state.front_op_fail = RenderingDevice.STENCIL_OP_KEEP
	stencil_state.front_op_pass = RenderingDevice.STENCIL_OP_KEEP
	stencil_state.front_op_depth_fail = RenderingDevice.STENCIL_OP_KEEP
	stencil_state.back_op_compare = stencil_state.front_op_compare
	stencil_state.back_op_compare_mask = stencil_state.front_op_compare_mask
	stencil_state.back_op_reference = stencil_state.front_op_reference
	stencil_state.back_op_fail = stencil_state.front_op_fail
	stencil_state.back_op_pass = stencil_state.front_op_pass
	stencil_state.back_op_depth_fail = stencil_state.front_op_depth_fail
	state.sc_pipeline = rd.render_pipeline_create(
		sc_shader,
		format,
		scdo_vertex_format,
		RenderingDevice.RENDER_PRIMITIVE_TRIANGLES,
		RDPipelineRasterizationState.new(),
		RDPipelineMultisampleState.new(),
		stencil_state,
		blend,
	)
	if not state.sc_pipeline.is_valid():
		push_error("failed to create stencil copy render pipeline")


func _build_jf_uniform_sets(state: PerBufferState) -> void:
	if not jf_shader.is_valid():
		return
	assert(state.textures[0].is_valid())
	assert(state.textures[1].is_valid())
	for group in [[0, state.textures[0], state.textures[1]],
				  [1, state.textures[1], state.textures[0]]]:
		var pass_number = group[0]
		var src_texture = group[1]
		var dest_texture = group[2]

		state.jf_uniform_sets[pass_number] = [RID(), RID()]

		var src_uniform := RDUniform.new()
		src_uniform.uniform_type = RenderingDevice.UNIFORM_TYPE_IMAGE
		src_uniform.binding = 0
		src_uniform.add_id(src_texture)

		var dest_uniform = RDUniform.new()
		dest_uniform.uniform_type = RenderingDevice.UNIFORM_TYPE_IMAGE
		dest_uniform.binding = 1
		dest_uniform.add_id(dest_texture)

		state.jf_uniform_sets[pass_number] = rd.uniform_set_create(
			[src_uniform, dest_uniform], jf_shader, 0)


func _build_textures(state: PerBufferState) -> void:
	var count = state.textures.size()

	var texture_format = RDTextureFormat.new()
	texture_format.texture_type = RenderingDevice.TEXTURE_TYPE_2D
	texture_format.width = state.resolution.x
	texture_format.height = state.resolution.y
	texture_format.format = RenderingDevice.DATA_FORMAT_R32G32B32A32_SFLOAT
	texture_format.usage_bits = (
		RenderingDevice.TEXTURE_USAGE_COLOR_ATTACHMENT_BIT |
		RenderingDevice.TEXTURE_USAGE_SAMPLING_BIT |
		RenderingDevice.TEXTURE_USAGE_STORAGE_BIT |
		RenderingDevice.TEXTURE_USAGE_CAN_COPY_TO_BIT
	)

	var texture_view = RDTextureView.new()

	for i in range(count):
		var rid: RID = rd.texture_create(texture_format, texture_view)
		assert(rid.is_valid())

		var old_rid: RID = state.textures[i]
		state.textures[i] = rid
		debug_textures[i].texture_rd_rid = rid

		if old_rid.is_valid():
			rd.free_rid(old_rid)


func _free_per_buffer_framebuffer_state(state: PerBufferState) -> void:
	if state.sc_pipeline.is_valid():
		rd.free_rid(state.sc_pipeline)
		state.sc_pipeline = RID()
	if state.sc_framebuffer.is_valid():
		if rd.framebuffer_is_valid(state.sc_framebuffer):
			rd.free_rid(state.sc_framebuffer)
		state.sc_framebuffer = RID()
	state.jf_uniform_sets = [RID(), RID()]


func _update_resolution_uniform(res: Vector2i) -> void:
	assert(scdo_uniform_buffer.is_valid())
	var buffer = PackedFloat32Array([res.x, res.y, 0, 0]).to_byte_array()
	rd.buffer_update(scdo_uniform_buffer, 0, buffer.size(), buffer)


func _get_buffer_state(render_scene_buffers: RenderSceneBuffersRD) -> PerBufferState:
	var key: int = render_scene_buffers.get_instance_id()
	if not _buffer_states.has(key):
		_buffer_states[key] = PerBufferState.new()
	return _buffer_states[key] as PerBufferState

# Called by the rendering thread every frame.
func _render_callback(_p_effect_callback_type, p_render_data):
	if not rd:
		return

	var render_scene_buffers: RenderSceneBuffersRD = p_render_data.get_render_scene_buffers()
	if not render_scene_buffers:
		return

	var size = render_scene_buffers.get_internal_size()
	if size.x == 0 and size.y == 0:
		return

	var state := _get_buffer_state(render_scene_buffers)
	var rebuild := false

	if rebuild_pipelines:
		mutex.lock()
		rebuild_pipelines = false
		mutex.unlock()
		_free_global_shaders()
		for st in _buffer_states.values():
			(st as PerBufferState).free_gpu(rd)
		_buffer_states.clear()
		state = _get_buffer_state(render_scene_buffers)
		rebuild = true

	var color_tex = render_scene_buffers.get_color_layer(0)
	var depth_tex = render_scene_buffers.get_depth_layer(0)

	if state.resolution != size:
		state.resolution = size
		rebuild = true
	if state.depth_texture != depth_tex:
		state.depth_texture = depth_tex
		rebuild = true
	state.color_texture = color_tex

	if rebuild:
		_ensure_global_pipelines()
		_free_per_buffer_framebuffer_state(state)
		_build_textures(state)
		_build_sc_pipeline(state)
		_build_jf_uniform_sets(state)

	if not _global_pipelines_valid:
		return
	if not state.sc_pipeline.is_valid() or not jf_pipeline.is_valid() or not do_pipeline.is_valid():
		return

	_update_resolution_uniform(state.resolution)

	var draw_list := rd.draw_list_begin(
		state.sc_framebuffer,
		RenderingDevice.DRAW_CLEAR_COLOR_0,
		[Color(-1, -1, 2**15, -1)],
		1.0,
		0,
		Rect2(),
		RenderingDevice.OPAQUE_PASS)
	rd.draw_list_bind_render_pipeline(draw_list, state.sc_pipeline)
	rd.draw_list_bind_vertex_array(draw_list, scdo_vertex_array)
	rd.draw_list_bind_uniform_set(draw_list, sc_uniform_set, 0)
	rd.draw_list_draw(draw_list, false, 3)
	rd.draw_list_end()

	@warning_ignore("integer_division")
	var x_groups : int = (state.resolution.x - 1) / 8 + 1
	@warning_ignore("integer_division")
	var y_groups : int = (state.resolution.y - 1) / 8 + 1
	var jf_push_constant := PackedByteArray()
	jf_push_constant.resize(16)

	var compute_list := rd.compute_list_begin()
	rd.compute_list_bind_compute_pipeline(compute_list, jf_pipeline)

	for i in range(_passes):
		var stride = (1<<(_passes-i-1))
		jf_push_constant.encode_u32(0, stride)
		rd.compute_list_bind_uniform_set(
			compute_list,
			state.jf_uniform_sets[i & 0x1],
			0)
		rd.compute_list_set_push_constant(compute_list, jf_push_constant, jf_push_constant.size())
		rd.compute_list_dispatch(compute_list, x_groups, y_groups, 1)

	rd.compute_list_end()

	var src_uniform := RDUniform.new()
	src_uniform.uniform_type = RenderingDevice.UNIFORM_TYPE_IMAGE
	src_uniform.binding = 0
	src_uniform.add_id(state.textures[_passes & 0x1])
	var dest_uniform = RDUniform.new()
	dest_uniform.uniform_type = RenderingDevice.UNIFORM_TYPE_IMAGE
	dest_uniform.binding = 1
	dest_uniform.add_id(state.color_texture)
	var uniform_set = UniformSetCacheRD.get_cache(do_shader, 0, [src_uniform, dest_uniform])
	assert(uniform_set.is_valid())

	var do_push_constant = PackedByteArray()
	do_push_constant.resize(20)
	do_push_constant.encode_float(0, outline_color.r)
	do_push_constant.encode_float(4, outline_color.g)
	do_push_constant.encode_float(8, outline_color.b)
	do_push_constant.encode_float(12, outline_color.a)
	do_push_constant.encode_u32(16, thickness**2)

	compute_list = rd.compute_list_begin()
	rd.compute_list_bind_compute_pipeline(compute_list, do_pipeline)
	rd.compute_list_bind_uniform_set(compute_list, uniform_set, 0)
	rd.compute_list_set_push_constant(compute_list, do_push_constant, do_push_constant.size())
	rd.compute_list_dispatch(compute_list, x_groups, y_groups, 1)
	rd.compute_list_end()
