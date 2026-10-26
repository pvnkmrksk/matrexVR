# Frozen 0.3.1 source: 300 native-tracker scene reloads

**Passed:** 300 full `Choice_desync` reloads on frozen source
`72baaf2f22cd2b89451afd6cb1e8026c87375f3a`, using Unity 6000.3.16f1,
Metal and an Apple M2 Pro. Every reload had a different scene handle.

The native Rekkefly process ran explicit generated demo input continuously on an
isolated port. Four Unity listeners were fresh and progressing in all **2,400
input observations** across 600 active samples. All **24 requested camera faces**
were rendered at every active sample. Rekkefly produced 83,881 frames at
119.73 frames/s, with zero reported ZMQ/UDP drops. Unity and the tracker exited 0.

| Check | Observed result |
| --- | --- |
| Experiment runtime failures / new crash reports | 0 / 0 |
| Startup Editor exception | 1 `UnityEditor.Search.SearchDatabase` indexing exception; retained with stack trace in the JSON |
| Actual scene duration | 2.015–2.356 s; median 2.208 s for a configured 2 s |
| RSS after 20 warmup loads | 802,881,536–1,631,764,480 bytes; last 839,155,712 bytes |
| Live file descriptors after warmup | 111–112 |
| Live nonpersistent materials / meshes / textures / render textures | Constant at 45 / 1 / 96 / 28 |
| Scene-owned tracking workers / trial materials / diagnostic targets | 4 / 8 / 24 while active; all 0 after teardown, before forced GC |

All 126 production C# files matched the frozen revision before launch and
remained unchanged. Their aggregate SHA-256 is
`8aa939f7c0f1fb69898d28611ff93f09c16feff574b899e6a1f10d91cf4e382a`.
The native executable SHA-256 is
`631ce45f37bf9af9969abad1e00997d2b8b208ccffb2236c7ea1e8cd771bb644`.

This is an Editor rendering test with generated input. Physical cameras, animals,
LED scanout, standalone players and asynchronous Swarm population writing were
outside its scope. The result does not identify the historical crash cause.
No public release archive was changed.

The [machine-readable report](release-0.3.1-native-reset-validation-results.json)
contains exact fingerprints, counter ranges, teardown evidence and the retained
Editor exception. Raw local evidence is in
`.release-work/scene-reload-diagnostics/release031-72baaf2f-native-rekkefly-300x2s/`.
