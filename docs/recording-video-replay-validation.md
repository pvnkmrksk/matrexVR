# Fresh recording and linked video replay — 26 October 2026

A fresh macOS development-player recording appeared automatically in the replay library, and its actual saved poses and native tracker video replayed together. Forward and backward scrubs changed both the trajectory position and corresponding video frame. This exercise used **generated Rekkefly demo input**, with no physical camera, animal or LED timing claim. The published `v0.3.1` archives were unchanged.

[Machine-readable aggregate results](recording-video-replay-validation-results.json) contain the build identity and validation limits. Raw recordings, video, local paths and process/session receipts are not published.

## Acquisition, discovery and collection

| Check | Observed result |
| --- | --- |
| Fresh experiment | Started through the native GUI with **Save tracker video** enabled; flying demo in Night sky (`Choice_Selwyn`). |
| Natural completion | One VR, one sequence cycle, planned 24 s; **2,836 playable pose rows**, zero rejected rows and **23.879249 s** between first and last pose timestamps. Writers drained; no finalization error. |
| Replay library | Listed both fresh recordings, automatically selected the newest, and opened it through **Replay selected**. |
| Independent recorder | Continued independently of Unity. Its native Cmd+Q produced a persisted Darwin clean-exit acknowledgment. |
| Later collection | **Collect media** worked after Unity restarted: one **640×480 MJPG AVI**, **1,709 frames**, nominal **60 FPS**. |

The first fresh experiment saved 2,804 pose rows but exposed a missing recorder-close acknowledgment: Launch Services starts the Mac tracker as a nonchild, for which the managed `Process.ExitCode` watcher was unreliable. Commit `e96b2724` observes the real Darwin wait status. Only clean exit zero acknowledges media; nonzero, killed or unverifiable exits stay pending. Twenty actual nonchild-process checks and the 102 tracker contracts passed before the second native recording.

Commit `c62c4bd9` separately seals completed sessions against late scene loggers and sidecar writes. Sixteen production-method file checks cover late Start, buffered drain, rotation and sky archive paths; 65 recording-summary checks passed. Removing the per-instance close guards reproduced the late-start regression.

## Replay and trajectory checks

The native player displayed the animal's rendered view alongside the recorded tracker video in God's-eye mode. Full animal view, return to God's-eye, Play and Pause were exercised. A forward scrub reached **19.762 s / displayed video frame 1,204**; a reverse scrub reached **4.963 s / displayed frame 316**. Displayed frame numbers are one-based.

A read-only production-parser audit passed **37 checks** on the separately acquired recording. All **2,822 consumed-input anchors** joined the saved pose to the correct source frame and source-clock timestamp. First, middle and last resolved JPEG frames matched their native originals. The test probe authored no poses.

This association means the software consumed that tracker frame for that pose. It does not establish camera exposure, physical animal-tracking accuracy or LED presentation timing. The remaining pose rows have no consumed-input anchor; synchronization is not invented for them.

An earlier fresh recording also exercised the recorded trajectory dashboard: seeking to **18 s** in its GUI set Unity's replay clock to **18.0 s**. Final visual confirmation of the newly styled dashboard remains outside this report until the final walkthrough is added.

## Preservation and limits

All **69 previously baselined recording files** remained unchanged. The fresh pose CSV and three original native recording files also remained unchanged (**4/4**). Collection adds verified copies and media metadata to the run folder. It preserves the existing ZIP, which remains an older snapshot without the later collected media; use the complete updated folder to transfer this replay. Changed media metadata correctly invalidates the cached finalized summary so the library can rescan it.

Native computer-control window routing was intermittent. Restarts and CLI replay bootstrapping were needed before returning to the GUI library, so this was not a seamless uninterrupted walkthrough. The successful GUI actions and the read-only parser checks are distinct evidence.

No physical camera/animal, LED scanout, multi-monitor apparatus, Windows or Linux walkthrough was validated here. No new 300-reload endurance result is claimed; [the frozen-release Editor endurance test](release-0.3.1-native-reset-validation.md) has its own scope. The historical scene-reset crash's root cause remains unproven. Monitor setup remains deferred.

The tested player identifies itself as `0.3.1-dev`, Unity `6000.3.16f1`, a dirty working-tree snapshot based on `44dad0b0`. Its prepared non-Editor C# fingerprint is `2b776e823986dc69d42de7c9acaf5e3aa2a210e6a5029ee15f1dad94fbc99b41` across 220 files; this fingerprint excludes packages, scenes and settings.
