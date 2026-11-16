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

The see-through test build enables `seamlessPeriodicView`. Each rig can see bodies across the arena edges at the existing 80 cm viewing range. This can show the same body in more than one direction. The experiment log still counts 135 bodies per rig in one square and records each body's position once. The overhead view shows one square.

Random removal gives each simulated locust the same chance of being removed regardless of its position or heading. Uniformly distributed simulated positions remain uniform on average after removal. Local density also depends on where peers move and how their copies are placed.

## Tracking

The operator can start and stop the trial. The experiment clock advances the phase schedule and ends the run independently of tracker availability. During a tracking gap, the corresponding avatar and copies hold their last pose under Kannadi’s existing tracking behavior.
