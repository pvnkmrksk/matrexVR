#!/usr/bin/env python3
"""Build a review-only hardware catalogue and offline bundle from measured inventory."""
from pathlib import Path
import html,json,re,subprocess,zipfile
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'docs/site/hardware'
def esc(x):return html.escape(str(x),quote=True)
manifest=json.loads((OUT/'inventory.json').read_text())
css='''
.hardware-main { max-width:1120px; margin:auto; padding:48px 30px 72px; }
.hardware-main h1 { font-size:clamp(2rem,4vw,3.2rem); }
.hardware-main h2 { margin-top:48px; font-size:1.7rem; }
.hardware-main h3 { margin-top:28px; }
.hardware-main .lead { font-size:1.1rem; max-width:760px; }
.review-note { border-left:4px solid var(--teal); background:var(--wash); padding:18px 22px; margin:24px 0; line-height:1.7; }
.part-grid { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:24px; }
.part-card { border:1px solid var(--line); border-radius:8px; overflow:hidden; min-width:0; }
.part-card img { width:100%; aspect-ratio:1100/680; object-fit:contain; }
.part-card .part-copy { padding:20px; }
.part-card h3 { margin-top:0; }
.part-card p { font-size:.85rem; margin-bottom:12px; }
.part-id, code { overflow-wrap:anywhere; }
.part-card .actions { gap:10px; flex-wrap:wrap; }
.part-card .button { padding:9px 14px; font-size:.8rem; }
.table-scroll { overflow-x:auto; }
table { border-collapse:collapse; width:100%; font-size:.85rem; line-height:1.6; }
th,td { border-bottom:1px solid var(--line); padding:12px; text-align:left; vertical-align:top; }
.hardware-main article { max-width:900px; margin:auto; }
.hardware-main article img { width:100%; max-width:700px; margin:20px auto; border:1px solid var(--line); border-radius:6px; }
.hardware-main li { line-height:1.8; margin-bottom:10px; }
.hardware-main article p { overflow-wrap:anywhere; }
@media(max-width:720px) { .hardware-main{padding:30px 22px;} .part-grid{grid-template-columns:1fr;} }
@media print { .masthead,.actions,.skip{display:none;} .hardware-main{padding:0;} .hardware-main article img{max-height:260px;object-fit:contain;} h2,h3{break-after:avoid;} figure,img,tr{break-inside:avoid;} }
'''
(OUT/'hardware.css').write_text(css)
def page(title,body):return f'''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{esc(title)} — MATREX VR</title><meta name="description" content="Recovered MATREX apparatus mesh candidates, dimensions and assembly review instructions."><link rel="stylesheet" href="../styles.css"><link rel="stylesheet" href="hardware.css"></head><body><a class="skip" href="#main">Skip to content</a><header class="masthead wrap"><a class="wordmark" href="../index.html">MATREX <span>VR</span></a><nav aria-label="Hardware navigation"><a href="index.html">Parts &amp; files</a><a href="assembly.html">Assembly guide</a><a href="../manual/getting-started.html">Software setup</a></nav></header><main id="main" class="hardware-main">{body}</main></body></html>'''
body='''<p class="eyebrow">Hardware resources / Local review edition</p><h1>Recover the parts.<br>Verify the apparatus.</h1><p class="lead">Dimensioned geometry previews and download files for five recovered source groups. Use these to identify the right revision before printing or assembling your apparatus.</p><div class="review-note"><strong>Candidate files, not a validated kit.</strong> Compatibility, required quantities and redistribution rights need confirmation. No verified matching parametric CAD history was recovered. 3MF files here contain editable mesh geometry; originals and printer-specific profiles remain local.</div><div class="actions"><a class="button primary" href="matrex-hardware-review.zip" download>Download review bundle ↓</a><a class="button" href="assembly.html">Assembly worksheet →</a><a class="text-link" href="inventory.json">Full inventory &amp; hashes</a></div><h2>Measured part catalogue</h2><p>All dimensions are X × Y × Z in millimetres after source build rotation. Plate translation was removed. Instance counts describe the original print plate, not quantities required for an apparatus.</p>'''
for part in manifest['parts']:
 body+=f'<section aria-labelledby="{part["id"]}"><h2 id="{part["id"]}">{esc(part["label"])}</h2><p>Recovered source: <code>{esc(part["sourceFilename"])}</code></p><div class="part-grid">'
 for s in part['shapes']:
  dims=' × '.join(f'{v:.2f}' for v in s['boundsMm']);links=''.join(f'<a class="button" href="files/{esc(e["file"])}" download>Download {Path(e["file"]).suffix[1:].upper()} ↓</a>' for e in s['exports'])
  body+=f'''<figure class="part-card"><img src="{s['image']}" width="1100" height="680" loading="lazy" alt="Measured isometric mesh of {esc(s['id'])}, {dims} millimetres"><figcaption class="part-copy"><h3 class="part-id">{esc(s['id'])}</h3><p><strong>{dims} mm</strong><br>{s['triangles']:,} triangles · {s['plateInstances']} original plate instance(s)<br>Indexed boundary edges: {s['boundaryEdges']}; non-manifold edges: {s['nonManifoldEdges']}; degenerate triangles: {s['degenerateTriangles']}.</p><div class="actions">{links}</div></figcaption></figure>'''
 body+='</div></section>'
