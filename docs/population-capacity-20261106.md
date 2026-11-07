# Native locust capacity on the Linux LED workstation, 2026-11-06

The optimized build supports **16 VRs × 64 original animated locusts at approximately 60 FPS with complete recording**. The 16-VR operating edge is around 72–80 locusts per VR; 88–96 falls below the target. Counts here are per VR: 64 × 16 is 1,024 total, while 1,024 × 16 is 16,384 total.

This report extends the [recording fix and fidelity validation](population-performance-20261106.md). The [structured capacity results](population-capacity-20261106-results.json) preserve all 32 cases, 43 measurement phases, CPU samples, ending queues, recording drops and exact build identity. Raw results, complete commands and run archives are retained in `Documents/ChatGPT/MatrexPerformance-20261105/locust-optimization/capacity-20261106` on the workstation.

## CPU usage and what the fix does

During a live three-second sample of 16 × 64, Unity used **24.8% of the machine's 32 logical CPUs**, the entire system used **31.9%**, and Unity's main thread used **72.4% of one logical CPU**. Linux tools that count 100% as one logical CPU show the Unity process as approximately 793%. The setup screen before the sweep used 4.3% for Unity and 10.4% for the whole system. These are different workload states.

A thread is one sequence of instructions. Unity's main thread coordinates the scene and the work needed to produce a frame. At 60 FPS, a frame gets 16.67 ms. Before the fix, the main path took about 22.26 ms even though many CPU slots were available; population capture spent 10.63 ms converting animation state into text. The measured original workload used approximately 19.5% of the whole machine for Unity, but only produced 44.55 FPS. Spare average CPU capacity could not shorten that serial part automatically.

The recording files were already written and compressed on background threads. The missing piece was animation JSON formatting, which still happened on the main thread before handing the data over. JSON is text describing the data, for example `{"time":0.47,"speed":1.0}`.

The fix makes three concrete changes:

1. The main thread reads the actual animation and pose values, then copies the animation numbers and arrays into an independent snapshot. It does not hand a live Unity object to a worker.
2. One existing population worker per VR formats the snapshot into the same CSV/JSON file format, compresses it and writes it. The snapshot stays unchanged until that worker finishes, so subsequent motion cannot corrupt an earlier recorded frame.
3. Snapshot buffers and number-formatting storage are reused. The ordinary original-locust animation format no longer creates a serializer or JSON string for each animal and frame; richer animation states retain the general serializer. This reduces discarded temporary objects and the cleanup pauses caused by garbage collection. The first asynchronous candidate reached about 57 FPS; removing these common allocations brought the workload to approximately 60.

Population capture now takes about **0.60 ms**, and the main path about **12.1–12.3 ms**. Original geometry, independent motion, animation phases, timestamps and recording frames are retained. The previous 443 snapshot checks, 371 animation/replay checks and 694 archive/writer checks cover that change. This sweep used the same binary throughout; it adds measurements rather than a new production optimization.

The process CPU percentage can rise after this fix because the application now produces more frames per second and uses workers concurrently. Main-thread elapsed time, summed CPU usage across workers and GPU elapsed time are different quantities; CPU and GPU work overlap, so their times must not be added as sequential costs.

## Sixteen VRs, 64 cameras

Original native model, four RBLF faces per VR, each 128 × 128 pixels. The software cap is 60 FPS with VSync off. No model simplification, shared animation clones, reduced count, reduced resolution or disabled recording was used.

| Locusts per VR | Total locusts | FPS | Main path mean | Unity CPU, whole-machine scale |
|---:|---:|---:|---:|---:|
|64|1,024|59.99|12.29 ms|24.7%|
|72|1,152|59.79–59.85|13.34–13.38 ms|26.5%|
|80|1,280|59.63|14.44 ms|28.9%|
|88|1,408|58.70–58.78|15.69–15.80 ms|30.4–30.5%|
|96|1,536|58.09|16.68 ms|32.4%|
|128|2,048|45.86|21.72 ms|31.8%|
|256|4,096|23.28|42.83 ms|29.5%|
|512|8,192|10.62|93.80 ms|28.5%|
|1,024|16,384|3.98|251.07 ms|32.5%|

All these 16-VR cases completed their recordings with zero dropped population frames and zero ending backlog. A low FPS is still a failed 60-FPS operating point even when the lower-rate recording is complete. At 128 per VR, the main path exceeds the frame budget while mean GPU time is about 12.4 ms. At 1,024 per VR, both main-path and GPU times exceed the budget; mean GPU time is about 122 ms.

