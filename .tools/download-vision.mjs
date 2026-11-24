// Robust download of the large vision_model.onnx with HTTP Range resume + retry.
import { existsSync, statSync, mkdirSync, openSync, closeSync, writeSync, renameSync, createReadStream } from 'node:fs';
import { createHash } from 'node:crypto';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const outDir = join(__dirname, '..', '.models', 'clip');
mkdirSync(outDir, { recursive: true });

const url = 'https://hf-mirror.com/Xenova/clip-vit-base-patch32/resolve/main/onnx/vision_model.onnx';
const dest = join(outDir, 'vision_model.onnx');
const part = dest + '.part';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function headSize(u) {
  const r = await fetch(u, { method: 'HEAD', redirect: 'follow' });
  const len = r.headers.get('content-length');
  return len ? Number(len) : null;
}

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

(async () => {
  if (existsSync(dest) && statSync(dest).size > 0) {
    console.log('ALREADY EXISTS', dest, statSync(dest).size);
    return;
  }

  let total = await headSize(url);
  console.log('HEAD content-length =', total);

  let offset = existsSync(part) ? statSync(part).size : 0;
  console.log('starting offset =', offset);

  let attempts = 0;
  while (total === null || offset < total) {
    attempts++;
    if (attempts > 500) {
      console.log('TOO MANY ATTEMPTS, giving up at offset', offset);
      process.exit(1);
    }

    const headers = offset > 0 ? { Range: `bytes=${offset}-` } : {};
    let resp;
    try {
      resp = await fetch(url, { redirect: 'follow', headers });
    } catch (e) {
      console.log(`[attempt ${attempts}] fetch ERR: ${e.message}; offset=${offset}; retry in 5s`);
      await sleep(5000);
      continue;
    }

    if (resp.status === 416) {
      // Range not satisfiable — we already have the whole file.
      console.log('416 -> already complete');
      offset = total;
      break;
    }
    if (resp.status !== 200 && resp.status !== 206) {
      console.log(`[attempt ${attempts}] status ${resp.status}; retry in 5s`);
      await sleep(5000);
      continue;
    }

    if (resp.status === 200 && offset > 0) {
      console.log(`[attempt ${attempts}] server ignored Range, restarting from 0`);
      offset = 0;
    }

    const cl = resp.headers.get('content-length');
    if (resp.status === 206) {
      const range = resp.headers.get('content-range'); // bytes X-Y/TOTAL
      if (range) {
        const m = range.match(/\/(\d+)$/);
        if (m) total = Number(m[1]);
      }
      console.log(`[attempt ${attempts}] resume 206, chunk=${cl}, total=${total}`);
    } else {
      if (cl) total = Number(cl);
      console.log(`[attempt ${attempts}] fresh 200, total=${total}`);
    }

    let fd;
    try {
      fd = openSync(part, offset === 0 ? 'w' : 'a');
      const reader = resp.body.getReader();
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        let written = 0;
        while (written < value.length) {
          written += writeSync(fd, value, written, value.length - written);
        }
        offset += value.length;
      }
      closeSync(fd);
      console.log(`  chunk done, offset=${offset}`);
    } catch (e) {
      try { closeSync(fd); } catch {}
      console.log(`[attempt ${attempts}] stream ERR: ${e.message}; offset=${offset}; retry in 5s`);
      await sleep(5000);
      continue;
    }

    if (total !== null && offset >= total) break;
    if (total !== null && offset < total) {
      console.log(`  incomplete (${offset}/${total}), continuing`);
    }
  }

  if (total !== null && offset >= total) {
    renameSync(part, dest);
    const sz = statSync(dest).size;
    const h = await sha256(dest);
    console.log(`DONE ${sz} bytes sha256=${h}`);
  } else {
    console.log('FAILED: could not determine completion');
    process.exit(1);
  }
})();
