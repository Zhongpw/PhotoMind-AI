// Downloads real photos from Wikimedia Commons for CLIP validation.
// Organizes into validation/<topAlias>/<subAlias>/NN.jpg (sub leaf) or validation/<topAlias>/NN.jpg (no subs).
// Throttles and retries on 429/5xx. Honest coverage: targets a few images per leaf category.
import { mkdirSync, writeFileSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';

const OUT = process.argv[2] || '.tools/clip-eval/validation';
const PER_LEAF = parseInt(process.argv[3] || '5', 10);

// leaf -> [topAlias, subAlias, search terms...] (subAlias null => top-level only)
const CATS = [
  ['nature', 'the ocean', 'ocean waves', 'beach sea', 'ocean'],
  ['nature', 'mountains', 'mountain landscape', 'mountain peak', 'snow mountain'],
  ['nature', 'flowers', 'flowers', 'flower garden', 'blossom'],
  ['nature', 'forest', 'forest trees', 'forest', 'woods'],
  ['nature', 'the sky', 'sky clouds', 'sunset sky', 'blue sky'],
  ['everyday life', 'daily routine', 'breakfast table', 'coffee cup', 'reading book'],
  ['everyday life', 'a party', 'birthday party', 'party celebration', 'wedding'],
  ['everyday life', 'family', 'family together', 'children playing', 'family photo'],
  ['everyday life', 'travel', 'suitcase travel', 'airport', 'travel'],
  ['animals', 'a cat', 'cat', 'kitten', 'house cat'],
  ['animals', 'a dog', 'dog', 'puppy', 'golden retriever'],
  ['animals', 'a bird', 'bird', 'parrot', 'sparrow'],
  ['animals', 'other animals', 'squirrel', 'butterfly', 'wildlife'],
  ['architecture', 'a city', 'city skyline', 'city street', 'downtown'],
  ['architecture', 'an indoor room', 'room interior', 'living room', 'bedroom'],
  ['architecture', 'a landmark', 'landmark', 'eiffel tower', 'monument'],
  ['food', 'Chinese food', 'chinese food', 'noodles', 'dumplings'],
  ['food', 'Western food', 'steak', 'pasta', 'burger'],
  ['food', 'dessert', 'dessert', 'cake', 'ice cream'],
  ['food', 'drinks', 'coffee', 'tea cup', 'juice'],
  ['miscellaneous', null, 'abstract pattern', 'texture background', 'random object'],
];

const sleep = (ms) => new Promise(r => setTimeout(r, ms));

async function apiJson(params) {
  const url = 'https://commons.wikimedia.org/w/api.php?' + new URLSearchParams(params).toString();
  for (let attempt = 0; attempt < 6; attempt++) {
    const res = await fetch(url, { headers: { 'User-Agent': 'PhotoMind-clip-eval/1.0 (calibration; contact dev@photomind.local)' } });
    if (res.status === 429) {
      const wait = 8000 * (attempt + 1);
      process.stderr.write(`  429 on api, backoff ${wait}ms\n`);
      await sleep(wait);
      continue;
    }
    if (!res.ok) throw new Error(`api HTTP ${res.status}`);
    return await res.json();
  }
  throw new Error('api: too many 429s');
}

async function download(url, dest) {
  for (let attempt = 0; attempt < 5; attempt++) {
    try {
      const res = await fetch(url, { headers: { 'User-Agent': 'PhotoMind-clip-eval/1.0 (calibration; contact dev@photomind.local)' } });
      if (res.status === 429) { await sleep(6000 * (attempt + 1)); continue; }
      if (!res.ok) return false;
      const buf = Buffer.from(await res.arrayBuffer());
      if (buf.length < 2000) return false; // too small / error page
      writeFileSync(dest, buf);
      return true;
    } catch (e) {
      process.stderr.write(`   dl err: ${e.message}\n`);
      await sleep(3000);
    }
  }
  return false;
}

let total = 0, ok = 0;
for (const [top, sub, ...terms] of CATS) {
  const leafDir = sub ? join(OUT, top, sub) : join(OUT, top);
  mkdirSync(leafDir, { recursive: true });
  const existing = existsSync(leafDir) ? (await import('node:fs')).readdirSync(leafDir).filter(f => f.endsWith('.jpg')).length : 0;
  if (existing >= PER_LEAF) {
    process.stderr.write(`skip ${top}/${sub ?? '-'} (already ${existing})\n`);
    continue;
  }

  let downloaded = existing;
  for (const term of terms) {
    if (downloaded >= PER_LEAF) break;
    try {
      const data = await apiJson({
        action: 'query', generator: 'search', gsrsearch: term, gsrnamespace: '6', gsrlimit: '6',
        prop: 'imageinfo', iiprop: 'url', iiurlwidth: '400', format: 'json',
      });
      const pages = (data?.query?.pages) ? Object.values(data.query.pages) : [];
      for (const p of pages) {
        if (downloaded >= PER_LEAF) break;
        const thumb = p?.imageinfo?.[0]?.thumburl;
        if (!thumb) continue;
        const dest = join(leafDir, `${String(downloaded + 1).padStart(2, '0')}.jpg`);
        if (await download(thumb, dest)) {
          downloaded++;
          ok++;
          total++;
          process.stderr.write(`ok  ${top}/${sub ?? '-'}/${downloaded}  <- ${term}\n`);
        }
        await sleep(900);
      }
    } catch (e) {
      process.stderr.write(`err ${top}/${sub ?? '-'} term=${term}: ${e.message}\n`);
    }
    await sleep(1500);
  }
  process.stderr.write(`done ${top}/${sub ?? '-'}: ${downloaded} images\n`);
}
process.stderr.write(`DONE total=${total} ok=${ok}\n`);
