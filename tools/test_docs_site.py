#!/usr/bin/env python3
"""Check static manual navigation, generated assets, deep links and search coverage."""
from collections import Counter
import argparse
from html.parser import HTMLParser
import json
import hashlib
from pathlib import Path
from urllib.parse import unquote, urlsplit

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--require-gallery', action='store_true', help='Final-publication gate: require every genuine gallery image and capture provenance.')
args = parser.parse_args()

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / 'docs/site'


class Document(HTMLParser):
    def __init__(self, path):
        super().__init__()
        self.path = path
        self.ids = []
        self.links = []
        self.images = []
        self.feed(path.read_text())

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if attrs.get('id'):
            self.ids.append(attrs['id'])
        for name in ('href', 'src'):
            if attrs.get(name):
                self.links.append(attrs[name])
        if tag == 'img':
            self.images.append(attrs)


pages = json.loads((ROOT / 'docs/manual/navigation.json').read_text())
assert len({p['slug'] for p in pages}) == len(pages), 'Duplicate manual slug'
documents = {p: Document(p) for p in SITE.rglob('*.html')}
failures = []
checked = 0
for path, doc in list(documents.items()):
    duplicate = [value for value, count in Counter(doc.ids).items() if count > 1]
    if duplicate:
        failures.append(f'{path.relative_to(ROOT)}: duplicate IDs {duplicate}')
    for image in doc.images:
        if 'alt' not in image:
            failures.append(f'{path.relative_to(ROOT)}: image without alt {image.get("src")}')
    for value in doc.links:
        u = urlsplit(value)
        if u.scheme or u.netloc:
            continue
        target = (path.parent / unquote(u.path)).resolve() if u.path else path
        if target.is_dir():
            target = target / 'index.html'
        checked += 1
        if not target.exists():
            failures.append(f'{path.relative_to(ROOT)}: missing {value}')
        elif u.fragment and target.suffix in {'.html', '.svg'}:
            target_doc = documents.get(target)
            if target_doc is None:
                target_doc = Document(target)
                documents[target] = target_doc
            if unquote(u.fragment) not in target_doc.ids:
                failures.append(f'{path.relative_to(ROOT)}: missing fragment {value}')
for page in pages:
    path = SITE / 'manual' / (page['slug'] + '.html')
    assert path in documents, path
    content = path.read_text()
    assert 'aria-label="Documentation chapters"' in content
    assert 'aria-label="On this page"' in content
    assert 'id="manual-search"' in content
    assert 'View Markdown source' in content
search = json.loads((SITE / 'manual/search-index.json').read_text())
assert len(search) > 200, 'Search index omits detailed sections'
all_text = ' '.join(e['page'] + ' ' + e['title'] + ' ' + e['text'] for e in search)
for term in ('ViewportSetter', 'ClosedLoop', 'Kannadi', 'ಕನ್ನಡಿ', 'sphereDiameter', 'schemaVersion', 'ReplayController', 'config_manifest.json', 'speciesName', 'Rekkefly', 'HejjéTrac', 'Daari Deepa'):
    assert term in all_text, 'Missing indexed concept: ' + term
for entry in search:
    u = urlsplit(entry['url'])
    target = SITE / 'manual' / u.path
    assert target in documents, entry['url']
    assert not u.fragment or u.fragment in documents[target].ids, entry['url']
schemas = json.loads((ROOT / 'Assets/Resources/ConfigurationUI/schemas.json').read_text())
def property_paths(node, prefix=''):
    paths = []
    for key, child in node.get('properties', {}).items():
        path = prefix + ('.' if prefix else '') + key
        paths.append(path)
        paths.extend(property_paths(child, path))
    if isinstance(node.get('items'), dict):
        paths.extend(property_paths(node['items'], prefix + '[]'))
    for union in ('oneOf', 'anyOf', 'allOf'):
        for child in node.get(union, []):
            paths.extend(property_paths(child, prefix))
    return paths
expected_fields = 0
for family, schema in schemas.items():
    fields = property_paths(schema)
    expected_fields += len(fields)
    page_entries = [entry for entry in search if entry['url'].startswith('schema-' + family + '.html#')]
    titles = {entry['title'] for entry in page_entries}
    missing = set(fields) - titles
    assert not missing, (family, 'Unindexed schema paths', sorted(missing))
