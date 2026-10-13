## Menu Bar

### File ✔️
- [x] New
- [x] Open...
- [x] Save
- [x] Save As...
- [x] Quit

### Edit ✔️
- [x] Undo
- [x] Redo

### Settings
- [x] Keybindings...
- [ ] Preferences...

### About
- [ ] Open Source Licenses...
- [ ] About TrembleEd...


## Views
- [x] Implement the main View area via TilingContainer, allow for arbitrary splitting of main view area into subviews
- [x] Camera movement controls for the views
- [x] Perspective 3D View
- [x] Scene Grid
- [ ] Orthographic 3D View
- [ ] Orthographic 2D View
- [ ] Display Wireframe Overlay

## Mesh Operations ✔️

- [x] Translation gizmo for all three component types
  - [x] TranslateTool respects Component Selection Mask
- [x] Object Translate
- [x] Object Rotate
- [x] Object Scale
- [x] Create new Primitive
  - [ ] Create Polygon Primitive

### Vertices
- [x] Merge
  - Merges N vertices into either a centroid position or at First/Last vertex. Default Last.
- [ ] Bevel
  - Bevel N selected vertices. Newly bevelled default distance is in the range of [Current Grid Snap, Shortest Maximum Bevel Distance for any selected vertex]
- [ ] Select Vertex Loop
  - Select two vertices or more vertices in sequence. Selects the entire loop  of vertices beloning to those selected vertices

### Edges
- [x] Dissolve
  - Dissolves N selected edges by deleting those edges and merging the faces between them into a single face.
- [x] Collapse
  - Collapses N connected edges by merging each edge's two vertices into a single vertex at the midpoint
- [ ] Merge
  - Merge 2 edges together. 
- [ ] Bevel
  - Bevel N Edges. Provides configurable options for: 1. Steps (number of new edges to create), 2. Shape (How angled from 0 - 90 degrees), 3. Width (How thick the bevel will be).
- [x] Extend
  - Extends an open edge outward in the planar direction of where the edge is pointed. Default extend length: Grid Snap. Configurable more or less. 
- [ ] Bridge Edges
  - Bridge N (?) edges together, optionally with interpolation and a number of intermediate steps with controls to configure the resulting shape/number of steps.
- [x] Edge Loop Selection
  - With zero or more edges selected, double click an edge to select the entire loop it is a part of. 
  - Needs to be made more robust as it currently only handles valence 4 vertices
- [ ] Edge Ring Selection
  - Same as above - but selects an edge ring (edges not connected but share faces)

### Faces 
- [x] Bridge Faces
  - Bridge 2 Faces together, optionally with interpolation and a number of intermediate steps with controls to configure the resulting shape/number of steps. Faces must have the same number of edges.
- [ ] Thicken Face
  - Thickens an open face (a plane) with a desired thickness
- [x] Combine Faces
  - Combine N faces together by dissolving the edges between them.
- [x] Collapse Faces
  - Collapse N faces together into a single midpoint vertex
- [ ] Subdivide Faces
- [x] Extrude Face
- [ ] Inset Bevel Face Island
- [ ] Select Face Loop
- [ ] Grow/Shrink Selection


## Selections
- [x] Mesh Object Selection
- [x] Mesh Component Selection (Free, can select or multi-select combinations of component types)
- [x] Single-Component Mesh Selection (Select Vertices, Edges, or Faces only)
- [x] X-Ray Selection
- [x] Multi-selection
- [ ] Box-Selection
- [ ] Select All, Select None, Select Inverse (Specific Components on a Mesh, must occur in "Single-Component Selection Mode")
- [x] Grid Snapping
- [x] Angle Snapping
- [ ] Move Pivot
- [ ] Reset Pivot Location

## Edge Cut Tool


## UVs/Texturing


## Overlays/Decals
