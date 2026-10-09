// Static server for the WebGL build: node tools/serve-webgl.js [port]  ->  http://localhost:5180
// The build uses gzip + decompression fallback, so plain octet-stream for .unityweb is fine.
const http = require("http"), fs = require("fs"), path = require("path");
const root = path.join(__dirname, "..", "Builds", "WebGL");
const port = +process.argv[2] || 5180;
const types = { ".html": "text/html", ".js": "application/javascript", ".css": "text/css", ".png": "image/png", ".ico": "image/x-icon", ".wasm": "application/wasm", ".json": "application/json" };
http.createServer((req, res) => {
  const rel = decodeURIComponent(req.url.split("?")[0]);
  const file = path.join(root, rel === "/" ? "index.html" : rel);
  if (!file.startsWith(root)) { res.writeHead(403); return res.end(); }
  fs.readFile(file, (err, buf) => {
    if (err) { res.writeHead(404); return res.end("not found"); }
    res.writeHead(200, { "Content-Type": types[path.extname(file)] || "application/octet-stream", "Cache-Control": "no-cache" });
    res.end(buf);
  });
}).listen(port, () => console.log(`WebGL build at http://localhost:${port}`));
