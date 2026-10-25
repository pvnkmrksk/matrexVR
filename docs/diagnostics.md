# Scene reload investigation

This investigation uses **two-second experiments with a full Unity scene reload for every experiment**. The asynchronous in-scene reset path is not used by the reload soak. Testing was performed on 25 October 2026 with Unity 6000.3.16f1, macOS, Apple Silicon and Metal rendering.

The original historical native Unity crash has not been reproduced. A concrete failure in the original tracking listener has been reproduced and isolated: shutting down a socket on the main thread while its worker is blocked in a receive leaves workers and socket resources behind. One run also recorded a NetMQ I/O thread exception followed by loss of tracking input. A separate material ownership regression was found and corrected. These findings must not be described as a captured native crash or proof that every historical crash has the same cause.

## What the test exercises

`Assets/Editor/SceneReloadDiagnostics.cs` writes a sequence with `duration: 2` and `reloadScene: true` on every row, disables randomisation and looping, and checks that every loaded experiment receives a new scene handle. The normal profile alternates `Choice_desync` and `Swarm`: two changing visual objects per animal in Choice, and 64 generated Bogong moth agents per animal in Swarm. All four animal rigs run throughout.

An external process publishes real, finite tracking packets on four local ZeroMQ PUB sockets at 120 Hz. The test samples after `Start`, approximately 0.55 and 1.55 seconds into each experiment. Each active sample checks that all four receivers have recent packets, and the second sample verifies that each receiver advanced. `sceneLoaded` observations are explicitly marked as occurring before `Start`; zero counters at that point are not used as evidence of resource cleanup.

Graphics are enabled. The authored animal cameras render normally, and each enabled panel camera is also rendered into a 128 × 128 offscreen target every Unity frame. These diagnostic targets are counted separately and destroyed on unload. Choice authors 20 panel cameras and Swarm authors 24. The test checks the actual authored count rather than assuming that every scene has six faces per rig. It does not duplicate a laboratory LED wall, its full resolution, drivers or physical tracker.

Every active sample records managed memory, Unity allocated/reserved memory, native Material/Mesh/Texture/RenderTexture counts and bytes, GameObjects, open descriptors, tracking workers, writer workers and writer backlog. A separate supervisor records process RSS, virtual size and actual operating-system thread counts. After the protocol it measures cleanup both before and after explicit garbage collection and `Resources.UnloadUnusedAssets`; neither is forced between experiments.

## Original listener: matched comparison

The archival project was reconstructed from commit `779926a80b02190d6dd0410f260e52bba105a04e`. All 2,484 tracked project files were present, and all 188 C# files matched that commit before adding the diagnostic runner. The matched comparison changes exactly one runtime file, `ZmqListener.cs`; the original NetMQ and AsyncIO DLLs and all other runtime source remain identical.

The input interruption profile pauses the publishers 0.25 seconds before each experiment ends, then resumes them when the next scene loads. Both active sample points still have four recent, progressing inputs. This exposes a listener waiting for the next packet at destruction.

| Observation, 40 forced reloads | Original listener | Worker-owned socket + 100 ms reads |
| --- | ---: | ---: |
| Active samples with all four live inputs | 80 / 80 | 80 / 80 |
| Active open descriptors, first → last | 113 → 183 | 109 → 109 |
| Shutdown deadline warnings | 37 | 0 |
| Descriptors after complete scene teardown | 167 | 93 |
| OS threads near first → last active sample | 120 → 149 | 127 → 123 |
| Native Unity process crash | None captured | None captured |

The original run has two shutdown timeouts while leaving the initial Control scene and another 35 during experiment reloads. Active descriptors grow by 70, or two per failed experiment-worker shutdown. After complete teardown, the original retains 74 more descriptors than the treatment, matching two per each of the 37 timeouts. This is strong evidence of a resource growth mechanism in the old listener. The comparison does not measure how many reloads would exhaust resources on a different system.

The old implementation creates the socket on the main thread, blocks on `ReceiveFrameString` on a worker, then disposes the same socket on the main thread during destruction. It waits 500 ms for the worker and discards the thread reference even if the worker remains alive. The treatment creates, receives from and disposes the socket on the worker, and uses bounded 100 ms receives so the stop flag can be observed without another packet. It does not invoke global NetMQ cleanup between scenes. This agrees with NetMQ's documented [socket threading restrictions](https://netmq.readthedocs.io/en/stable/poller/) and [socket cleanup lifecycle](https://netmq.readthedocs.io/en/stable/cleanup/).

An earlier original-listener attempt recorded this actual exception on the second reload:

```text
NullReferenceException
NetMQ.Core.Pipe.ProcessPipeTermAck
NetMQ.Core.ZObject.ProcessCommand
NetMQ.Core.IOThread.Ready
NetMQ.Core.IOThreadMailbox.RaiseEvent
NetMQ.Core.Utils.Proactor.Loop
```

