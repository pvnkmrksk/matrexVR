# Repeated resets and scene lifecycle

The historical workaround avoids scene reloads by advancing an experiment within its existing scene. That path remains useful: same-scene blocks with `reloadScene: false` use `IInSceneSequencer.AdvanceStep`, including Dynamic Choice/Choice_desync, Swarm, Kannadi and the terrain lab. Changing to another scene, or explicitly setting `reloadScene: true`, still loads a scene normally.

Asynchronous scene loading alone does not release objects or native handles retained by a previous trial. The 0.3 work addresses concrete resource ownership problems found while auditing repeated transitions:

- Generated material instances now have an owner that destroys them when their trial object is destroyed, including in-place resets.
- Optomotor's generated mesh, material and texture are released on destruction.
- Asynchronous swarm writers dispose their wake-up handle after draining. Submission/completion synchronize with disposal so a late callback cannot signal an already disposed handle.
- Tracking listeners stop when disabled as well as destroyed, and restart on re-enable. A worker still stopping remains owned rather than being replaced by another live socket/thread.

The workers still own their sockets and file writers on their own threads. Clean shutdown drains recorded frames before archiving. The runtime exposes active worker/writer/material/resource counters for targeted stress validation; these counters identify owned resources rather than total Unity process memory.

There was no original Unity crash dump establishing a single root cause. These changes fix identified leaks and lifecycle races; they do not establish that every crash after 50–100 reloads has been eliminated. Physical GPU/driver behavior, acquisition timing, different scenes and long sessions require testing on the actual workstation.

The [Unity report](lifecycle-replay-validation-results.json) passed **1,293 checks**, including 150 actual MainController Choice↔Swarm scene loads at accelerated 0.1-second block duration with four local SUB listeners and two generated agents, plus 150 native ColorDrift/material/grating/socket teardown cycles. Its scene-load samples record the tracked owned-resource counters after teardown. These are accelerated real scene loads, not a 30-second hardware acquisition soak.

For reproducible acquisition, use explicit same-scene reset settings and save the sequence/config graph with each run. Compare regular scene reloads and in-place transitions under the same workload, record resource counters/process memory, and retain Unity's Player log and any crash dump. A shortened synthetic transition test is useful evidence of cleanup; it cannot substitute for a full-duration hardware run.

See [release verification](releases.md), [experiment workflow](experiment-workflow.md) and [saved-run replay](replay.md).
