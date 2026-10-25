# Tracker transport assemblies

Unmodified official NuGet binaries in Assets/Plugins. Exact versions, target frameworks, source URLs and SHA-256 hashes are in Assets/Plugins/tracker-transport-manifest.json.

- NetMQ4.0.1.13: LGPLv3; source at https://github.com/zeromq/netmq/tree/41a940c5893413ecda04f30af771deb4accaf251 . Retained COPYING.LESSER and upstream README license statement.
- AsyncIO0.1.69: MPL2.0; source at https://github.com/somdoron/AsyncIO .
- NaCl.Net0.1.13: MPL2.0; source at https://github.com/somdoron/NaCl.net .

The net47 NetMQ assembly and net40 AsyncIO use Unity's managed framework. NaCl uses netstandard2.1 without additional NuGet dependencies. Unity supplies System.Threading.Tasks.Extensions and other standard framework facades. The binaries remain separate replaceable DLLs; they are not merged into application code. This update fixes the old NetMQ4.0.0.1 handshake with the current native HejjéTrac publisher. It does not alter tracker numeric output.

Verified in Unity6000.3.16f1 on macOS arm64:100 JSON +300 real HejjéTrac sample-video rows,150 listener teardown cycles, worker count returns0, OSthread count stays bounded. See docs/tracker-native-validation-results.json. Windows/Linux and physical camera validation are separate.