manifest = json.loads((SITE / 'manual/build-info.json').read_text())
assert manifest['schemaPropertyPaths'] == expected_fields
for source, digest in manifest['sourceSha256'].items():
    assert hashlib.sha256((ROOT / source).read_bytes()).hexdigest() == digest, 'Rebuild the static manual after editing ' + source
assert (SITE / 'manual/assets/reference/docs/manual/generated/schema-source.json').read_bytes() == (ROOT / 'Assets/Resources/ConfigurationUI/schemas.json').read_bytes()
if args.require_gallery:
    gallery = json.loads((SITE / 'assets/gallery-manifest.json').read_text())
    for item in gallery['items']:
        image = SITE / 'assets' / item['image']
        assert image.is_file() and image.stat().st_size > 0, 'Final gallery capture missing: ' + item['image']
    provenance = SITE / 'assets/gallery/provenance.json'
    assert provenance.is_file(), 'Final gallery provenance is missing'
    capture = json.loads(provenance.read_text())
    assert capture.get('failure') is None, 'Native gallery capture failed'
    records = capture.get('records', [])
    assert len(records) == len(gallery['items']) == 9, 'Expected nine native gallery records'
    by_name = {record['name']: record for record in records}
    assert len(by_name) == len(records), 'Duplicate native gallery record'
    assert set(by_name) == {item['id'] for item in gallery['items']}, 'Gallery record/name mismatch'
    hashes = {record['name']: record for record in json.loads((SITE / 'manual/gallery-hashes.json').read_text())['records']}
    def jpeg_size(path):
        data = path.read_bytes()
        assert data[:2] == b'\xff\xd8', 'Not a JPEG: ' + str(path)
        at = 2
        while at < len(data):
            assert data[at] == 255, 'Invalid JPEG marker'
            while data[at] == 255: at += 1
            marker = data[at]; at += 1
            if marker in {0xD8,0xD9} or 0xD0 <= marker <= 0xD7: continue
            length = int.from_bytes(data[at:at+2], 'big')
            assert length >= 2, 'Invalid JPEG segment'
            if marker in {0xC0,0xC1,0xC2,0xC3,0xC5,0xC6,0xC7,0xC9,0xCA,0xCB,0xCD,0xCE,0xCF}:
                return int.from_bytes(data[at+5:at+7],'big'), int.from_bytes(data[at+3:at+5],'big')
            at += length
        raise AssertionError('Missing JPEG dimensions')
    for item in gallery['items']:
        record = by_name[item['id']]
        image = SITE / 'assets' / item['image']
        assert record['image'] == image.name, 'Image provenance mismatch'
        assert record.get('scene') and 'Actual Unity' in record.get('source', ''), 'Missing native source attribution'
        assert jpeg_size(image) == (record['width'], record['height']), 'Capture dimension mismatch'
        config_name = record['configuration']
        assert Path(config_name).name == config_name, 'Unexpected gallery configuration path'
        config = image.parent / config_name
        assert config.is_file() and isinstance(json.loads(config.read_text()), dict), 'Missing gallery experiment config'
        assert hashes[item['id']]['imageSha256'] == hashlib.sha256(image.read_bytes()).hexdigest(), 'Rebuild after a gallery image changes'
        assert hashes[item['id']]['configSha256'] == hashlib.sha256(config.read_bytes()).hexdigest(), 'Rebuild after a gallery config changes'
    assert by_name['forest-choice'].get('additionalPrefab') == 'Assets/Prefabs/Landscape.prefab', 'Forest fixture customization must be explicit'
    for error in capture.get('errors', []):
        assert 'UnityEditor.Search.SearchDatabase' in error and 'IndexationOnStartup' in error, 'Unexpected native capture error'
    assert 'Native capture pending' not in (SITE / 'manual/gallery.html').read_text(), 'Rebuild the manual after adding real captures'
license_path = SITE / 'manual/assets/lucide/LICENSE'
assert 'ISC License' in license_path.read_text()
assert 'cff-version: 1.2.0' in (ROOT / 'CITATION.cff').read_text()
if failures:
    print('\n'.join(failures[:60]))
    raise SystemExit(f'{len(failures)} static documentation errors')
print(f'PASS: {len(pages)} manual chapters; {len(search)} searchable sections; {checked} local links/assets; {expected_fields} schema paths; image alt text, unique anchors, search coverage and icon/citation notices.')
