(function () {
  'use strict';
  if (typeof module === 'object' && module.exports) { module.exports = { search: search }; return; }
  const menu = document.querySelector('.mobile-menu');
  const sidebar = document.getElementById('manual-nav');
  menu?.addEventListener('click', function () {
    const open = sidebar.classList.toggle('open');
    menu.setAttribute('aria-expanded', String(open));
  });
  document.addEventListener('click', function (event) {
    if (sidebar?.classList.contains('open') && !sidebar.contains(event.target) && !menu.contains(event.target)) {
      sidebar.classList.remove('open'); menu.setAttribute('aria-expanded', 'false');
    }
  });

  const dialog = document.querySelector('.search-dialog');
  const input = document.getElementById('manual-search');
  const results = document.querySelector('.search-results');
  const status = document.querySelector('.search-status');
  let index;
  let loading;
  async function loadIndex() {
    if (index) return index;
    if (!loading) loading = fetch('search-index.json').then(function (response) {
      if (!response.ok) throw new Error('Search index unavailable');
      return response.json();
    }).then(function (data) { index = data; return data; }).catch(function () {
      status.textContent = 'Search could not load. Use the chapter navigation or open this manual through the website.';
      loading = undefined; return [];
    });
    return loading;
  }
  function search(query, entries) {
    const terms = query.toLocaleLowerCase().trim().split(/\s+/).filter(Boolean);
    if (!terms.length) return [];
    return entries.map(function (entry) {
      const title = (entry.page + ' ' + entry.title).toLocaleLowerCase();
      const body = entry.text.toLocaleLowerCase();
      const valid = terms.every(function (term) { return title.includes(term) || body.includes(term); });
      const score = valid ? terms.reduce(function (sum, term) { return sum + (title.includes(term) ? 12 : 1); }, 0) : 0;
      return { entry: entry, score: score };
    }).filter(function (item) { return item.score > 0; }).sort(function (a, b) { return b.score - a.score; }).slice(0, 10);
  }
  async function renderSearch() {
    const query = input.value.trim();
    results.replaceChildren();
    if (!query) { status.textContent = 'Search every chapter and section. Nothing leaves your browser.'; return; }
    const entries = await loadIndex();
    if (input.value.trim() !== query) return;
    const matches = search(query, entries);
    if (!entries.length) return;
    status.textContent = matches.length ? 'Best matching sections:' : 'No matching section. Try fewer words or a component name.';
    matches.forEach(function (item) {
      const entry = item.entry;
      const link = document.createElement('a'); link.href = entry.url;
      const page = document.createElement('small'); page.textContent = entry.page;
      const title = document.createElement('strong'); title.textContent = entry.title;
      const snippet = document.createElement('p');
      const lower = entry.text.toLocaleLowerCase();
      const term = query.toLocaleLowerCase().split(/\s+/)[0];
      const at = Math.max(0, lower.indexOf(term) - 55);
      snippet.textContent = (at ? '…' : '') + entry.text.slice(at, at + 190) + (entry.text.length > at + 190 ? '…' : '');
      link.append(page, title, snippet); results.append(link);
    });
  }
  function openSearch() {
    if (!dialog.open) dialog.showModal();
    input.focus(); loadIndex(); renderSearch();
  }
  document.querySelector('.search-open')?.addEventListener('click', openSearch);
  input?.addEventListener('input', renderSearch);
  document.addEventListener('keydown', function (event) {
    const editing = /^(INPUT|TEXTAREA|SELECT)$/.test(document.activeElement?.tagName || '') || document.activeElement?.isContentEditable;
    if (event.key === '/' && !editing) { event.preventDefault(); openSearch(); }
    if (event.key === 'Escape' && sidebar?.classList.contains('open')) { sidebar.classList.remove('open'); menu.setAttribute('aria-expanded', 'false'); }
  });
  dialog?.addEventListener('click', function (event) { if (event.target === dialog) dialog.close(); });

  document.querySelectorAll('.prose pre').forEach(function (block) {
    if (!navigator.clipboard?.writeText) return;
    const button = document.createElement('button'); button.className = 'code-copy'; button.type = 'button'; button.textContent = 'Copy';
    button.setAttribute('aria-label', 'Copy code block');
    button.addEventListener('click', async function () {
      try { await navigator.clipboard.writeText(block.querySelector('code')?.textContent || ''); button.textContent = 'Copied'; }
      catch { button.textContent = 'Select text'; }
      setTimeout(function () { button.textContent = 'Copy'; }, 1600);
    });
    block.append(button);
  });

  if ('IntersectionObserver' in window) {
    const links = Array.from(document.querySelectorAll('.right-sidebar nav a'));
    const observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        links.forEach(function (link) { link.classList.toggle('active', link.hash.slice(1) === entry.target.id); });
      });
    }, { rootMargin: '-90px 0px -65% 0px' });
    document.querySelectorAll('.prose h2[id], .prose h3[id]').forEach(function (heading) { observer.observe(heading); });
  }
}());
