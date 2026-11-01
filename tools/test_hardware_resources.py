#!/usr/bin/env python3
"""Verify recovered geometry, export integrity and static resource references."""
import hashlib,json,struct,tempfile,unittest,zipfile
from collections import Counter
from pathlib import Path
from html.parser import HTMLParser
from urllib.parse import unquote,urlsplit
import xml.etree.ElementTree as ET
import numpy as np
from prepare_hardware_resources import load,NS,PROD
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'docs/site/hardware'
class Resources(unittest.TestCase):
 def test_actual_exports(self):
  data=json.loads((OUT/'inventory.json').read_text());self.assertEqual(len(data['parts']),5)
  for part in data['parts']:
   for shape in part['shapes']:
    for export in shape['exports']:
     p=OUT/'files'/export['file'];self.assertEqual(hashlib.sha256(p.read_bytes()).hexdigest(),export['sha256'])
     if p.suffix=='.stl':
      raw=p.read_bytes();count=struct.unpack_from('<I',raw,80)[0];self.assertEqual(count,shape['triangles']);self.assertEqual(len(raw),84+50*count)
      vertices=np.array([struct.unpack_from('<12fH',raw,84+50*i)[3:12] for i in range(count)]).reshape((-1,3));self.assertTrue(np.isfinite(vertices).all());np.testing.assert_allclose(np.ptp(vertices,axis=0),shape['boundsMm'],atol=.0001)
     else:
      meshes=load(p);self.assertEqual(len(meshes),1);self.assertEqual(len(meshes[0]['triangles']),shape['triangles']);np.testing.assert_allclose(np.ptp(meshes[0]['vertices'],axis=0),shape['boundsMm'],atol=.0001)
      with zipfile.ZipFile(p) as z:
       self.assertIsNone(z.testzip());self.assertEqual(set(z.namelist()),{'[Content_Types].xml','_rels/.rels','3D/3dmodel.model'})
      v=meshes[0]['vertices'];t=meshes[0]['triangles'];edges=Counter(tuple(sorted((int(tr[a]),int(tr[b])))) for tr in t for a,b in ((0,1),(1,2),(2,0)));self.assertEqual(sum(n==1 for n in edges.values()),shape['boundaryEdges']);self.assertEqual(sum(n>2 for n in edges.values()),shape['nonManifoldEdges']);self.assertEqual(set(edges.values()),{2})
    self.assertTrue((OUT/shape['image']).is_file())
 def test_nested_transform_and_reflection(self):
  # A tetrahedron, component rotation/translation, then reflected build transform.
  child=f'<model xmlns="{NS}" unit="millimeter"><resources><object id="1" type="model"><mesh><vertices><vertex x="0" y="0" z="0"/><vertex x="2" y="0" z="0"/><vertex x="0" y="3" z="0"/><vertex x="0" y="0" z="4"/></vertices><triangles><triangle v1="0" v2="2" v3="1"/><triangle v1="0" v2="1" v3="3"/><triangle v1="0" v2="3" v3="2"/><triangle v1="1" v2="2" v3="3"/></triangles></mesh></object></resources></model>'
  root=f'<model xmlns="{NS}" xmlns:p="{PROD}" unit="millimeter"><resources><object id="1" type="model"><components><component p:path="/3D/Objects/child.model" objectid="1" transform="0 1 0 -1 0 0 0 0 1 7 8 9"/></components></object></resources><build><item objectid="1" transform="-1 0 0 0 1 0 0 0 1 100 200 300"/></build></model>'
  with tempfile.TemporaryDirectory() as d:
   p=Path(d)/'test.3mf'
   with zipfile.ZipFile(p,'w') as z:z.writestr('3D/3dmodel.model',root);z.writestr('3D/Objects/child.model',child)
   shape=load(p)[0];v=shape['vertices'];t=shape['triangles'];np.testing.assert_allclose(v,[[0,0,0],[0,2,0],[3,0,0],[0,0,4]])
   volume=np.einsum('ij,ij->i',v[t[:,0]],np.cross(v[t[:,1]],v[t[:,2]])).sum()/6;self.assertAlmostEqual(volume,4)
 def test_offline_bundle(self):
  with zipfile.ZipFile(OUT/'matrex-hardware-review.zip') as z:
   self.assertIsNone(z.testzip());names=z.namelist();self.assertIn('assembly.html',names);self.assertIn('styles.css',names)
   self.assertFalse(any('Metadata' in n or '.gcode' in n for n in names))
   for name in names:
    if name.startswith('files/'):self.assertEqual(z.read(name),(OUT/name).read_bytes())
 def test_all_site_links_and_images(self):
  class Doc(HTMLParser):
   def __init__(self,p):super().__init__();self.refs=[];self.ids=[];self.feed(p.read_text())
   def handle_starttag(self,tag,attrs):
    a=dict(attrs)
    if 'id' in a:self.ids.append(a['id'])
    if tag=='img' and 'alt' not in a:raise AssertionError('Missing image alt')
    for key in ('href','src'):
     if a.get(key):self.refs.append(a[key])
  site=ROOT/'docs/site';docs={p.resolve():Doc(p) for p in site.rglob('*.html')}
  for p,doc in docs.items():
   self.assertEqual(len(doc.ids),len(set(doc.ids)),str(p))
   for ref in doc.refs:
    u=urlsplit(ref)
    if u.scheme or u.netloc:continue
    target=(p.parent/unquote(u.path)).resolve() if u.path else p
    if target.is_dir():target/='index.html'
    self.assertTrue(target.exists(),str(p)+' → '+ref)
    if u.fragment and target.suffix=='.html':self.assertIn(unquote(u.fragment),docs[target].ids,str(p)+' → '+ref)
if __name__=='__main__':unittest.main()
