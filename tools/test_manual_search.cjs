// Unit tests for browser-local documentation search ranking, independent of UI control.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const { search } = require('../docs/site/manual/assets/manual.js');
const entries = JSON.parse(fs.readFileSync(require('node:path').join(__dirname, '../docs/site/manual/search-index.json'), 'utf8'));
assert.equal(search('', entries).length, 0);
assert.equal(search('no_such_component_987654', entries).length, 0);
assert(search('ViewportSetter', entries).some(x => x.entry.url.startsWith('viewports.html')));
assert(search('closedloop', entries).some(x => x.entry.url.startsWith('closed-loop.html')));
assert(search('sphereDiameter', entries).some(x => x.entry.text.includes('sphereDiameter')));
assert(search('ಕನ್ನಡಿ', entries).some(x => /Kannadi|scenes/i.test(x.entry.url + x.entry.text)));
assert(search('replay timeline', entries).every(x => /replay/i.test(x.entry.page + x.entry.title + x.entry.text) && /timeline/i.test(x.entry.page + x.entry.title + x.entry.text)));
assert(search('the', entries).length <= 10);
const fixture = [
  {page:'Body only',title:'Other',url:'body.html',text:'ViewportSetter maps cameras'},
  {page:'ViewportSetter',title:'Projection',url:'title.html',text:'Exact rectangle mapping'}
];
assert.equal(search('viewportsetter', fixture)[0].entry.url, 'title.html');
assert.equal(search('  ViewPortSetter  ', fixture)[0].entry.url, 'title.html');
console.log('PASS: 10 manual search assertions, including ranking, case, all-term matches, Kannada, coverage and empty/no-match results.');
