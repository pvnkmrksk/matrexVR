#!/usr/bin/env python3
"""Pre-render the MATREX VR manual with installed Pandoc; readers need no runtime dependencies."""
import argparse
import hashlib
import html
from html.parser import HTMLParser
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
from urllib.parse import unquote, urlsplit, urlunsplit

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / 'docs/site'
OUT = SITE / 'manual'
REPO = 'https://github.com/pvnkmrksk/matrexVR'
BASE_URL = 'https://pvnkmrksk.github.io/matrexVR/'
PAGES = json.loads((ROOT / 'docs/manual/navigation.json').read_text())
BY_SOURCE = {(ROOT / p['source']).resolve(): p for p in PAGES}


def esc(value):
    return html.escape(str(value), quote=True)


def icon(name):
    return '<svg class="icon" aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><use href="assets/icons.svg#' + esc(name) + '"></use></svg>'


def output_name(page):
    return page['slug'] + '.html'


class Rewrite(HTMLParser):
    def __init__(self, source):
        super().__init__(convert_charrefs=False)
        self.source = source
        self.parts = []

    def link(self, value, image=False):
        u = urlsplit(value)
        if u.scheme or u.netloc or not u.path:
            return value
        path = (self.source.parent / unquote(u.path)).resolve()
        if path in BY_SOURCE:
            new = output_name(BY_SOURCE[path])
        elif path == ROOT / 'CITATION.cff':
            shutil.copyfile(path, OUT / 'CITATION.cff')
            new = 'CITATION.cff'
        elif path.is_relative_to(SITE):
            new = os.path.relpath(path, OUT).replace(os.sep, '/')
        elif path.is_file() and (image or (path.is_relative_to(ROOT / 'docs') and path.suffix in {'.json', '.txt', '.svg', '.png', '.jpg', '.jpeg', '.webp'})):
            relative = path.relative_to(ROOT)
            dest = OUT / 'assets/reference' / relative
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, dest)
            new = dest.relative_to(OUT).as_posix()
        elif path.is_relative_to(ROOT):
            relative = path.relative_to(ROOT).as_posix()
            new = REPO + ('/tree/main/' if path.is_dir() else '/blob/main/') + relative
        else:
            return value
        return urlunsplit(('', '', new, u.query, u.fragment))

    def handle_starttag(self, tag, attrs):
        if tag == 'table':
            self.parts.append('<div class="table-scroll">')
        values = []
        for key, value in attrs:
            if key in {'href', 'src'} and value:
                value = self.link(value, image=tag == 'img' and key == 'src')
            values.append(key if value is None else key + '="' + esc(value) + '"')
        if tag == 'img' and not any(k == 'loading' for k, v in attrs):
            values.append('loading="lazy"')
        self.parts.append('<' + tag + (' ' + ' '.join(values) if values else '') + '>')

    def handle_startendtag(self, tag, attrs):
        self.handle_starttag(tag, attrs)

    def handle_endtag(self, tag):
        self.parts.append('</' + tag + '>')
        if tag == 'table':
            self.parts.append('</div>')

    def handle_data(self, data):
        self.parts.append(data)

    def handle_entityref(self, name):
        self.parts.append('&' + name + ';')

    def handle_charref(self, name):
        self.parts.append('&#' + name + ';')

    def handle_comment(self, data):
        self.parts.append('<!--' + data + '-->')