Subsequent experiment receivers stopped receiving, and descriptors rose from 116 to 229 by load 17 while the publishers continued. That attempt was then terminated by the **diagnostic supervisor**, after its timestamp parser rejected a five-digit fractional timestamp. Its exit was SIGTERM, not an observed native Unity crash. The parser was corrected before the complete 40-load comparison above. No macOS crash report was produced by that attempt.

## Material ownership regression

`DynamicSequenceController` installed an owned material for each object, but the later colour swap accessed `Renderer.material`. That access created another instance and detached it from the owner. The swap now changes the already-owned `sharedMaterial`.

The focused native probe rebuilds eight visual objects 40 times and exercises both colour-swap paths, with no garbage collection or asset unload between cycles. With the previous swap implementation, destroying all objects leaves **320 native materials** while the ownership counter incorrectly reads zero. With the correction, the rendered material stays identical to the owned material, the live count stays at nine (eight objects plus their source), and destruction leaves **zero** probe materials. This is a regression in the later ownership implementation, distinct from the older full-scene reload failure.

## Completed reload evidence

The first modern run and the matched pre-modernisation `c13d33dd` run each completed 200 full two-second reloads with 400 active samples, all four inputs progressing, all authored cameras rendered, and no native process crash. Their per-scene live native resource counts stayed flat. Both exposed two null-sky UI summary exceptions on the final return to Control; these are preserved in their results, and were corrected before the final current-source rerun.

The final current-source run **passed all 200 reloads**: 400 active samples, no stale or nonprogressing input, no runtime exceptions, no shutdown timeouts, and all 20/24 authored panel cameras rendered. Actual scene intervals ranged from 2.002 to 2.301 seconds (median 2.215), including transition work. Before forced garbage collection, teardown left zero tracking workers, writer workers, owned trial materials and diagnostic render targets; these stayed zero after collection. Live native counts stayed constant within each scene. One Unity Editor search-index startup exception was preserved and classified separately from runtime errors.

Detailed current-source results and the separate terrain/night-sky profile are recorded in [the machine-readable results](scene-reload-diagnostics-results.json). Source and plugin fingerprints identify the exact tested working tree, since several fixes are newer than the base commit.

The terrain profile also **passed 40 full two-second reloads**, with the publishers paused before every unload. All 80 active samples verified two terrain components, a recorded 4,096 × 2,048 astronomical sky and four progressing animal inputs. All 24 panel cameras rendered; live counts remained 188 materials, one mesh, 100 textures and 32 render textures. There were no runtime errors or worker shutdown timeouts. Scene intervals were 2.014–2.990 seconds, including terrain/sky setup; teardown left zero tracking workers and owned trial materials.

The terrain scene emits five existing missing-script warnings per load: three `InfinityCode.RealWorldTerrain.RealWorldTerrainItem` and two `RealWorldTerrainContainer` importer metadata components refer to absent scripts. Native Unity Terrain and the recorded sky are present and rendered, but these 200 warnings are retained in the results. They are not classified as a resource leak. The probe's initial idle-sky inspection also required a shader-property guard; that intentionally interrupted attempt is recorded separately from the clean 40-load repeat.

## Reproduce on the diagnostic host

Use a disposable project copy with `KANNADI_VALIDATION_COPY` at its root. Never use the original working project: the runner temporarily replaces its copy's system and sequence configurations, restores them on normal completion, and saves the originals in the output folder. If the process is interrupted, restore those two files from `original-configs` before running another test.

```sh
python3 tools/run_scene_reload_diagnostics.py \
  --project /absolute/path/to/disposable-project \
  --output /absolute/path/to/new-output-folder \
  --count 200 --duration 2 --scenes Choice_desync,Swarm

python3 tools/analyze_scene_reload_diagnostics.py \
  /absolute/path/to/new-output-folder
```

For the interrupted-input comparison add `--pause-before-reload 0.25 --scenes Choice_desync --count 40`. For terrain and automatic sky use `--profile terrain-night-sky --scenes Choice_Selwyn --count 40`. Override `--unity` and `--libzmq` for the installed host paths. This supervisor currently uses macOS process tools; it is not a Windows/Linux soak result.

Raw JSONL observations, complete Unity logs, publisher progress, source hashes and any newly generated native crash reports remain in the ignored diagnostic output directory. The supervisor captures native stacks and a memory-map summary if main-thread progress stops, then terminates only its own child. A supervisor termination is explicitly distinguished from a crash. Historical `mono_crash.mem.*.blob` files present in the repository contain only zero bytes and supplied no usable stack evidence.

Full reloads now have a repeatable regression path, but an eight-minute accelerated Editor soak does not certify multi-hour experiments, Windows/Linux graphics, laboratory hardware, arbitrary custom scenes or every resource limit. Keep both the full-reload path and the in-scene transition option available, and repeat the test with the exact experiment configuration and hardware if the historical crash recurs.