body+=f'''<h2>What was recovered?</h2><p>The inventory records {len(manifest['inventory'])} filename-matched local candidates, including many incompatible or historical variants. Five representative groups are exported above. Selection is not a claim that these are the newest or mutually compatible versions. Other candidates stay inventory-only.</p><h2>Before sharing or building</h2><p>No redistribution license was found in the selected source metadata. This package grants no license. Confirm ownership and permission before publication. Source build orientation is retained as evidence, not a certified print recommendation. Read the <a href="assembly.html">illustrated assembly worksheet</a> for the candidate BOM, scale checks, dry-fit stages and missing inputs.</p>'''
(OUT/'index.html').write_text(page('Printable apparatus resources',body))
markdown=ROOT/'docs/hardware/assembly.md'
render=subprocess.check_output(['pandoc','--from=gfm','--to=html5',str(markdown)],text=True)
render=render.replace('../../docs/site/hardware/index.html','index.html').replace('../../docs/site/hardware/inventory.json','inventory.json').replace('../../docs/site/hardware/images/','images/').replace('../getting-started.md','../manual/getting-started.html').replace('../six-camera-rig.md','../manual/six-camera-rig.html').replace('../tracker-integration.md','../manual/tracker-integration.html')
# Tables can scroll within the document on small screens.
render=render.replace('<table>','<div class="table-scroll"><table>').replace('</table>','</table></div>')
(OUT/'assembly.html').write_text(page('Assembly worksheet','<article>'+render+'</article>'))
notice='''MATREX recovered hardware — local review edition, 2026-11-01\n\nNOT A VALIDATED KIT. No redistribution license was established. Confirm rights before sharing externally.\nOpen index.html for dimensions/downloads and assembly.html for the illustrated worksheet.\n3MF: millimetre-labelled editable mesh; STL: import coordinates in millimetres at 100% scale.\nNo parametric CAD history, printer settings or physical assembly validation is provided.\nOriginal source projects remain in local Downloads; source filenames/hashes appear in inventory.json.\nArchive includes only generated geometry, illustrations, documentation and website styles.\nExternal software documentation requires internet access.\n'''
(OUT/'README.txt').write_text(notice)
archive=OUT/'matrex-hardware-review.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(OUT.rglob('*')):
  if not p.is_file() or p==archive:continue
  data=p.read_bytes()
  if p.suffix=='.html':data=data.decode().replace('../styles.css','styles.css').replace('../index.html','https://pvnkmrksk.github.io/matrexVR/').replace('../manual/','https://pvnkmrksk.github.io/matrexVR/manual/').encode()
  # Exclude the archive's own download link in the offline index.
  if p.name=='index.html':data=data.replace(b'href="matrex-hardware-review.zip"',b'href="README.txt"').replace(b'Download review bundle',b'Review bundle notes')
  info=zipfile.ZipInfo(p.relative_to(OUT).as_posix(),(2026,11,1,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;z.writestr(info,data)
 z.writestr('styles.css',(ROOT/'docs/site/styles.css').read_bytes())
print('Built hardware catalogue, illustrated assembly worksheet and offline review ZIP:',archive.stat().st_size,'bytes')
