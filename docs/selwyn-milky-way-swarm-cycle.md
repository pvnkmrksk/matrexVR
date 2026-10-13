# Selwyn Milky Way / ordered swarm cycle

The full-duration reference `Assets/StreamingAssets/Examples/Swarm/selwyn-milky-way-swarm-cycle.sequence.json` runs the protocol below in `Choice_Selwyn`. Your active `Assets/StreamingAssets/sequenceConfig.json` retains the quick-test edits made during implementation: the first two blocks and heading window are 3 seconds, with gain 2.5 on those rows. The later swarm blocks are 240 seconds each. To run the full protocol, copy the reference sequence over `Assets/StreamingAssets/sequenceConfig.json` before Play. Open `Assets/Scenes/ControlScene.unity`, press Play, fill the original metadata form, then press Save/Start. Automatic start is off. Your system config, tracking addresses, camera order, resolution and FPS settings are retained.

| Block | Seconds | Sky | Conspecifics | Auto-trim |
|---|---:|---|---|---|
| 1: Setup | 360 | Current Selwyn Milky Way | None | On |
| 2: Milky Way measurement | 240 | Current Selwyn Milky Way | None | Off |
| 3: Milky Way + swarm | 240 | Current Selwyn Milky Way | 150 Bogong moths per rig | Off |
| 4: Swarm only | 240 | Uniform gray RGB 0.12 | 150 Bogong moths per rig | Off |

Setup runs once. The first pass lasts 18 minutes; subsequently blocks 2–4 repeat every 12 minutes, with auto-trim staying off. `loopStartIndex: 1` (zero-based) returns to the Milky Way-only block. Index 1 means the second sequence row. Randomization is off because blocks 3 and 4 depend on block 2. `reloadScene: false` retains the same rigs across these blocks. Setup resets their positions/headings to `(0,100,0)` / north; later blocks retain pose. The calibrated yaw DC offset persists, including when auto-trim turns off. Flight variance gating and tuning are copied from the existing auto-trim calibration example. The dashboard can still control the current row.

All four experiments enable autopilot at **7 m/s**, with animal steering enabled and tracked translation disabled. Wind is zero and AGL is 100 m. Terrain scenes use metre world units: Unity +Z is north/forward, +X east, +Y up; positive yaw turns clockwise from north. This generated experiment does not read the recorded CSV or change its conversion.

The three stellar blocks use latitude −35.894409, longitude 148.453948, `Australia/Sydney`, `date: null`, `localTime: "now"`, and north yaw zero. UTC offset is resolved from that timezone, including daylight saving. The sky is regenerated on block entry and held between the configured 30-minute image updates. Exposure 0.1 and faint-detail cutoff 0.05 retain the existing generated moth recipe's settings. Block 4 replaces the Milky Way with an archived uniform gray image; the scene's authored terrain remains.

## Config files and hierarchy

The full-duration sequence contains timing, order, loop start index, scene name, yaw gain 1, reload flag, auto-trim enable flag and each experiment file reference. All flight, sky, measurement and swarm parameters live in the **individual experiment files**:

* `Examples/Swarm/selwyn-cycle-01-setup-full.example.json`
* `Examples/Swarm/selwyn-cycle-02-milky-way-full.example.json`
* `Examples/Swarm/selwyn-cycle-03-milky-way-swarm.example.json`
* `Examples/Swarm/selwyn-cycle-04-swarm-only.example.json`

The full-duration sequence is `Examples/Swarm/selwyn-milky-way-swarm-cycle.sequence.json`. These are complete flat Choice experiment configs, editable as JSON. The active quick-test sequence retains the original `selwyn-cycle-01-setup.example.json` and `selwyn-cycle-02-milky-way.example.json` names and your edits; their full-duration counterparts set the heading window to 240 seconds. Hardware remains in `system_config.json`. Existing generated and CSV examples remain available as separate recipes.

## Heading measured during block 2

