# Kannadi shared swarm and multiplayer experiment

Sixteen real locusts participate from the start. Each sees the other 15 locusts as peer avatars, with its own avatar and copies hidden. All rigs share one fixed arena and one simulated swarm. A simulated locust has the same world position for every observer and follows the ordered entrainment motion.

## Starting positions

Sample a random position for each of the 16 rigs in the shared arena at the start of each trial. Use the existing randomized cardinal headings.

## Schedule

Every five minutes, remove the next 30 simulated locusts from a seeded random order drawn at the start of the trial. Add two copies of each peer in the same update. The surviving simulated locusts and existing peer bodies continue from their current poses.

| Minutes | Simulated locusts | Peer avatars and copies per rig | Extra copies per peer | Bodies per rig |
| --- | ---: | ---: | ---: | ---: |
| 0–5 | 120 | 15 | 0 | 135 |
| 5–10 | 90 | 45 | 2 | 135 |
| 10–15 | 60 | 75 | 4 | 135 |
| 15–20 | 30 | 105 | 6 | 135 |
| 20–25 | 0 | 135 | 8 | 135 |

## Avatar copy placement

Use the same four pairs of offsets for each peer. The offsets are in shared arena coordinates, in centimetres. Add one copy at the listed offset and one at its opposite. Each copy follows the peer’s motion and heading at its fixed offset, with its position wrapped at the arena borders.

| Minute added | First copy `(x, z)` | Opposite copy `(x, z)` |
| ---: | ---: | ---: |
| 5 | `(25, 16)` | `(-25, -16)` |
| 10 | `(-16, 25)` | `(16, -25)` |
| 15 | `(6, 33)` | `(-6, -33)` |
| 20 | `(-33, 6)` | `(33, -6)` |

The first two pairs form a cross. The last two pairs form a second cross turned about 45 degrees. Each step adds copies on opposite sides of the peer, and the full layout places all eight copies about 30 to 34 cm from it before wrapping.

## Arena and density

The arena is a 1 m × 1 m square. Tracked and simulated bodies wrap at the edges.

The sandy-ground build uses a muted brown material in Kannadi. It retains the original ground texture's spatial pattern and 100 cm tile span, converts its colour to luminance, and softens its contrast to 55%. The ground remains flat and unlit. This is a built-in scene appearance, so use the sandy-ground player for new runs and retain the original player for its recorded sessions.

The see-through test build enables `seamlessPeriodicView`. Each rig can see bodies across the arena edges at the existing 80 cm viewing range. This can show the same body in more than one direction. The experiment log still counts 135 bodies per rig in one square and records each body's position once. The overhead view shows one square.

Random removal gives each simulated locust the same chance of being removed regardless of its position or heading. Uniformly distributed simulated positions remain uniform on average after removal. Local density also depends on where peers move and how their copies are placed.

## Tracking

The operator can start and stop the trial. The experiment clock advances the phase schedule and ends the run independently of tracker availability. By default, the avatar and copies hold their last pose during a tracking gap.

Set `"hideInactiveAnimals": true` in the Kannadi experiment JSON to hide each tracked animal and its copies until its first pose arrives and whenever the latest pose is more than one second old. They reappear together when fresh tracking resumes. A stationary animal stays visible while its tracker sends poses. The generated swarm still follows the scheduled counts, so the number of visible bodies can fall below 135 while any peer is hidden.

Set `"hideStationaryAnimals": true` to use walking speed for visibility. The avatar, its copies, and their walking animation share one speed estimate over the last 0.30 seconds of horizontal motion. The avatar and copies appear after one continuous second above `animationSpeedThreshold` and hide after one continuous second below 80% of that speed. With the example’s settings, those thresholds are 0.5 cm/s and 0.4 cm/s. Between them, visibility holds and any pending change resets. The view across the arena boundaries follows the same visibility rule.
