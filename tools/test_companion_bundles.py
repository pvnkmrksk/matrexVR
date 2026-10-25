#!/usr/bin/env python3
"""Packaging checks use synthetic archives; no cameras, downloads or app execution."""
import importlib.util
import io
from unittest.mock import patch
import hashlib
import json
from pathlib import Path
import stat
import tempfile
import unittest
import zipfile

spec=importlib.util.spec_from_file_location('companion_bundles',Path(__file__).with_name('companion_bundles.py'))
bundles=importlib.util.module_from_spec(spec);spec.loader.exec_module(bundles)

class BundlesTest(unittest.TestCase):
 def test_rejects_archive_traversal(self):
  with tempfile.TemporaryDirectory() as tmp:
   for name in ('../outside','/outside','C:/outside','a\\outside'):
    with self.subTest(name=name):
     with self.assertRaises(ValueError):bundles.safe_destination(Path(tmp),name)
 def test_rejects_symlink_outside_package(self):
  with tempfile.TemporaryDirectory() as tmp:
   root=Path(tmp);archive=root/'bad.zip'
   with zipfile.ZipFile(archive,'w') as z:
    item=zipfile.ZipInfo('link');item.create_system=3;item.external_attr=(stat.S_IFLNK|0o777)<<16;z.writestr(item,'../../outside')
   with self.assertRaises(ValueError):bundles.extract(archive,root/'out')
 def test_checksum_mismatch_does_not_accept_cached_download(self):
  with tempfile.TemporaryDirectory() as tmp:
   cache=Path(tmp);asset={'sha256':'0'*64,'asset':'tracker.zip','url':'invalid://fixture'}
   (cache/('0'*64+'-tracker.zip')).write_bytes(b'corrupt')
   with patch.object(bundles,'urlopen',return_value=io.BytesIO(b'corrupt-download')):
    with self.assertRaises(ValueError):bundles.download(asset,cache)
 def test_stage_offline_complete_package_and_signing_provenance(self):
  with tempfile.TemporaryDirectory() as tmp:
   root=Path(tmp);cache=root/'cache';cache.mkdir();archive=root/'fixture.zip'
   with zipfile.ZipFile(archive,'w') as z:
    for name,data in {'Pkg/Rekkefly.app/Contents/MacOS/rekkefly':b'native fixture','Pkg/Rekkefly.app/Contents/Resources/configs/demo.toml':b'kind="demo"','Pkg/Rekkefly.app/Contents/Resources/configs/camera-a.toml':b'kind="camera"','Pkg/LICENSE':b'verbatim license'}.items():
     item=zipfile.ZipInfo(name);item.create_system=3;item.external_attr=(stat.S_IFREG|0o755)<<16;z.writestr(item,data)
   sha=bundles.digest(archive);asset={'asset':'fixture.zip','sha256':sha,'url':'invalid://must-not-download','sizeBytes':archive.stat().st_size}
   archive.rename(cache/(sha+'-fixture.zip'))
   lock={'schemaVersion':1,'trackers':{'rekkefly':{'releaseTag':'v1.0.0','repository':'https://example.invalid/repo','releaseUrl':'https://example.invalid/release','platforms':{'macos-arm64':asset}}}}
   out=root/'out';catalog=bundles.stage(['macos-arm64'],out,cache,lock)
   entry=catalog['entries'][0];exe=out/entry['packageRoot']/entry['executable']
   self.assertEqual(exe.read_bytes(),b'native fixture');self.assertTrue(exe.stat().st_mode&stat.S_IXUSR)
   self.assertEqual(json.loads((out/'companions.lock.json').read_text()),lock)
   self.assertEqual((out/entry['packageRoot']/'Pkg/LICENSE').read_bytes(),b'verbatim license')
   with self.assertRaises(FileExistsError):bundles.stage(['macos-arm64'],out,cache,lock)
   old=entry['executableSha256'];exe.write_bytes(b'explicitly signed native fixture')
   bundles.seal_catalog(out);sealed=json.loads((out/'catalog.json').read_text())['entries'][0]
   self.assertEqual(sealed['upstreamExecutableSha256'],old);self.assertNotEqual(sealed['executableSha256'],old);self.assertEqual(sealed['archiveSha256'],sha)

if __name__=='__main__':unittest.main()