The measurement block contains:

```json
"headingReference": {
  "enabled": true,
  "windowSeconds": 240,
  "startOffsetSeconds": 0,
  "measureDuringPresentation": true,
  "saveAs": "milky-way-baseline"
}
```

Each rig samples its own **virtual world yaw** after movement once per frame, and computes the circular mean over that 240-second window. A 359°/1° pair averages north, rather than 180°. This is a mean of unit heading vectors, not a straight average of angles or a displacement-weighted direction. Resultant length and sample count are recorded; low resultant length does not extend or reject a trial.

`measureDuringPresentation: true` runs observation and the sequence timer together. Block 2 therefore lasts 240 seconds, rather than an observation period followed by an additional 240-second presentation. The final observation completes before the sequence advances. Ordinary flat experiments retain their previous timing when this flag is omitted/false. In phased files, measurement remains in `preStimulus`, under the phase runner's timer.

Both swarm blocks contain:

```json
"useHeadingReference": true,
"headingReferenceId": "milky-way-baseline",
"mu": 0
```

Thus each rig's swarm mean is that animal's saved mean from block 2 plus zero degrees. It is frozen in the world through both swarm blocks, even while the animal steers. This does not continuously steer the swarm toward the animal or recompute the mean in block 3. References are held by the session controller, separately by rig and name; they survive in-place block changes and scene reloads. They are cleared at start, stop and each sequence loop, so a previous cycle cannot supply the next baseline. Each repeat starts a fresh 240-second Milky Way measurement, without returning to trimming. Missing named references fail explicitly. Core validation rejects shuffled or reordered sequences that present a named swarm before measuring it.

`headingReference.jsonl` records each rig's mean, resultant, sample count, observation times, trial/step, saved name and concurrent-measurement flag. Each swarm log header records its resolved world `Mu`, yaw/elevation kappa and sampler. The exact system, sequence and all four experiments are archived with the run; generated sky images and metadata are also saved.

## Ordered motion with independent speeds and wing phases

| Nested `swarm` parameter | Active value | Meaning |
|---|---:|---|
| `numberOfLocusts` | 150 | Moth count **per rig**; four rigs give 600 total. Legacy field name retained. |
| `trajectoryFile` | empty | Generated distribution; no recorded data input. |
| `locustSpeed` | 6.17 | Mean speed, m/s in this terrain scene. |
| `locustSpeedStdDev` | 1.24 | Speed standard deviation, m/s; not variance. |
| `directionDistribution` | `VonMises` | Corrected normalized circular sampler. |
| `kappa` | 1000 in block 3; 100 in block 4 | Yaw concentration around the frozen baseline; retains your later tuning. |
| `muElevation` | 0 | Mean horizontal motion; positive Unity pitch points downward. |
| `kappaElevation` | 100 | Elevation concentration. Combined order is about 99.45% with yaw kappa 1000, or 99% with yaw kappa 100. |
| `volumeHalfExtentMetres` | 2 | Moving cube ±2 m on all axes, side 4 m, volume 64 m³. |
| `spawnRelativeToAnimal`, `followAnimalPosition` | true | Cube centered on the current animal; mean direction stays frozen. |
| `preserveBoundaryOvershoot`, `wrapY` | true | Exact periodic wrapping on all three cube axes. |
| `agentVisual`, `bogongVisual.shape` | `Bogong`, `Moth` | Physical cigar body and one coupled wing pair. |
| `bodyLengthMetres`, `bodyDiameterMetres`, `wingSpanMetres` | 0.03, 0.01, 0.06 | Perspective body and wing dimensions. |
| `flapFrequencyHz` | 30 | GPU wing motion with independently randomized phases. |
| `sizeMode`, `subpixelVisibility` | `World`, true | Physical perspective; contrast attenuation below pixel resolution. |

