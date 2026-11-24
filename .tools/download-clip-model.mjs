// Download CLIP ViT-B/32 ONNX model + tokenizer from hf-mirror.com
// Usage: node download-clip-model.mjs
import { createWriteStream, mkdirSync, existsSync, statSync, createReadStream, renameSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { pipeline } from 'node:stream/promises';
import { Readable } from 'node:stream';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const outDir = join(__dirname, '..', '.models', 'clip');
mkdirSync(outDir, { recursive: true });

const BASE = 'https://hf-mirror.com/Xenova/clip-vit-base-patch32/resolve/main';
const files = [
  ['onnx/vision_model.onnx', 'vision_model.onnx'],
  ['onnx/text_model.onnx', 'text_model.onnx'],
  ['vocab.json', 'vocab.json'],
  ['merges.txt', 'merges.txt'],
  ['config.json', 'config.json'],
  ['preprocessor_config.json', 'preprocessor_config.json'],
];

async function sha256(file) {
  const h = createHash('sha256');
  await new Promise((res, rej) => {
    const s = createReadStream(file);
    s.on('data', (d) => h.update(d));
    s.on('end', res);
    s.on('error', rej);
  });
  return h.digest('hex');
}

let failed = 0;
for (const [remote, local] of files) {
  const dest = join(outDir, local);
  if (existsSync(dest) && statSync(dest).size > 0) {
    console.log('SKIP (exists)', local);
    continue;
  }
  const url = `${BASE}/${remote}`;
  console.log('DOWNLOAD', local, '<-', url);
  try {
    const resp = await fetch(url, { redirect: 'follow' });
    if (!resp.ok) { console.log('  FAIL status', resp.status, local); failed++; continue; }
    const tmp = dest + '.part';
    await pipeline(Readable.fromWeb(resp.body), createWriteStream(tmp));
    renameSync(tmp, dest);
    const sz = statSync(dest).size;
    const h = await sha256(dest);
    console.log('  OK', (sz / 1024 / 1024).toFixed(1) + 'MB', 'sha256=' + h);
  } catch (e) {
    console.log('  ERR', e.message, local);
    failed++;
  }
}
console.log(`DONE failed=${failed}`);
process.exit(failed === 0 ? 0 : 1);
