#!/usr/bin/env python3
"""Recover local 3MF geometry for review; never infer a buildable apparatus or license.
Uses numpy/Pillow for measured illustrations. Originals and slicer profiles stay local.
"""
import argparse, collections, hashlib, html, json, math, re, struct, zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
NS = 'http://schemas.microsoft.com/3dmanufacturing/core/2015/02'
PROD = 'http://schemas.microsoft.com/3dmanufacturing/production/2015/06'
SELECTED = [
 ('camera-locust-matrex', 'Part Studio 1 - Camera Holder Locust MATREX.3mf', 'Locust MATREX camera holder'),
 ('panel-middle', 'Led Panel Holder - Middle.3mf', 'Middle LED-panel holder'),
 ('top-panel-p1952', 'Top Panel Mount P1.952 128 128.3mf', 'Top-panel mount · P1.952 / 128 × 128 filename variant'),
 ('ball-holder-265', 'Ball Holder 26.5-2.3mf', 'Ball-holder 26.5 filename variant'),
 ('table-clamp', 'Table Clamp Part Studio-7.3mf', 'Table-clamp filename variant 7'),
]

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def tag(s): return '{'+NS+'}'+s

def transform(value):
    v = list(map(float, value.split())) if value else [1,0,0,0,1,0,0,0,1,0,0,0]
    if len(v)!=12 or not all(math.isfinite(x) for x in v): raise ValueError('Invalid 3MF transform')
    return np.array([[v[0],v[3],v[6],v[9]],[v[1],v[4],v[7],v[10]],[v[2],v[5],v[8],v[11]],[0,0,0,1]])

def load(path):
    with zipfile.ZipFile(path) as z:
        assert z.testzip() is None
        models = {n: ET.fromstring(z.read(n)) for n in z.namelist() if n.endswith('.model')}
        assert all(m.get('unit','millimeter')=='millimeter' for m in models.values()), 'Non-mm source needs explicit conversion'
        objects = {n: {o.get('id'):o for o in m.findall('./'+tag('resources')+'/'+tag('object'))} for n,m in models.items()}
        def mesh(file, oid, matrix, stack=()):
            key=(file,oid)
            if key in stack: raise ValueError('Cyclic components')
            o=objects[file][oid]; m=o.find(tag('mesh'))
            if m is not None:
                vertices=np.array([[float(v.get(a)) for a in ('x','y','z')] for v in m.find(tag('vertices'))])
                triangles=np.array([[int(t.get(a)) for a in ('v1','v2','v3')] for t in m.find(tag('triangles'))],dtype=np.int32)
                assert np.isfinite(vertices).all() and triangles.min()>=0 and triangles.max()<len(vertices)
                if np.linalg.det(matrix[:3,:3]) < 0: triangles=triangles[:,[0,2,1]]
                yield (np.c_[vertices,np.ones(len(vertices))]@matrix.T)[:,:3],triangles
            for c in o.findall('./'+tag('components')+'/'+tag('component')):
                target=c.get('{'+PROD+'}path',file).lstrip('/')
                yield from mesh(target,c.get('objectid'),matrix@transform(c.get('transform')),stack+(key,))
        shapes=[]
        for i,item in enumerate(models['3D/3dmodel.model'].find(tag('build'))):
            if item.get('printable','1')=='0': continue
            chunks=list(mesh('3D/3dmodel.model',item.get('objectid'),transform(item.get('transform'))))
            if not chunks: continue
            vertices=np.concatenate([v for v,t in chunks]); offset=0; tri=[]
            for v,t in chunks: tri.append(t+offset);offset+=len(v)
            vertices-=vertices.min(axis=0) # preserve rotation, remove plate translation
            triangles=np.concatenate(tri)
            fingerprint=hashlib.sha256(np.round(vertices,4).tobytes()+triangles.tobytes()).hexdigest()
            existing=next((s for s in shapes if s['fingerprint']==fingerprint),None)
            if existing: existing['plateInstances']+=1
            else: shapes.append(dict(vertices=vertices,triangles=triangles,fingerprint=fingerprint,plateInstances=1,sourceBuildItem=i+1))
        return shapes