The main path still contains the cost of individual Unity objects, native animation and preparing many camera views. The original model is only 310 triangles, but each animal still has its original Animator and SkinnedMeshRenderer. Low image resolution does not remove those object, animation and camera costs. The earlier recording fix leaves that rendering architecture intact.

Average main time can remain below 16.67 ms while some frames exceed it. For example, 88 per VR has a 15.7 ms mean but 18.7–18.9 ms main-path p95. That is why the operating edge appears before the mean reaches the full frame budget.

## Fewer VRs and recording limits

The following are measured operating ranges, not exact universal integer maxima. The lower point in each row has more room for variation.

| VRs | Locusts per VR near 60 FPS with recording | Next observed limitation |
|---:|---|---|
|16|64 with margin;72 about 59.8;80 near the edge|88 about 58.7 FPS|
|8|192 about 60;224–240 about 59.6–59.8|256 about 59.0 FPS|
|4|512 about 59.94|576 about 59.3;640 about 56.5–57.5|
|2|1,024 about 59.98|1,280 about 58.4–58.7|
|1|1,536 about 59.98 with no growing queue|1,792 queues recording data;2,048 drops recording frames|

Fewer VRs help, but capacity is not simply inversely proportional to VR count. With the same 1,024 total animals, main-path time was 12.3 ms at 16 VRs, 8.5 ms at 8 VRs, 6.7 ms at four VRs, 5.8 ms at two VRs and 5.2 ms at one VR. Reducing VR count also reduces camera and rig work.

There is a second limit: each VR has one population recording worker. A single VR with 2,048 animals and two VRs with 1,024 each have the same total count, but the former sends all recording work through one worker while the latter has two.

The single-VR checks show why displayed FPS alone is insufficient:

- **1,536:** two 20-second measured intervals, 59.98 FPS, ending pending frames 0 then 1, zero dropped recording frames.
- **1,792:** two 12-second measured intervals, 59.96–59.97 FPS, ending queue 96 then 196. No frames were lost during this finite test, but the persistent growth is unsustainable for a longer trial.
- **2,048:** two 12-second measured intervals, 59.97 then59.66 FPS, ending queue 205 then 239, and 165 dropped recording frames. The bounded queue has a 240-frame capacity per writer. This is a failed recording operating point despite near-60 rendering.

## Conditions, evidence and use

The machine is an i9-13900K with 32 logical CPUs and an RTX 4070, running Ubuntu 22.04.5 / Linux 6.8, NVIDIA driver 580.178.04 and Unity 6000.3.16f1. The build is `0.4.2-native64-noalloc`, based on main `5fbfc05` with the local recording fix; its runtime source fingerprint is `4e00cde2f216045a6f33a5cc39bf26baa19144f27ad690b2829a21505287e6bd`.

NOVA was the single 1920×1080 at 60 Hz display, selected as display 0. World units were centimetres, ball diameter 10 cm, the same swarm volume and original visuals were used, and external native trackers remained active. For 16 VRs the configured IDs are VR1–16; smaller tests use connected VR9 upward. This keeps tracking available while reducing rendered rigs.

Coarse samples measured eight seconds after startup/warmup. Most boundary points used two 12-second intervals; 16×64 and 1×1,536 also used two 20-second intervals. Finalization drained and checked the population writers before the next process ran. Each scenario verified the actual population, rig and camera count. The intentional 2,048-agent recording-overload case retains its errors and dropped-frame evidence rather than being reported as successful.

All coarse and boundary measurements reported keyboard focus. The additional 40-second 16×64 CPU/throughput run lost keyboard focus while the session remained unlocked and rendering continued. That extra run is a workload/CPU measurement, not independent proof of physical presentation timing. Its approximately 19 ms frame-interval p99 also makes clear that “60 FPS” is an average, not a guarantee of perfectly uniform 16.67 ms display scanout. No hour-long capacity or photodiode timing claim is made.

The default operator launchers remain at 64 original locusts per VR. To repeat a capacity case, use the isolated harness with a fresh tag after closing the owned setup:

```sh
python3 /home/flyvr01/Documents/ChatGPT/MatrexPerformance-20261105/locust-optimization/capacity-20261106/capacity.py stop-ready
python3 /home/flyvr01/Documents/ChatGPT/MatrexPerformance-20261105/locust-optimization/capacity-20261106/capacity.py run --rigs 8 --agents 192 --seconds 20 --repeat --tag my-repeat
```

The harness uses separate config and data directories, refuses concurrent owned benchmark players and verifies that the requested counts actually spawned. Original operator calibration, downloaded release, existing recordings and external native trackers were not replaced.
