# Rigs and viewports

A rig is the virtual observation pose controlled by one animal. It normally contains ClosedLoop, a tracker listener, manual controls, a ViewportSetter, stimulus cameras and compatible recording components. `VRn` is the routing identity that connects the rig to one system configuration entry.

## Configured rig count

RuntimeRigRegistry reconciles positive, unique `VRn` identities with the scene's authored templates. It disables omitted rigs and clones a template for additional configured identities before those rigs start tracking. Sparse IDs such as VR10 are valid; VR1 must not accidentally match VR10.

Each independent stimulus population requires an isolated Unity user layer. The authored first four populations have named layers; extra populations use available unnamed layers. Capacity is finite and depends on the build's layer layout. An exhausted layout produces a configuration error rather than silently sharing a population. JSON configuration does not provision another computer, tracker or LED panel.

## Camera-face allow-list

ViewportSetter is authoritative for its own stimulus cameras. It starts by disabling them, then enables only named faces listed in the rig's system `displayOrder`. Valid letters are **D, R, B, L, F, U**: down, right, back, left, front, up. Normalization uppercases, removes unknown letters and deduplicates while preserving valid order.

A camera must match `Main Camera <letter>`. A camera absent from the normalized order stays disabled even if its prefab checkbox was enabled. A rig absent from system config renders none of its stimulus views. This protects against old serialized camera state exposing the wrong view.

## Pixel-to-viewport mapping

`ledPanelWidth`/`ledPanelHeight` are pixel dimensions of one face. `startCol` and `startRow` locate its first slot on the selected display; rows count from the top. `horizontal` chooses whether successive camera slots advance across columns or down rows.

For display width W and height H:

```text
w = ledPanelWidth / W
h = ledPanelHeight / H
x = (startCol + (horizontal ? slot : 0)) * w
y = 1 - (startRow + 1 + (horizontal ? 0 : slot)) * h
viewport = (x, y, w, h)
```

These are normalized Unity camera rectangles, not a physical LED wiring map. Configure the actual display dimensions and verify the result on the apparatus. The camera field of view is set to 90°. Up/Down faces inherit the matching front camera's population mask, position and clipping planes so vertical faces do not accidentally expose another rig's population.

## Physical display selection

System `targetDisplay` selects the zero-based Unity display and is applied to each rig. An invalid/missing display or nonpositive panel size leaves cameras disabled. Secondary displays are activated when needed. ViewportSetter uses Screen dimensions for display 0 and the secondary display's rendering dimensions for other displays; resolution changes trigger a layout refresh.

A small desktop window can therefore produce a very small or displaced face strip if the system file still describes a full LED canvas. Use a desktop layout intentionally for inspection rather than assuming a monitor view is a calibrated arena.

## Shared geometry and isolated animals

Camera culling masks combine shared environment geometry with selected agent layers. Runtime rig provisioning remaps additional-rig stimulus masks. Kannadi changes self/peer population visibility. Operator marker layers remain excluded from stimulus cameras.

A layer mask is not a physical display index. `watchIndex`, `vrId`, `targetDisplay` and `displayOrder` describe different relationships; do not reuse one number for another without an explicit mapping.

## Operator overview

The overhead view is an analysis/operator camera with numbered heading arrows and fading animal trails. Right drag orbits, middle drag pans and the wheel zooms inside the image. Hiding it stops its rendering/tracking work and releases its render target; it does not turn off stimulus cameras or telemetry.

The independent FPS/status display can target another display. Replay's animal-view cameras are also analysis views; they do not reconstruct a hardware-calibrated LED layout merely by loading a recorded system file.

## Source and full reference

[ViewportSetter](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Scripts/ViewportSetter.cs), [RuntimeRigRegistry](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Scripts/RuntimeRigRegistry.cs), [six-camera reference](../six-camera-rig.md), [hardware checklist](hardware.md).