Speeds are independent nonnegative normal samples taken once per agent; a rare negative draw is resampled. Headings/elevations are independently sampled once at spawn. Steady motion retains those directions and speeds. Periodic boundaries recirculate the finite population; no per-frame random direction changes or synchronized spawning/flapping are imposed. Observer motion is subtracted naturally by world-relative geometry: a moth at the observer's speed/direction remains near it, and faster/slower moths pass.

The historical direction sampler has a broad tail: kappa 1000 does **not** give its mathematically expected high concentration, while kappa ≥10000 forces exactly parallel motion. It is preserved for old configs using omitted/default `directionDistribution: "Legacy"`. The new cycle opts into `"VonMises"`, using the normalized rejection sampler. Your current block 3 tuning raises yaw kappa to 1000 (about 1.81° yaw SD); block 4 retains yaw kappa 100. Yaw kappa 100 has about 5.74° SD and resultant 0.995; adding independent elevation kappa 100 gives about **0.990 3D directional order**. This is an order parameter, not a guarantee that 99% of individuals have identical headings. Higher kappa narrows the spread without the legacy point-mass cutoff.

## Visible population and contrast

Six square 90° views cover directions around the animal; for a uniform cube centered on it, each face receives about one sixth of the population. **150 agents → about 25 geometric moth positions per view.** It is editable in both swarm block files. Density is 2.344 agents/m³. Natural counts fluctuate; the implementation does not insert/remove moths to clamp each view to a count or force near passes. Turning changes which moths are visible, while the cube stays centered.

In the deterministic two-minute test at observer speed 7 m/s, the six view means ranged 24.1–25.8. About 77.5% of camera frames contained 20–30 positions. A natural closest approach was 2.74 cm. These are one realization's statistics, not guaranteed rates.

Geometric view counts do not mean 25 equally clear silhouettes on a 64×64 panel. Far moths still fade with projected coverage, and visibility depends on body orientation, pixel placement and sky contrast. The earlier native physical-moth test reached half full contrast between sampled distances 1 and 1.5 m; the cube retains the requested minimum 2 m half extent. There is no hard radial cutoff at 2 m; cube corners are 3.46 m away. Close 10 cm wing views and far fading use the existing rendering fixes, without enlarging moths or adding wing pairs. See [generated moth rendering](generated-bogong-swarm.md).

## Validation and performance

`Assets/Editor/SelwynSwarmCycleValidation.cs` runs in the marked disposable validation project. It validates the full-duration reference files and tests the original ControlScene Save/Start button, in-place transitions, auto-trim flags, retained rigs/DC offsets, circular per-rig means, frozen reuse after steering, sky modes, cycle reset, misordered/shuffled references, exact coordinate recording and population statistics. The observation window is shortened only in the disposable copy; sequence durations and runtime assertions use the full-duration reference values, without modifying the main checkout's active quick-test configuration. This is an accelerated transition test, not a complete live hardware run.

The normalized distribution test samples 100,000 independent yaw/elevation pairs. The recording benchmark exercises four 150-agent swarms and 24 actual 64×64 cameras, including pooled capture and asynchronous gzip output. It verifies every final coordinate against its presented transform and requires no dropped frames, pending backlog or steady main-thread managed allocation. Results and machine details are in [selwyn-swarm-cycle-validation-results.json](selwyn-swarm-cycle-validation-results.json). Timings cover CPU movement/upload/capture/render submission; they exclude terrain rendering, GPU completion, tracking and physical panels, so they do not guarantee live 120 FPS. The existing system target remains 120 FPS.

These features are on `codex/bogong-night-sky`, using the original metadata form and existing status overlay. The experimental configuration GUI stays on its separate UI branch.

On the tested non-GUI branch, the 600-moth/24-camera recording benchmark submitted 170,400 rows without drops. Median CPU movement/upload/capture/render submission was 3.01 ms; pooled coordinate capture was 0.158 ms. Native renderer tests passed 100,319 checks; the legacy five-scene phase suite passed 85,479, and recorded trajectory/continuation passed 99.
