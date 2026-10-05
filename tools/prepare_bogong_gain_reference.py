#!/usr/bin/env python3
"""Install the immutable historical gain oracle into a disposable Unity test copy."""
from pathlib import Path
import hashlib
import subprocess
import sys

revision = 'ef8a68b2ad2ff690e803007511666296a31eda5a'
expected = '84312430379fd7ffa0cdbe56b19297a4999fd7d70198c75ae68f44cfc8b70286'
root = Path(__file__).resolve().parents[1]
target = Path(sys.argv[1]).resolve()
assert (target / 'KANNADI_VALIDATION_COPY').is_file(), 'Disposable validation copy required'
assert target != root, 'Do not install the reference implementation in the working project'
raw = subprocess.check_output(['git', 'show', f'{revision}:Assets/Scripts/ClosedLoop.cs'], cwd=root)
assert hashlib.sha256(raw).hexdigest() == expected, 'Historical source changed'
folder = target / 'Assets/BogongHistoricalValidationOnly'
folder.mkdir(parents=True, exist_ok=True)
# The numerical code is unchanged. Supply its clock and I/O from the regression runner.
code = raw.decode().replace('Time.deltaTime', 'ReferenceClock.deltaTime')
(folder / 'ClosedLoop.cs').write_text('#if UNITY_EDITOR\nnamespace BogongHistorical {\n' + code + '\n}\n#endif\n')
(folder / 'ReferenceAdapters.cs').write_text('''#if UNITY_EDITOR
using UnityEngine;
namespace BogongHistorical {
    public static class ReferenceClock { public static float deltaTime; }
    public class ZmqListener : MonoBehaviour {
        public Vector3 position, rawRotation;
        public Quaternion quaternion;
    }
    public class MainController : MonoBehaviour {
        public SystemConfig GetSystemConfigForGameObject(GameObject value) => new SystemConfig();
        public void RegisterVRClosedLoop(string id, ClosedLoop value) { }
        public void UnregisterVRClosedLoop(string id) { }
    }
}
#endif
''')
print(f'Installed historical oracle from {revision}; SHA-256 {expected}')
