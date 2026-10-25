# Scenes and capabilities

A **scene** supplies authored geometry, rig templates and a compatible controller. An **experiment file** supplies its stimulus/motion parameters. A **sequence** chooses scene/file combinations and timing. Reusing an environment with a different JSON file is usually preferable to duplicating a whole scene for a parameter change.

## Compact desktop scene set

| Scene | Purpose | Controller/config path |
| --- | --- | --- |
| `ControlScene` | Operator studio and sequence launch | MainController, ConfigWorkbench, session metadata |
| `Choice` | Objects, bars, backgrounds and ordinary choice/visual tasks | ChoiceController; `parameters.configFile` |
| `Choice_desync` | Design-driven Choice with independent per-animal progression | DynamicSequenceController; `parameters.design` |
| `Choice_Selwyn` | Terrain-scale Choice/flight, astronomical sky and compatible embedded swarm | Choice family plus authored terrain |
| `Swarm` | Generated or recorded virtual population around each animal rig | SwarmController and Kannadi/Swarm schema |
| `Kannadi` | Matrix of mirrored animal poses | Kannadi and its prefab catalog |
| `Optomotor` | Rotating drum/grating stimulus | OptomotorSceneController and optomotor config |
| `TerrainCueLab` | Independent visual/navigation geometry, height and tilt controls | Choice-compatible configuration plus RugosityWorld |
| `Rugosity_RS` | Preserved earlier rugosity experiment | Historical scene/config behavior retained |
| `ReplayScene` | Saved-session timeline and inspection views | ReplayController; archived inputs rather than current live JSON |

Optional `Choice_Forrest` and the two-tree variants remain in source. Recipes marked `sourceBuildOnly` require enabling those authored scenes and rebuilding. They are not hidden downloads or automatically generated levels. The [gallery](gallery.md) distinguishes genuine source-scene captures from which scenes ship in the compact player. Its forest preview instantiates the bundled Landscape prefab into Choice_Forrest for the capture; that source customization is recorded explicitly and is not the scene’s default ground/forest setup.

## Choice objects, bars and fixation

ChoiceController reads complete experiment JSON and uses its registered prefab/material resources. Trials can place objects at specified positions or angular arrangements, change their appearance, and select tracking/autopilot/reset behavior. Empty or omitted object lists support sky-only terrain conditions where the parser permits them.

Bars and stripe fixation are visual task recipes inside these controller families; they are not a claim of a separate dedicated scene for every stimulus. Dynamic Choice designs can sequence changes within one environment and trigger steps by their configured time/area rules. Keep per-animal step labels in the recording when asynchronous animals advance independently.

## Swarm populations

Generated agents use preferred `agentCount`, `agentSpeed` and `agentSpeedStdDev` names while old number/speed aliases remain readable. Direction/concentration/elevation, spawn volume, wrapping, density and visual dimensions define the presented population. A generated population and a recorded trajectory file have different timing controls; `playback.speed` does not mean generated agent speed.

The modern Bogong visual supports dot/angular modes and a physical moth representation with GPU wing motion. Recorded tracks can be anchored to each animal's world position and continued according to their recipe. See [generated swarm](../generated-bogong-swarm.md) and [recorded swarm](../swarm-playback.md).

## Kannadi: the mirror matrix

**ಕನ್ನಡಿ (Kannadi) means “mirror” in Kannada.** Kannadi replicates the tracked animal's pose across a configurable hexagonal matrix. The replicas are virtual **agents**, derived from real animal motion rather than independently measured animals.

`numberOfRings`, `hexRadius` (legacy `spacing`) and the selected `kannadiTilePrefab` determine the arrangement. Per-animal `vrConfigs` can set initial pose and `watchIndex`. A specified watch index chooses a configured animal population; without it the view contains the other animal layers. Self-view handling skips the overlapping centre where appropriate. Camera masks retain shared floor/world geometry while replacing animal-layer visibility.

The full matrix is a stimulus arrangement, not a second physical tracking array. A recorded Kannadi clone row identifies a visible-model root; a root can contain a mirrored band rather than one biological animal. The [data reference](../data-formats.md) explains the schema, and [integration history](../kannadi-modernization.md) retains the original validation context.

## Night sky, terrain and panoramas

Choice_Selwyn and other supported Choice/Swarm/Kannadi experiments can use `nightSky` to orient the bundled stars/Milky Way to location and time. This is a generated texture stimulus. It does not add atmosphere, planets, the Sun or the Moon.

`skyboxPath` supplies an explicit panorama instead. Generated sky and explicit image precedence are documented per parser. A panorama background does not define collision or navigable terrain; a terrain mesh and its scale remain separate. Homing-style environments can combine authored terrain, configured landmarks and a panoramic background without making them one file format.

TerrainCueLab separates **what is rendered** from **what controls navigation**. Matching, mismatching and height/tilt isolation conditions apply explicit surfaces; “rough” is a geometry condition, not an asserted perceptual result. [Rugosity](../rugosity.md) documents actual mesh samples and precedence.

## Optomotor

The drum/grating path owns stimulus motion and disables general forward autopilot. Its config describes frequency, contrast, duty cycle, speed, axis/direction and tracked loop switches. Stimulus/drum transforms must not be interpreted as the real animal pose. Generated mesh/material/texture ownership matters when repeatedly rebuilding the scene.

## Scene extension boundary

The studio configures experiments that the build knows how to render. New environments require Unity authoring, controller/prefab registration and build-list changes. There is no general level editor. Follow the [extension workflow](extending.md) when the desired condition cannot be expressed by existing parameters.
