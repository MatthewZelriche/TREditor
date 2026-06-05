# TODO

## Primitive creation

- [ ] **Define interactive Sphere and Plane creation semantics** before wiring them into the dynamic creation flow. Box and Cylinder fit the current footprint-plus-height workflow, but Sphere needs a radius/ellipsoid decision and Plane likely needs a footprint-only commit path.

## Selection highlighting

- [ ] **Size component selection highlights per viewport camera**. Component selection highlighting currently resizes based on distance from the active viewport's camera, instead of from each viewport's camera independently.

## Viewport camera settings

- [ ] **Hook up the camera perspective menu** in `scripts/ui/viewportWorkspace/ViewportCameraSettingsPopup.cs` — wire the `ProjectionOption` `OptionButton` (scene path: `Margin/Column/Controls/ProjectionOption` in `ViewportPane.tscn`) to the pane’s `ViewCamera` so users can switch between perspective and orthographic projection from the camera settings popup.
