/* Download links come only from uploaded assets of a published GitHub release. */
(function () {
  'use strict';
  const repository = 'pvnkmrksk/matrexVR';
  const releasesUrl = 'https://github.com/' + repository + '/releases';
  const platforms = {
    windows: { label: 'Windows ZIP', match: /(?:windows|win64|win-x64)/i },
    macos: { label: 'macOS ZIP', match: /(?:macos|osx|darwin)/i },
    linux: { label: 'Linux ZIP', match: /linux/i }
  };

  function availableReleases(data) {
    if (!Array.isArray(data)) return [];
    return data.filter(function (release) {
      return release && !release.draft && !release.prerelease && release.published_at &&
        /^v?\d+\.\d+(?:\.\d+)?$/.test(release.tag_name || '');
    }).sort(function (a, b) {
      return b.tag_name.replace(/^v/, '').localeCompare(a.tag_name.replace(/^v/, ''), undefined, { numeric: true });
    });
  }

  function platformAsset(release, os) {
    if (!release || !platforms[os]) return null;
    const matches = (Array.isArray(release.assets) ? release.assets : []).filter(function (asset) {
      return asset && asset.state === 'uploaded' && asset.size > 0 &&
        /\.zip$/i.test(asset.name || '') && platforms[os].match.test(asset.name) &&
        typeof asset.browser_download_url === 'string' &&
        asset.browser_download_url.startsWith('https://github.com/' + repository + '/releases/download/');
    });
    if (os === 'macos') matches.sort(function (a, b) { return Number(/universal/i.test(b.name)) - Number(/universal/i.test(a.name)); });
    return matches[0] || null;
  }

  function formatSize(bytes) {
    return bytes >= 1024 * 1024 * 1024 ? (bytes / (1024 * 1024 * 1024)).toFixed(1) + ' GB' :
      Math.max(1, Math.round(bytes / (1024 * 1024))) + ' MB';
  }

  if (typeof module === 'object' && module.exports) {
    module.exports = { availableReleases: availableReleases, platformAsset: platformAsset, formatSize: formatSize };
    return;
  }

  const select = document.getElementById('release-select');
  const status = document.getElementById('download-status');
  const notes = document.getElementById('release-notes');
  if (!select || !status || !notes) return;

  function showRelease(release) {
    let found = 0;
    const macDetails = document.querySelector('[data-architecture="macos"]');
    const releaseUrl = releasesUrl + '/tag/' + encodeURIComponent(release.tag_name);
    notes.href = releaseUrl;
    notes.textContent = 'Release notes ↗';
    Object.keys(platforms).forEach(function (os) {
      const link = document.querySelector('[data-os="' + os + '"]');
      const size = document.querySelector('[data-size="' + os + '"]');
      const asset = platformAsset(release, os);
      if (asset) {
        found += 1;
        link.href = asset.browser_download_url;
        link.textContent = 'Download ' + platforms[os].label + ' ↓';
        link.setAttribute('aria-label', 'Download ' + release.tag_name + ' ' + platforms[os].label + ', ' + formatSize(asset.size));
        size.textContent = asset.name + ' · ' + formatSize(asset.size);
        if (os === 'macos' && macDetails) macDetails.textContent = /universal/i.test(asset.name) ? 'Apple silicon & Intel' : /arm64|apple-silicon/i.test(asset.name) ? 'Apple silicon' : /x86_64|intel/i.test(asset.name) ? 'Intel' : 'See architecture in release notes';
      } else {
        link.href = releaseUrl;
        link.textContent = 'View release ↗';
        link.setAttribute('aria-label', 'No uploaded ' + os + ' ZIP for ' + release.tag_name + '; view release details');
        size.textContent = 'No uploaded ZIP for this OS in this release';
        if (os === 'macos' && macDetails) macDetails.textContent = 'Desktop player';
      }
    });
    status.textContent = release.tag_name + ' · ' + found + ' of 3 operating-system archives available. ' +
      'Published ' + new Date(release.published_at).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) + '.';
  }

  async function loadReleases() {
    const controller = new AbortController();
    const timeout = setTimeout(function () { controller.abort(); }, 8000);
    try {
      const response = await fetch('https://api.github.com/repos/' + repository + '/releases?per_page=50', {
        headers: { Accept: 'application/vnd.github+json' }, signal: controller.signal, cache: 'no-store'
      });
      if (!response.ok) throw new Error('Release API ' + response.status);
      const releases = availableReleases(await response.json());
      select.replaceChildren();
      if (!releases.length) {
        const empty = document.createElement('option');
        empty.textContent = 'No published release available';
        select.appendChild(empty);
        status.textContent = 'No published release assets are available yet. Check GitHub Releases for publication status.';
        return;
      }
      releases.forEach(function (release, index) {
        const option = document.createElement('option');
        option.value = String(index);
        option.textContent = release.tag_name + (index === 0 ? ' · latest' : '');
        select.appendChild(option);
      });
      select.disabled = false;
      select.addEventListener('change', function () { showRelease(releases[Number(select.value)]); });
      showRelease(releases[0]);
    } catch (error) {
      select.replaceChildren();
      const unavailable = document.createElement('option');
      unavailable.textContent = 'Choose a release on GitHub';
      select.appendChild(unavailable);
      status.textContent = 'Release discovery is unavailable in this browser. Use GitHub Releases to choose an uploaded OS archive.';
    } finally {
      clearTimeout(timeout);
    }
  }
  loadReleases();
}());
