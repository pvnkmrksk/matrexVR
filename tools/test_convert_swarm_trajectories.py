import csv
import tempfile
import unittest
from pathlib import Path
from convert_swarm_trajectories import convert


class ConversionTests(unittest.TestCase):
    def test_axes_height_sort_and_wall_time(self):
        with tempfile.TemporaryDirectory() as directory:
            source, output = Path(directory)/"input.csv", Path(directory)/"out.csv"
            source.write_text("Frame,ID,X,Y,Z,TimeExact\n2,7,-1000,-2000,10,2026-10-07 19:00:00.010\n1,7,0,0,0,2026-10-07 19:00:00.000\n3,8,nan,0,0,2026-10-07 19:00:00.020\n")
            report = convert(source, output)
            self.assertNotIn(b"\r", output.read_bytes())
            rows = list(csv.DictReader(output.read_text().splitlines()))
            self.assertEqual(list(rows[0]), ["ID", "X", "Y", "Z", "time"])
            self.assertEqual([float(rows[0][k]) for k in ("X","Y","Z","time")], [0,5,0,0])
            self.assertEqual([float(rows[1][k]) for k in ("X","Y","Z","time")], [2,5.01,-1,.01])
            self.assertTrue(report["startLocal"].endswith("+11:00"))
            self.assertEqual(len(report["rejectedRows"]), 1)
            report = convert(source, output, yaw_degrees=-90)
            self.assertAlmostEqual(float(list(csv.DictReader(output.read_text().splitlines()))[1]["X"]), 1)

    def test_duplicates_do_not_silently_merge_tracks(self):
        with tempfile.TemporaryDirectory() as directory:
            source, output = Path(directory)/"input.csv", Path(directory)/"out.csv"
            source.write_text("Frame,ID,X,Y,Z,TimeExact\n1,1,0,0,0,2026-10-07 19:00:00\n1,1,1,1,1,2026-10-07 19:00:00\n")
            with self.assertRaises(ValueError):
                convert(source, output)
            self.assertEqual(convert(source, output, duplicate_policy="first")["duplicates"], 1)


if __name__ == "__main__":
    unittest.main()