def export(directory, stem, vertices, triangles):
    stl=directory/(stem+'.stl')
    with stl.open('wb') as f:
        f.write(b'MATREX geometry review; coordinates in millimeters'.ljust(80,b'\0'));f.write(struct.pack('<I',len(triangles)))
        for t in triangles:
            a,b,c=vertices[t];normal=np.cross(b-a,c-a);length=np.linalg.norm(normal)
            if length: normal/=length
            f.write(struct.pack('<12fH',*normal,*a,*b,*c,0))
    model=ET.Element('model',{'xmlns':NS,'unit':'millimeter'});resources=ET.SubElement(model,'resources');obj=ET.SubElement(resources,'object',{'id':'1','type':'model'});mesh=ET.SubElement(obj,'mesh');vs=ET.SubElement(mesh,'vertices');ts=ET.SubElement(mesh,'triangles')
    for v in vertices: ET.SubElement(vs,'vertex',dict(zip(('x','y','z'),(format(float(x),'.9g') for x in v))))
    for t in triangles: ET.SubElement(ts,'triangle',dict(zip(('v1','v2','v3'),map(str,t))))
    ET.SubElement(ET.SubElement(model,'build'),'item',{'objectid':'1'})
    three=directory/(stem+'.3mf')
    with zipfile.ZipFile(three,'w',zipfile.ZIP_DEFLATED) as z:
        z.writestr('[Content_Types].xml','<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="model" ContentType="application/vnd.ms-package.3dmanufacturing-3dmodel+xml"/></Types>')
        z.writestr('_rels/.rels','<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Target="/3D/3dmodel.model" Id="rel0" Type="http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel"/></Relationships>')
        z.writestr('3D/3dmodel.model',ET.tostring(model,encoding='utf-8',xml_declaration=True))
    # Indexed topology; a diagnostic, not a physical printability certification.
    edges=collections.Counter(tuple(sorted((int(t[a]),int(t[b])))) for t in triangles for a,b in ((0,1),(1,2),(2,0)))
    cross=np.cross(vertices[triangles[:,1]]-vertices[triangles[:,0]],vertices[triangles[:,2]]-vertices[triangles[:,0]])
    assert stl.stat().st_size==84+50*len(triangles)
    with zipfile.ZipFile(three) as z: assert z.testzip() is None
    return {'boundsMm':[round(float(x),4) for x in vertices.max(axis=0)],'vertices':len(vertices),'triangles':len(triangles),'boundaryEdges':sum(n==1 for n in edges.values()),'nonManifoldEdges':sum(n>2 for n in edges.values()),'degenerateTriangles':int((np.linalg.norm(cross,axis=1)<1e-10).sum()),'exports':[{'file':p.name,'sha256':sha(p),'bytes':p.stat().st_size} for p in (three,stl)]}

def illustrate(path, vertices, triangles, label):
    image=Image.new('RGB',(1100,680),'#f6f8fb');d=ImageDraw.Draw(image)
    # Orthographic isometric projection; actual triangles, no invented assembly.
    q=vertices-vertices.mean(axis=0);project=np.array([[.707,-.707,0],[.408,.408,-.816]]);xy=q@project.T;depth=q@np.array([.577,.577,.577]);extent=np.ptp(xy,axis=0);scale=min(990/max(extent[0],.001),540/max(extent[1],.001));xy=(xy-(xy.max(axis=0)+xy.min(axis=0))/2)*scale+np.array([550,320])
    for idx in np.argsort(depth[triangles].mean(axis=1)):
        t=triangles[idx];normal=np.cross(q[t[1]]-q[t[0]],q[t[2]]-q[t[0]]);norm=np.linalg.norm(normal);shade=.45+.45*abs(float(normal@np.array([.2,.5,.84]))/norm) if norm else .5
        d.polygon([tuple(p) for p in xy[t]],fill=tuple(int(c*shade) for c in (51,151,146)))
    dims=' × '.join(f'{x:.2f}' for x in vertices.max(axis=0))+' mm (X × Y × Z)'
    d.text((30,615),label,fill='#20334c');d.text((30,640),dims+' | source build orientation; plate translation removed',fill='#52657a');image.save(path)

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--downloads',type=Path,default=Path.home()/'Downloads');args=parser.parse_args()
    out=ROOT/'docs/site/hardware';files=out/'files';images=out/'images';files.mkdir(parents=True,exist_ok=True);images.mkdir(exist_ok=True)
    candidates=[]
    pattern=re.compile(r'panel|camera.?holder|ball.?holder|table.?clamp|flexure|main.?assembly',re.I)
    for p in sorted(args.downloads.iterdir()):
        if p.suffix.lower() in ('.3mf','.stl','.step','.stp','.scad','.fcstd') and pattern.search(p.stem):
            candidates.append({'name':p.name,'bytes':p.stat().st_size,'sha256':sha(p),'format':p.suffix.lower()[1:],'association':'filename candidate; compatibility and rights unconfirmed','location':'local Downloads (not distributed)'})
    manifest={'searchScope':['Project source tree (Unity visual meshes excluded)', 'Local Downloads top-level filename candidates', 'Documents and Desktop CAD/print filename scan: no additional matches found'], 'excludedVisualAssets':[str(p.relative_to(ROOT)) for p in sorted((ROOT/'Assets/Prefabs/Locust').glob('*.blend'))], 'status':'Recovered geometry candidates for local review; not a validated kit','units':'millimeter','license':'No redistribution license found in selected source metadata. No license is granted by this package. Confirm ownership/permission before publication.','parametricSources':'No verified matching parametric source recovered. 3MF is editable mesh geometry, not CAD history. Unmatched STEP candidate is inventory-only.','inventory':candidates,'parts':[]}
    for slug,name,label in SELECTED:
        p=args.downloads/name;part={'id':slug,'label':label,'sourceFilename':name,'sourceSha256':sha(p),'variantSelection':'Representative named candidate, not a newest/compatible revision claim','shapes':[]}
        for i,s in enumerate(load(p),1):
            stem=f'{slug}-{i:02d}';data=export(files,stem,s['vertices'],s['triangles']);data.update(id=stem,plateInstances=s['plateInstances'],sourceBuildItem=s['sourceBuildItem'],image='images/'+stem+'.png');illustrate(out/data['image'],s['vertices'],s['triangles'],label+f' / shape {i}');part['shapes'].append(data)
        manifest['parts'].append(part)
    (out/'inventory.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print(json.dumps({'candidates':len(candidates),'sourceGroups':len(manifest['parts']),'shapes':sum(len(p['shapes']) for p in manifest['parts']),'output':str(out)},indent=2))
if __name__=='__main__':main()
