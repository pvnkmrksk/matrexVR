#!/usr/bin/env python3
"""Pin, verify and stage native tracker packages; never download code at app runtime."""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import tarfile
import tempfile
from urllib.request import Request, urlopen
import zipfile

ROOT = Path(__file__).resolve().parents[1]
LOCK = ROOT / 'Assets/StreamingAssets/Companions/companions.lock.json'
REPOSITORIES = {'rekkefly': 'pvnkmrksk/rekkefly', 'hejjetrac': 'pvnkmrksk/hejjetrak'}
PATTERNS = {
 'rekkefly': {'macos-arm64': r'Rekkefly-[0-9.]+-macos-arm64.zip', 'macos-x64': r'Rekkefly-[0-9.]+-macos-x64.zip', 'windows-x64': r'Rekkefly-[0-9.]+-windows-x64.zip', 'linux-x64': r'Rekkefly-[0-9.]+-linux-x64.zip'},
 'hejjetrac': {'macos-arm64': r'hejjetrac-macos-arm64.zip', 'macos-x64': r'hejjetrac-macos-x86_64.zip', 'windows-x64': r'hejjetrac-windows-amd64.zip', 'linux-x64': r'hejjetrac-linux-x86_64.tar.gz'},
}

def digest(path):
 h=hashlib.sha256()
 with Path(path).open('rb') as handle:
  for block in iter(lambda:handle.read(1024*1024),b''):h.update(block)
 return h.hexdigest()

def refresh_lock():
 result={'schemaVersion':1,'selection':'Latest stable release at explicit lock refresh; runtime never updates/downloads itself','trackers':{}}
 for backend,repo in REPOSITORIES.items():
  release=json.loads(subprocess.check_output(['gh','api',f'repos/{repo}/releases/latest']))
  selected={}
  for platform,pattern in PATTERNS[backend].items():
   assets=[a for a in release['assets'] if re.fullmatch(pattern,a['name'])]
   if len(assets)!=1:raise ValueError(f'{repo} {release["tag_name"]}: expected one {platform} archive, found {len(assets)}')
   asset=assets[0];sha=asset.get('digest','')
   if not re.fullmatch(r'sha256:[0-9a-f]{64}',sha):raise ValueError('Release must supply an authoritative SHA-256: '+asset['name'])
   selected[platform]={'asset':asset['name'],'url':asset['browser_download_url'],'sha256':sha[7:],'sizeBytes':asset['size']}
  result['trackers'][backend]={'repository':'https://github.com/'+repo,'releaseTag':release['tag_name'],'releaseUrl':release['html_url'],'platforms':selected}
 LOCK.parent.mkdir(parents=True,exist_ok=True)
 LOCK.write_text(json.dumps(result,indent=2)+'\n')
 return result

def safe_destination(root,name):
 pure=PurePosixPath(name)
 if pure.is_absolute() or '..' in pure.parts or '\\' in name or re.match(r'^[A-Za-z]:',name):raise ValueError('Unsafe archive path: '+name)
 return root.joinpath(*pure.parts)

def extract(archive,root):
 root.mkdir(parents=True,exist_ok=True)
 if str(archive).endswith('.zip'):
  with zipfile.ZipFile(archive) as z:
   for info in z.infolist():
    if info.filename.startswith('__MACOSX/'):continue
    target=safe_destination(root,info.filename);mode=info.external_attr>>16
    if info.is_dir():target.mkdir(parents=True,exist_ok=True);continue
    target.parent.mkdir(parents=True,exist_ok=True)
    data=z.read(info)
    if stat.S_ISLNK(mode):
     link=data.decode();resolved=(target.parent/link).resolve()
     if not resolved.is_relative_to(root.resolve()):raise ValueError('Archive symlink leaves package: '+info.filename)
     target.symlink_to(link)
    else:
     target.write_bytes(data)
     if mode:target.chmod(stat.S_IMODE(mode))
 else:
  with tarfile.open(archive,'r:gz') as tar:
   for info in tar.getmembers():
    target=safe_destination(root,info.name)
    if info.isdir():target.mkdir(parents=True,exist_ok=True);continue
    target.parent.mkdir(parents=True,exist_ok=True)
    if info.issym():
     if not (target.parent/info.linkname).resolve().is_relative_to(root.resolve()):raise ValueError('Archive symlink leaves package')
     target.symlink_to(info.linkname)
    elif info.isfile():
     with tar.extractfile(info) as source,target.open('wb') as out:shutil.copyfileobj(source,out)
     target.chmod(info.mode)
    else:raise ValueError('Unsupported archive entry '+info.name)

def download(asset,cache):
 cache.mkdir(parents=True,exist_ok=True)
 archive=cache/(asset['sha256']+'-'+asset['asset'])
 if archive.exists() and digest(archive)==asset['sha256']:return archive
 temporary=archive.with_suffix(archive.suffix+'.partial')
 request=Request(asset['url'],headers={'User-Agent':'MATREX-VR-companion-bundler'})
 with urlopen(request,timeout=120) as response,temporary.open('wb') as out:shutil.copyfileobj(response,out)
 if digest(temporary)!=asset['sha256']:
  temporary.unlink();raise ValueError('Checksum mismatch: '+asset['asset'])
 temporary.replace(archive)
 return archive

