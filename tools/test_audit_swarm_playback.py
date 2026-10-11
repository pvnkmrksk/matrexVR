import tempfile
import unittest
from pathlib import Path
from audit_swarm_playback import audit


class SwarmAuditTests(unittest.TestCase):
    def test_units_and_explicit_speed_are_separate_from_measurements(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "tracks.csv"
            path.write_text("ID,X,Y,Z,time\nA,0,5,2,0\nA,0,5,0,1\nB,2,5,2,2\nB,2,5,0,3\n")
            config = {"playback": {"maxGapSeconds": 2, "continuationSpeedMetresPerSecond": 6}}
            result = audit(path, config)
            self.assertEqual((result["ids"], result["uninterruptedSegments"], result["birthsPerMinute"]), (2, 2, 40))
            self.assertEqual(result["recordedSpeedMetresPerSecond"]["median"], 2)
            self.assertEqual(result["measuredTerminalSpeedMetresPerSecond"]["median"], 2)
            self.assertEqual(result["configuredContinuation"]["continuationSpeedMetresPerSecond"], 6)
            path.write_text("ID,X,Y,Z,time\nA,0,500,200,0\nA,0,500,0,1\nB,200,500,200,2\nB,200,500,0,3\n")
            config["playback"]["dataUnitsPerMetre"] = 100
            centimetres = audit(path, config)
            for key in ("sampleCentroidMetres", "recordedSpeedMetresPerSecond", "measuredContinuation", "configuredContinuation"):
                self.assertEqual(result[key], centimetres[key])

    def test_missing_data_and_unsupported_motion_are_reported(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "tracks.csv"
            path.write_text("ID,X,Y,Z,time\nA,0,5,0,0\nA,1,5,0,.1\nA,2,5,0,1\nA,3,5,0,1.1\n")
            config = {"playback": {"maxGapSeconds": .2, "centerOnAnimal": False}}
            result = audit(path, config)
            self.assertEqual(result["uninterruptedSegments"], 2)
            self.assertAlmostEqual(result["meanSimultaneouslyObserved"], .2 / 1.1)
            self.assertFalse(result["closestPassModelSupported"])
            self.assertIsNone(result["configuredContinuation"])

    def test_incoming_extension_is_reported_without_altering_observations(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "tracks.csv"
            source = "ID,X,Y,Z,time\nA,2,0,0,0\nA,3,0,0,1\nB,4,0,0,5\nB,5,0,0,6\n"
            path.write_text(source)
            config = {"playback": {"maxGapSeconds": 2, "continuationSpeedMetresPerSecond": 2, "preRollSeconds": 2}}
            result = audit(path, config, observer_speed=0)
            without = result["configuredContinuation"]["passesWithinMetres"]["0.1"]["count"]
            with_lead = result["configuredIncomingAndOutgoing"]["passesWithinMetres"]["0.1"]["count"]
            self.assertEqual((without, with_lead), (1, 2))
            self.assertEqual(result["recordedSpeedMetresPerSecond"]["median"], 1)
            self.assertEqual(path.read_text(), source)


if __name__ == "__main__":
    unittest.main()