class Sections(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.entries = []
        self.heading = None
        self.current = None

    def handle_starttag(self, tag, attrs):
        if tag in {'h1', 'h2', 'h3'}:
            self.heading = {'level': int(tag[1]), 'id': dict(attrs).get('id', ''), 'title': '', 'text': ''}
        elif tag in {'p', 'li', 'tr', 'pre', 'figcaption'} and self.current:
            self.current['text'] += '\n'

    def handle_endtag(self, tag):
        if self.heading and tag == 'h' + str(self.heading['level']):
            self.current = self.heading
            self.entries.append(self.current)
            self.heading = None

    def handle_data(self, data):
        if self.heading:
            self.heading['title'] += data
        elif self.current:
            self.current['text'] += data


def navigation(current):
    groups = []
    for group in dict.fromkeys(p['group'] for p in PAGES):
        entries = [p for p in PAGES if p['group'] == group]
        links = ''.join('<a href="' + output_name(p) + '"' + (' class="active" aria-current="page"' if p == current else '') + '>' + icon(p['icon']) + '<span>' + esc(p['title']) + '</span></a>' for p in entries)
        expanded = ' open' if group in {'Start here', current['group']} else ''
        groups.append('<details class="nav-group"' + expanded + '><summary>' + esc(group) + icon('chevron-down') + '</summary><div>' + links + '</div></details>')
    return ''.join(groups)


def gallery():
    manifest = json.loads((SITE / 'assets/gallery-manifest.json').read_text())
    provenance_path = SITE / 'assets/gallery/provenance.json'
    provenance = json.loads(provenance_path.read_text()) if provenance_path.is_file() else {}
    records = {record['name']: record for record in provenance.get('records', [])}
    cards, hashes = [], []
    for item in manifest['items']:
        path = SITE / 'assets' / item['image']
        record = records.get(item['id'], {})
        if path.is_file():
            figure = '<a href="../site/assets/' + esc(item['image']) + '"><img src="../site/assets/' + esc(item['image']) + '" loading="lazy" alt="' + esc(item['title']) + ' — actual Unity capture"></a>'
            evidence = {'name': item['id'], 'image': item['image'], 'imageSha256': hashlib.sha256(path.read_bytes()).hexdigest()}
            config = record.get('configuration')
            if config and (path.parent / config).is_file():
                config_path = path.parent / config
                evidence.update(configuration='gallery/' + config, configSha256=hashlib.sha256(config_path.read_bytes()).hexdigest())
                links = '<p class="gallery-links"><a href="../site/assets/gallery/' + esc(config) + '">Experiment JSON</a> · <a href="../site/assets/gallery/provenance.json">Capture provenance</a></p>'
            else:
                links = ''
            hashes.append(evidence)
        else:
            figure = '<div class="capture-pending">Native capture pending<span>' + esc(item['scene']) + '</span></div>'
            links = ''
        scene = record.get('scene', item['scene'])
        cards.append('<figure class="gallery-card" id="' + esc(item['id']) + '">' + figure + '<figcaption><span class="gallery-scene">' + esc(scene) + '</span><h2 id="' + esc(item['id']) + '-preview">' + esc(item['title']) + '</h2><p>' + esc(item['summary']) + '</p>' + links + '</figcaption></figure>')
    (OUT / 'gallery-hashes.json').write_text(json.dumps({'description':'SHA-256 of actual gallery images and consumed demonstration JSON copied with the static manual build. Capture provenance remains separate and unmodified.', 'records':hashes}, indent=2, ensure_ascii=False)+'\n')
    note = '<a href="../site/assets/gallery/provenance.json">Capture provenance</a> identifies the native fixture, scene and configuration. <a href="../site/manual/gallery-hashes.json">Image/config hashes</a> identify the actual files.' if provenance else 'Capture provenance is added with the native image set.'
    if provenance.get('errors'):
        note += ' The manifest retains an Editor startup SearchDatabase exception separately; the gallery failure field is null. No hardware collection is represented.'
    return '<div class="capability-gallery">' + ''.join(cards) + '</div><p class="gallery-provenance">' + note + '</p>'


def generate_schema_sources():
    """Expose every emitted property path, without inventing runtime behavior from UI ranges."""
    schemas_path = ROOT / 'Assets/Resources/ConfigurationUI/schemas.json'
    schemas = json.loads(schemas_path.read_text())
    help_text = json.loads((ROOT / 'Assets/Resources/ConfigurationUI/field-help.json').read_text())
    generated = ROOT / 'docs/manual/generated'
    generated.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(schemas_path, generated / 'schema-source.json')
    counts = {}
    def walk(node, prefix=''):
        for key, child in node.get('properties', {}).items():
            path = prefix + ('.' if prefix else '') + key
            yield path, child, key in node.get('required', [])
            yield from walk(child, path)
        if isinstance(node.get('items'), dict):
            yield from walk(node['items'], prefix + '[]')
        for union in ('oneOf', 'anyOf', 'allOf'):
            for child in node.get(union, []):
                yield from walk(child, prefix)
    for family, schema in schemas.items():
        fields = list(walk(schema))
        counts[family] = len(fields)
        content = [f'# {family.title()} schema fields',
            f'This atlas lists **{len(fields)} emitted property paths** from the current `{family}` runtime/UI schema. It is generated from the actual bundled JSON rather than a second handwritten parameter list.',
            'The schema default is a serialized type default, not a guarantee that it overrides an authored scene/prefab or a field retained by an in-place controller. Production loaders also enforce cross-field rules. A UI slider is a convenience range, not a runtime clamp. See [parameter semantics](../../configuration-reference.md) and [the exact emitted schema](schema-source.json).',
            'Every field has a deep link and is indexed by the manual search. Array items use `[]` in their path. Nested pre/stimulus/post fields are listed where the schema emits them; repeated names in different phases are distinct paths.']
        group = None
        for path, node, required in fields:
            top = path.split('.')[0]
            if group != top:
                group = top
                content.append('## ' + top + ' group')
            ui = node.get('x-ui', {})
            leaf = path.split('.')[-1].replace('[]', '')
            description = ui.get('help') or node.get('description') or help_text.get(family + '.' + path) or help_text.get(path) or help_text.get(leaf)
            if leaf == 'vrId':
                description = 'Exact unique positive VR identifier, such as VR1 or VR10. Duplicate IDs are rejected; a rig is separate from its animal ID/species.'
            content.append('### `' + path + '`')
            if description:
                content.append(str(description))
            typ = node.get('type', 'schema union' if any(k in node for k in ['oneOf','anyOf','allOf']) else 'unspecified')
            if isinstance(typ, list): typ = ' or '.join(typ)
            rows = ['| Contract | Emitted value |', '| --- | --- |', '| Type | `' + str(typ) + '` |', '| Required by parent schema | ' + ('Yes' if required else 'No') + ' |']
            if 'default' in node and not isinstance(node['default'], (dict,list)):
                rows.append('| Default | `' + json.dumps(node['default'], ensure_ascii=False).replace('|', '\\|') + '` |')
            elif 'default' not in node:
                rows.append('| Default | No default emitted at this path. |')
            if ui.get('unit'):
                rows.append('| UI unit label | ' + str(ui['unit']).replace('|','\\|') + ' |')
            for key in ['enum','minimum','maximum','exclusiveMinimum','exclusiveMaximum','minItems','maxItems','additionalProperties']:
                if key in node:
                    rows.append('| `' + key + '` | `' + json.dumps(node[key],ensure_ascii=False,separators=(',',':')).replace('|','\\|') + '` |')
            if ui.get('slider'):
                rows.append('| UI soft slider range | `' + json.dumps(ui['slider'],ensure_ascii=False,separators=(',',':')).replace('|','\\|') + '`; display/edit convenience, not an extra runtime constraint. |')
            content.append('\n'.join(rows))
            if 'default' in node and isinstance(node['default'], (dict,list)):
                content.append('<details><summary>Exact emitted default JSON</summary>\n\n```json\n' + json.dumps(node['default'],indent=2,ensure_ascii=False) + '\n```\n\n</details>')
            for key in ['visibleWhen','enabledWhen','readOnly']:
                if key in ui:
                    content.append('UI `' + key + '`: `' + json.dumps(ui[key],ensure_ascii=False,separators=(',',':')) + '`. This governs the form, not removal of stored values.')
        content.append('## Source\n\n[Bundled schema source](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Resources/ConfigurationUI/schemas.json) · [Schema generator](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Editor/ConfigurationSchemaBuilder.cs)')
        (generated / ('schema-' + family + '.md')).write_text('\n\n'.join(content) + '\n')
    intro = ['# Full schema field atlas',
        f'The bundled schemas currently emit **{sum(counts.values()):,} property paths across {len(schemas)} families**. This index includes nested arrays and phase payloads, current animal/agent names and the accepted legacy aliases. It is generated with the static manual from the same JSON used by the configuration studio.',
        'Use the field pages for exact type/default/constraint data. Use the [written parameter reference](../../configuration-reference.md) for units, precedence and experimental meaning. The schema cannot replace cross-field production validation or physical calibration.',
        '| Family | Emitted paths | Field atlas |', '| --- | --- | --- |']
    for family, count in counts.items():
        intro.append('| `' + family + '` | ' + str(count) + ' | [' + family.title() + ' fields](schema-' + family + '.md) |')
    intro += ['## Read the contract correctly',
        'A default is the value emitted by the schema generator, not an instruction to inject it into every user JSON file. Opening inherited form controls preserves omission. Scene/prefab defaults and controller application rules remain relevant.',
        'Required means the parent schema emitted that field in its required list. Optional does not mean every combination is executable: production loaders validate modes, references, periods, layer capacity and other cross-field relationships.',
        'UI soft slider bounds, unit labels and visibility/enable rules describe the editor. They do not silently clamp a saved value or delete values hidden by another mode.',
        '## Download the exact JSON',
        '[Current bundled schema JSON](schema-source.json) is copied without changing its bytes. It retains all emitted schema metadata, including UI annotations. [Schema builder source](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Editor/ConfigurationSchemaBuilder.cs) explains generation.']
    (generated / 'schema-index.md').write_text('\n\n'.join(intro) + '\n')
    return counts


def build():
    OUT.mkdir(parents=True, exist_ok=True)
    schema_counts = generate_schema_sources()
    search = []
    source_hashes = {}
    for i, page in enumerate(PAGES):
        source = ROOT / page['source']
        markdown = source.read_text()
        source_hashes[page['source']] = hashlib.sha256(source.read_bytes()).hexdigest()
        if '<!-- GALLERY -->' in markdown:
            markdown = markdown.replace('<!-- GALLERY -->', gallery())
        rendered = subprocess.run(['pandoc', '--from=gfm', '--to=html5', '--wrap=none', '--no-highlight'], input=markdown, text=True, capture_output=True, check=True).stdout
        rewrite = Rewrite(source)
        rewrite.feed(rendered)
        body = ''.join(rewrite.parts)
        sections = Sections()
        sections.feed(body)
        toc = ''.join('<a href="#' + esc(s['id']) + '" class="toc-level-' + str(s['level']) + '">' + esc(s['title']) + '</a>' for s in sections.entries if s['level'] > 1 and s['id'] and (not page['slug'].startswith('schema-') or s['level'] == 2))
        for section in sections.entries:
            text = re.sub(r'\s+', ' ', section['text']).strip()
            search.append({'page': page['title'], 'title': section['title'], 'url': output_name(page) + ('#' + section['id'] if section['id'] else ''), 'text': text[:12000]})
        previous = PAGES[i-1] if i else None
        following = PAGES[i+1] if i+1 < len(PAGES) else None
        prev_link = '<a href="' + output_name(previous) + '"><small>Previous</small>' + esc(previous['title']) + '</a>' if previous else '<span></span>'
        next_link = '<a href="' + output_name(following) + '"><small>Next</small>' + esc(following['title']) + '<span aria-hidden="true"> →</span></a>' if following else '<span></span>'
        source_note = '<aside class="history-note">This guide retains historical integration/validation context. Use <a href="scenes.html">Scenes and capabilities</a> and the current configuration reference for the present default setup.</aside>' if page['slug'] == 'kannadi-history' else ''
        if page['slug'] in {'input-contracts', 'integrations', 'tracker-integration', 'live-monitoring'}:
            source_note += '<aside class="source-note">Current-source additions are marked explicitly. Stable <a href="releases.html">0.3 downloads</a> retain their archived contract; no new release is implied.</aside>'
        output = f'''<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>{esc(page['title'])} · MATREX VR manual</title><meta name="description" content="{esc(page['summary'])}">
<link rel="canonical" href="{BASE_URL}manual/{'' if page['slug']=='index' else output_name(page)}"><meta name="theme-color" content="#102d33">
<link rel="icon" href="../assets/favicon.svg" type="image/svg+xml"><link rel="stylesheet" href="assets/manual.css"><script src="assets/manual.js" defer></script><noscript><style>.search-open,.mobile-menu{{display:none!important}}@media(max-width:800px){{.left-sidebar{{display:block;position:static;width:auto;height:auto;box-shadow:none}}}}</style></noscript></head>
<body><a class="skip-link" href="#article">Skip to content</a>
<header class="docs-header"><a class="docs-brand" href="../index.html"><span class="brand-mark" aria-hidden="true">M</span> MATREX <span>VR</span></a><a class="manual-label" href="index.html">Documentation <span>0.3 + source</span></a><div class="header-actions"><a href="gallery.html">Gallery</a><a href="../index.html#downloads">Downloads</a><a href="{REPO}">Source ↗</a><button type="button" class="mobile-menu" aria-controls="manual-nav" aria-expanded="false">Menu</button></div></header>
<div class="docs-layout"><aside class="left-sidebar" id="manual-nav"><button class="search-open" type="button">{icon('sliders-horizontal')} Search the manual <kbd>/</kbd></button><nav aria-label="Documentation chapters">{navigation(page)}</nav><div class="sidebar-footer"><a href="citation.html">Cite the software</a><a href="{REPO}/blob/main/{page['source']}">Edit this page ↗</a></div></aside>
<main id="article" tabindex="-1"><div class="article-context"><span>{esc(page['group'])}</span><span>MATREX VR / source manual</span></div><article class="prose">{source_note}{body}</article><nav class="chapter-pager" aria-label="Adjacent chapters">{prev_link}{next_link}</nav><footer class="article-footer">Static documentation · software contracts and recorded evidence. <a href="citation.html">Citation and credits</a></footer></main>
<aside class="right-sidebar"><nav aria-label="On this page"><p>On this page</p>{toc}</nav><a class="source-link" href="{REPO}/blob/main/{page['source']}">View Markdown source ↗</a></aside></div>
<dialog class="search-dialog" aria-labelledby="search-title"><form method="dialog"><h2 id="search-title">Search documentation</h2><button class="search-close" aria-label="Close search">×</button></form><label class="sr-only" for="manual-search">Search every manual section</label><input id="manual-search" type="search" placeholder="Try: ViewportSetter, yaw units, replay, sky…" autocomplete="off"><p class="search-status" role="status">Search runs locally in your browser.</p><div class="search-results"></div><p class="search-hint">Enter a term · Tab to a result · Escape to close</p></dialog>
</body></html>'''
        (OUT / output_name(page)).write_text(output)
    sitemap = '<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">' + ''.join('<url><loc>' + BASE_URL + 'manual/' + ('' if page['slug'] == 'index' else output_name(page)) + '</loc></url>' for page in PAGES) + '</urlset>\n'
    (OUT / 'sitemap.xml').write_text(sitemap)
    (OUT / 'search-index.json').write_text(json.dumps(search, ensure_ascii=False, separators=(',', ':')) + '\n')
    (OUT / 'build-info.json').write_text(json.dumps({'generator':'tools/build_docs_site.py','format':'Pre-rendered static HTML with browser-local section search','pages':len(PAGES),'searchSections':len(search),'schemaPropertyPaths':sum(schema_counts.values()),'schemaFamilies':schema_counts,'sourceSha256':source_hashes}, indent=2, ensure_ascii=False)+'\n')
    print(f'Built {len(PAGES)} static manual pages and {len(search)} searchable sections.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.parse_args()
    if not shutil.which('pandoc'):
        raise SystemExit('Install Pandoc to rebuild the committed static manual. Readers do not need it.')
    build()


if __name__ == '__main__':
    main()