def find_one(root,predicate,description):
 found=[p for p in root.rglob('*') if p.is_file() and predicate(p)]
 if len(found)!=1:raise ValueError(f'Expected one {description}, found {len(found)} in {root}')
 return found[0]

def describe(root,backend,platform):
 windows=platform.startswith('windows');mac=platform.startswith('macos')
 filename=('rekkefly' if backend=='rekkefly' else 'hejjetrac')+('.exe' if windows else '')
 candidates=[p for p in root.rglob(filename) if p.is_file() and (not mac or '.app/Contents/MacOS/' in p.as_posix())]
 if not candidates and backend=='hejjetrac':candidates=[p for p in root.rglob('hejjetrak'+('.exe' if windows else '')) if p.is_file()]
 if not candidates:raise ValueError('Tracker executable absent in '+str(root))
 executable=sorted(candidates,key=lambda p:len(p.parts))[0]
 resources=executable.parent.parent/'Resources' if mac else executable.parent
 config=resources/'configs/demo.toml' if backend=='rekkefly' else resources/'examples/upstream-config.txt'
 if not config.exists():
  config=find_one(root,lambda p:p.name==('demo.toml' if backend=='rekkefly' else 'upstream-config.txt'),'demo config')
 live=resources/'configs/camera-a.toml' if backend=='rekkefly' else resources/'VR1.txt'
 if not live.exists():live=find_one(root,lambda p:p.name==('camera-a.toml' if backend=='rekkefly' else 'VR1.txt'),'camera config')
 sample=None if backend=='rekkefly' else resources/'examples/sample.mp4'
 if sample is not None and not sample.exists():sample=find_one(root,lambda p:p.name=='sample.mp4','sample video')
 return dict(executable=executable.relative_to(root).as_posix(),executableSha256=digest(executable),demoConfig=config.relative_to(root).as_posix(),cameraConfig=live.relative_to(root).as_posix(),sampleVideo=sample.relative_to(root).as_posix() if sample else None)

def stage(platforms,output,cache,lock=None):
 lock=lock or json.loads(LOCK.read_text())
 lock_id=hashlib.sha256(json.dumps(lock,sort_keys=True,separators=(',',':')).encode()).hexdigest()[:16]
 if output.exists():raise FileExistsError('Refusing to replace an existing companion bundle: '+str(output))
 output.parent.mkdir(parents=True,exist_ok=True)
 temporary=Path(tempfile.mkdtemp(prefix=output.name+'.staging-',dir=output.parent))
 try:
  entries=[]
  for platform in platforms:
   for backend,tracker in lock['trackers'].items():
    asset=tracker['platforms'][platform]
    package=temporary/backend/tracker['releaseTag']/platform
    extract(download(asset,cache),package)
    details=describe(package,backend,platform)
    entries.append(dict(backend=backend,platform=platform,releaseTag=tracker['releaseTag'],repository=tracker['repository'],releaseUrl=tracker['releaseUrl'],archiveSha256=asset['sha256'],packageRoot=package.relative_to(temporary).as_posix(),**details))
  catalog=dict(schemaVersion=1,catalogId=lock_id,installation='Versioned stable per-user installation; configuration is stored separately; no runtime downloads',entries=entries)
  (temporary/'catalog.json').write_text(json.dumps(catalog,indent=2)+'\n')
  (temporary/'companions.lock.json').write_text(json.dumps(lock,indent=2)+'\n')
  temporary.rename(output)
  print(output)
  return catalog
 except BaseException:
  shutil.rmtree(temporary,ignore_errors=True);raise

def seal_catalog(root):
 path=root/'catalog.json';catalog=json.loads(path.read_text())
 for entry in catalog['entries']:
  binary=safe_destination(safe_destination(root,entry['packageRoot']),entry['executable'])
  entry.setdefault('upstreamExecutableSha256',entry['executableSha256'])
  entry['executableSha256']=digest(binary)
 catalog['signing']='Executable digests sealed after explicit packaging/signing; original archive SHA-256 retained'
 path.write_text(json.dumps(catalog,indent=2)+'\n')
 print('Sealed executable digests:',path)

def main():
 p=argparse.ArgumentParser(description=__doc__)
 p.add_argument('--refresh-lock',action='store_true')
 p.add_argument('--seal-catalog',type=Path,help='After intentional signing of nested apps, update binary hashes before signing the parent app')
 p.add_argument('--platform',choices=['macOS','macos-arm64','macos-x64','windows-x64','linux-x64'])
 p.add_argument('--output',type=Path)
 p.add_argument('--cache',type=Path,default=ROOT/'.release-work/companion-cache')
 args=p.parse_args()
 if args.seal_catalog:seal_catalog(args.seal_catalog);return
 if args.refresh_lock:refresh_lock()
 if args.platform:
  if not args.output:p.error('--platform requires --output')
  stage(['macos-arm64','macos-x64'] if args.platform=='macOS' else [args.platform],args.output,args.cache)
 elif not args.refresh_lock:p.error('Choose --refresh-lock or --platform')

if __name__=='__main__':main()
