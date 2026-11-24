// Resolve + download the dependency closure for PhotoMind AI's NEW packages
// (xUnit/Moq test stack, EF Core Design + dotnet-ef tool, Magick.NET for HEIC/RAW).
// Reuses the BFS resolver from resolve-packages.mjs; skips files already present
// in .nuget/offline so it only downloads what is missing.
import { writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dir = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(__dir, '..');
const offlineDir = join(repoRoot, '.nuget', 'offline');
mkdirSync(offlineDir, { recursive: true });

const FLAT = 'https://api.nuget.org/v3-flatcontainer';

const ROOTS = [
  ['xunit', '2.9.3'],
  ['xunit.runner.visualstudio', '2.8.2'],
  ['Microsoft.NET.Test.Sdk', '17.12.0'],
  ['Moq', '4.20.72'],
  ['Microsoft.EntityFrameworkCore.Design', '8.0.34'],
  ['dotnet-ef', '8.0.34'],
  ['Magick.NET-Q8-AnyCPU', '13.10.0'],
];

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function fetchJson(url) {
  for (let i = 0; i < 5; i++) {
    try {
      const res = await fetch(url, { redirect: 'follow' });
      if (res.status === 404) return null;
      if (!res.ok) throw new Error('HTTP ' + res.status);
      return await res.json();
    } catch (e) {
      if (i === 4) throw new Error(`${url}: ${e.message}`);
      await sleep(1000 * (i + 1));
    }
  }
}

async function fetchText(url) {
  for (let i = 0; i < 5; i++) {
    try {
      const res = await fetch(url, { redirect: 'follow' });
      if (res.status === 404) return null;
      if (!res.ok) throw new Error('HTTP ' + res.status);
      return await res.text();
    } catch (e) {
      if (i === 4) throw new Error(`${url}: ${e.message}`);
      await sleep(1000 * (i + 1));
    }
  }
}

function parseVersion(v) {
  const plus = v.indexOf('+');
  if (plus >= 0) v = v.slice(0, plus);
  let prerelease = [];
  const dash = v.indexOf('-');
  if (dash >= 0) { prerelease = v.slice(dash + 1).split('.'); v = v.slice(0, dash); }
  const nums = v.split('.').map((s) => parseInt(s, 10) || 0);
  return { nums, prerelease };
}

function cmpVersion(a, b) {
  const va = parseVersion(a), vb = parseVersion(b);
  const len = Math.max(va.nums.length, vb.nums.length);
  for (let i = 0; i < len; i++) {
    const na = va.nums[i] || 0, nb = vb.nums[i] || 0;
    if (na !== nb) return na < nb ? -1 : 1;
  }
  const aPre = va.prerelease.length, bPre = vb.prerelease.length;
  if (aPre === 0 && bPre === 0) return 0;
  if (aPre === 0) return 1;
  if (bPre === 0) return -1;
  const plen = Math.max(aPre, bPre);
  for (let i = 0; i < plen; i++) {
    const pa = va.prerelease[i], pb = vb.prerelease[i];
    if (pa === undefined) return -1;
    if (pb === undefined) return 1;
    const na = /^\d+$/.test(pa) ? parseInt(pa, 10) : pa;
    const nb = /^\d+$/.test(pb) ? parseInt(pb, 10) : pb;
    if (typeof na === 'number' && typeof nb === 'number') {
      if (na !== nb) return na < nb ? -1 : 1;
    } else {
      const s = String(na).localeCompare(String(nb));
      if (s !== 0) return s < 0 ? -1 : 1;
    }
  }
  return 0;
}

function parseRange(range) {
  range = (range || '').trim();
  if (!range || range === '*') return { min: '0.0.0', minInc: true, max: null, maxInc: true };
  if (range.startsWith('[') || range.startsWith('(')) {
    const minInc = range[0] === '[';
    const maxInc = range[range.length - 1] === ']';
    const inner = range.slice(1, -1);
    const parts = inner.split(',');
    const min = parts[0].trim();
    const max = parts.length > 1 ? parts[1].trim() : '';
    return { min: min || '0.0.0', minInc: min ? minInc : true, max: max || null, maxInc: max ? maxInc : true };
  }
  return { min: range, minInc: true, max: null, maxInc: true };
}

function satisfies(version, r) {
  const cMin = cmpVersion(version, r.min);
  if (r.minInc ? cMin < 0 : cMin <= 0) return false;
  if (r.max != null) {
    const cMax = cmpVersion(version, r.max);
    if (r.maxInc ? cMax > 0 : cMax >= 0) return false;
  }
  return true;
}

function resolve(ranges, available) {
  for (const v of available) {
    if (ranges.every((r) => satisfies(v, r))) return v;
  }
  return null;
}

const availableCache = new Map();
async function getAvailable(idLower) {
  if (availableCache.has(idLower)) return availableCache.get(idLower);
  const data = await fetchJson(`${FLAT}/${idLower}/index.json`);
  const versions = (Array.isArray(data) ? data : (data && data.versions) || [])
    .filter((v) => !/\+/.test(v));
  const sorted = versions.slice().sort(cmpVersion);
  availableCache.set(idLower, sorted);
  return sorted;
}

const nuspecCache = new Map();
async function getDeps(idLower, version) {
  const key = `${idLower}@${version}`;
  if (nuspecCache.has(key)) return nuspecCache.get(key);
  const xml = await fetchText(`${FLAT}/${idLower}/${version}/${idLower}.nuspec`);
  if (xml == null) { nuspecCache.set(key, []); return []; }
  const deps = [];
  const depRe = /<dependency\s+[^>]*id="([^"]+)"[^>]*version="([^"]+)"[^>]*\/?>/g;
  let m;
  while ((m = depRe.exec(xml)) !== null) deps.push([m[1], m[2]]);
  const depRe2 = /<dependency\s+[^>]*version="([^"]+)"[^>]*id="([^"]+)"[^>]*\/?>/g;
  while ((m = depRe2.exec(xml)) !== null) deps.push([m[2], m[1]]);
  nuspecCache.set(key, deps);
  return deps;
}

const constraints = new Map();
function addConstraint(idLower, rangeStr) {
  const idL = idLower.toLowerCase();
  if (!constraints.has(idL)) constraints.set(idL, []);
  constraints.get(idL).push(parseRange(rangeStr));
}

const resolved = new Map();
const expanded = new Set();
const dirty = new Set();

for (const [id, v] of ROOTS) {
  addConstraint(id, `[${v}]`);
  dirty.add(id.toLowerCase());
}

while (dirty.size > 0) {
  const idL = dirty.values().next().value;
  dirty.delete(idL);
  const avail = await getAvailable(idL);
  const v = resolve(constraints.get(idL) || [], avail);
  if (!v) throw new Error(`Cannot resolve ${idL} with ${JSON.stringify(constraints.get(idL))}`);
  resolved.set(idL, v);
  const key = `${idL}@${v}`;
  if (!expanded.has(key)) {
    expanded.add(key);
    const deps = await getDeps(idL, v);
    for (const [did, drange] of deps) {
      addConstraint(did, drange);
      dirty.add(did.toLowerCase());
    }
  }
}

console.log('=== RESOLVED NEW PACKAGES (' + resolved.size + ') ===');
const rows = [...resolved.entries()].sort((a, b) => a[0].localeCompare(b[0]));
for (const [id, v] of rows) console.log(`${id} ${v}`);

let ok = 0, fail = 0, skipped = 0;
for (const [idL, v] of rows) {
  const out = join(offlineDir, `${idL}.${v}.nupkg`);
  if (existsSync(out)) { skipped++; continue; }
  const url = `${FLAT}/${idL}/${v}/${idL}.${v}.nupkg`;
  let done = false;
  for (let attempt = 0; attempt < 5 && !done; attempt++) {
    try {
      const res = await fetch(url, { redirect: 'follow' });
      if (!res.ok) throw new Error('HTTP ' + res.status);
      const buf = Buffer.from(await res.arrayBuffer());
      if (buf.length === 0) throw new Error('empty body');
      writeFileSync(out, buf);
      console.log(`downloaded ${idL}.${v}.nupkg (${(buf.length / 1048576).toFixed(1)} MB)`);
      ok++;
      done = true;
    } catch (e) {
      if (attempt === 4) { console.error(`FAILED ${idL}.${v}: ${e.message}`); fail++; }
      else await sleep(1000 * (attempt + 1));
    }
  }
}

console.log(`\nDONE. downloaded=${ok} skipped=${skipped} failed=${fail} total=${resolved.size}`);
process.exit(fail ? 1 : 0);
