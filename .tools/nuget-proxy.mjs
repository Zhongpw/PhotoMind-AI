// NuGet reverse proxy: forwards to https://api.nuget.org using Node's undici fetch (OpenSSL),
// which works on this machine where Windows Schannel cannot reach nuget.org.
// NuGet is pointed at http://127.0.0.1:PORT/v3/index.json and all traffic is relayed here.
import http from 'node:http';

const PORT = Number(process.env.PORT || 18080);
const UPSTREAM_HOST = 'api.nuget.org';
const UPSTREAM_ORIGIN = `https://${UPSTREAM_HOST}`;
const LOCAL_ORIGIN = `http://127.0.0.1:${PORT}`;

const server = http.createServer(async (req, res) => {
  try {
    const headers = {
      'accept-encoding': 'identity',
      'user-agent': req.headers['user-agent'] || 'PhotoMindAI-nuget-proxy',
      'range': req.headers['range'] || '',
      'if-none-match': req.headers['if-none-match'] || '',
    };
    for (const k of ['range', 'if-none-match']) if (!headers[k]) delete headers[k];

    const up = await fetch(UPSTREAM_ORIGIN + req.url, {
      method: req.method,
      headers,
      redirect: 'follow',
    });

    let body = Buffer.from(await up.arrayBuffer());
    const ct = up.headers.get('content-type') || '';
    if (/(json|text|xml)/i.test(ct)) {
      body = Buffer.from(body.toString('utf8').split(UPSTREAM_ORIGIN).join(LOCAL_ORIGIN), 'utf8');
    }

    const respHeaders = {};
    for (const [k, v] of up.headers) {
      const lk = k.toLowerCase();
      if (['content-encoding', 'content-length', 'transfer-encoding', 'connection', 'keep-alive'].includes(lk)) continue;
      respHeaders[k] = v;
    }
    respHeaders['content-length'] = body.length;
    res.writeHead(up.status, respHeaders);
    res.end(body);
  } catch (e) {
    console.error(`[proxy] error ${req.url}: ${e.message}`);
    if (!res.headersSent) {
      res.writeHead(502, { 'content-type': 'text/plain; charset=utf-8' });
      res.end(`upstream error: ${e.message}`);
    }
  }
});

server.listen(PORT, '127.0.0.1', () => {
  console.log(`[proxy] listening ${LOCAL_ORIGIN} -> ${UPSTREAM_ORIGIN}`);
});
