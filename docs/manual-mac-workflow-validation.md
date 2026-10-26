# macOS workflow validation — 26 October 2026

**Partial manual validation — final replay controls await device unlock.** Current candidate: `2494d882`; Unity `6000.3.16f1`. Recording manifests do not contain complete application build revisions; source identities below also use operator observations.

These were webcam/software exercises with **no animal measurement**. Saved provenance retains `actualInputSource: unverified`. Separately, the native tracker title showed 29.9 FPS on port 9874, and live packet 57980 reported `source_kind=camera`, `source_fallback=false`. The image was almost black with no animal: this establishes live-camera plumbing, not biological tracking accuracy.

## Recordings and replay

| Run | Raw experiment rows / span | Current importer: rows / contexts / span | Finding |
| --- | --- | --- | --- |
| `20261026_022145` | 214,769 / 1811.909276 s | 214,768 / 4 / 1811.897717 s | Rotation only; exposed autopilot gating. Excludes one idle row and 3,480 failed-replay sidecar rows. |
| `20261026_031046` | 23,606 / 205.373206 s | 23,596 / 10 / 205.363811 s | Five trials × two 20 s scenes; gain 3, autopilot 7, manual keys disabled. Mean within-context XZ speed **7.0011 units/s**. Excludes nine handoffs and one idle row. |
| `20261026_035227` | 10,219 / 110.776208 s | 10,219 / 44 / 110.776208 s | Fresh `dab56e82` recording: 22 trials × two 2 s scenes; **zero scene/sequence mismatches, idle rows or reader exclusions**. Median within-context frame speed **7.0003 units/s**. |

All retained contexts resolve to saved configurations. All three recordings have 55-column pose rows with no malformed/nonfinite values or timestamp/frame reversals or duplicates. The first experiment log has contiguous frames. The 20 s run has nine one-frame omissions at scene changes (0.506–0.715 s loading gaps). The 2 s run has 43 transitions, each omitting two gated/loading frames (86 total; 0.511–0.616 s gaps). Largest frame intervals are 1.012 s and 1.216 s respectively; constant 120 Hz operation is not claimed.

Each run’s four config snapshots/readable copies and 4096×2048 skybox PNG passed hash/size checks. The fresh run has 44 sky applications and no ReplayScene CSV. Fresh scoped comparisons for candidate `2494d882` found all 23 files in `031046` byte-identical. In `035227`, all 21 core files and the manifest match; only the previously observed 61-byte shutdown trace append differs, with its original prefix matching. Neither session has added or missing files. Final post-replay comparison remains pending.

Reader exclusions require explicit modern context and a verified saved sequence. Uncertain legacy aliases remain untouched; local-wall-clock, unlogged-animation and tracker-validity limitations remain disclosed. Original acquisition files are preserved.

![Configured Mac player ready to run](images/manual-mac-configured-start.jpg)

Actual native player capture from `8b3287a0`, before the final replay toolbar revision.

## Actual manual observations

- Configure typing, expanded VR2→VR5 rename, port 9892 and vertical packing retained scroll/foldout state. Actual Start produced the three recordings.
- On `8b3287a0`, duplicate changed 2→3 blocks and 4→6 s, with matching header/overview and row 03 Choice selected. Remove restored 2 blocks / 4 s and row 02 Choice selected.
- Live plots launched native DaariDeepa (PID 54224, parent PID 1); reopen created no duplicate. Browser plots showed heading arrows, signed heading, context and units.
- Dashboard Pause held 75 samples across the next trial while acquisition/metadata continued. Resume showed 116 samples/current context; stopping Unity produced stale status.
- Quitting Unity left both companions running. Quitting the tracker left Unity and dashboard running. These are operator observations, not CSV inferences.
- With `55ecc76c` replay running, the browser remained stale on the prior `043609` session; no new replay publisher was observed. This supports replay pose/context suppression during that observation interval.

## Reproduced bugs and fixes

| Finding | Source fix |
| --- | --- |
| Repeated fullscreen geometry requests. | `ca95c288`; not established as the replay pointer failure’s cause. |
| Delayed-field commit swallowed the first click. | `38e3efbb`, `41517bb5`: defer rebuilding and recover pointer release. |
| Draft Start rejected setup; sequence errors blocked hardware completion. | `11322654`, `8bb9e355`: configuration action and hardware-only validation. |
| Per-rig output and CLI/global fallback disagreed. | `7e7f1cc7`, `b98d3389`, `8bb9e355`: consistent routing/validation. |
| Disabling keyboard controls stopped autopilot. | `b738fb05`; movement verified above. |
| Missing replay components caused exceptions/live side logging. | `891880f9`: isolated, fail-closed reconstruction. |
| Replay sidecars and handoff metadata created false contexts. | `ee86c6ea`, `039b2303`, `ef9bba78`: producer gating and conservative reader filtering. |
| Sequence totals/selection and Retina replay UI were clunky. | `381fe9a5`, `ee4be416`, `dab56e82`; sequence test passed, replay uses native pixels without the Lemon marker. |
| Replay must stay outside live acquisition telemetry. | `55ecc76c`: suppress ReplayScene pose/context publication; bounded stale-view observation above. |
| Earlier native event-restoration attempts did not restore usable IMGUI input. | That approach was disproved. `2494d882` puts interactive controls outside moving clips; actual interaction is pending unlock, not a newly reproduced failure. |

Supporting checks passed: **1002 total = 880 across 10 scripts + 67 telemetry/replay policy + 55 replay UI tests**; the batch includes 150 writer lifecycles. The previous event-type restoration approach failed native diagnostics and was replaced with controls in the root GUI clip. These checks do not establish the final real pointer behavior: the Mac locked after the `2494d882` build/launch, before that interaction could be tested.

## Remaining manual checks

- After the Mac is unlocked, exercise actual Play/Pause, forward/backward scrub, Browse and Back on `2494d882`. The final native-root implementation has not yet passed or failed this manual check.
- Repeat the scoped source-file comparison after replay exits.
- Preserve the distinction between observed camera packets and older archived provenance labels; no biological performance claim is made.
