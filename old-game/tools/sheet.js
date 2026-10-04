// Zero-dependency PNG decode/crop/scale/encode for spritesheet extraction.
// Usage:
//   node sheet.js info <src>
//   node sheet.js crop <src> <x> <y> <w> <h> <scale> <out> [grid]
//     grid: optional cell size — draws magenta gridlines every N*scale px
const fs = require("fs");
const zlib = require("zlib");

function decodePNG(file) {
  const buf = fs.readFileSync(file);
  if (buf.readUInt32BE(0) !== 0x89504e47) throw new Error("not a png");
  let pos = 8, ihdr = null, idat = [], plte = null, trns = null;
  while (pos < buf.length) {
    const len = buf.readUInt32BE(pos), type = buf.toString("ascii", pos + 4, pos + 8);
    const data = buf.slice(pos + 8, pos + 8 + len);
    if (type === "IHDR") ihdr = {
      w: data.readUInt32BE(0), h: data.readUInt32BE(4),
      depth: data[8], color: data[9], interlace: data[12],
    };
    else if (type === "IDAT") idat.push(data);
    else if (type === "PLTE") plte = data;
    else if (type === "tRNS") trns = data;
    else if (type === "IEND") break;
    pos += 12 + len;
  }
  if (ihdr.interlace) throw new Error("interlaced png unsupported");
  if (ihdr.depth !== 8) throw new Error("bit depth " + ihdr.depth + " unsupported");
  const raw = zlib.inflateSync(Buffer.concat(idat));
  const ch = { 0: 1, 2: 3, 3: 1, 4: 2, 6: 4 }[ihdr.color];
  const stride = ihdr.w * ch;
  const out = Buffer.alloc(ihdr.h * stride);
  let p = 0;
  for (let y = 0; y < ihdr.h; y++) {
    const filter = raw[p++];
    const line = raw.slice(p, p + stride); p += stride;
    const prev = y ? out.slice((y - 1) * stride, y * stride) : Buffer.alloc(stride);
    const cur = out.slice(y * stride, (y + 1) * stride);
    for (let i = 0; i < stride; i++) {
      const a = i >= ch ? cur[i - ch] : 0, b = prev[i], c = i >= ch ? prev[i - ch] : 0;
      let v = line[i];
      if (filter === 1) v += a;
      else if (filter === 2) v += b;
      else if (filter === 3) v += (a + b) >> 1;
      else if (filter === 4) {
        const pp = a + b - c, pa = Math.abs(pp - a), pb = Math.abs(pp - b), pc = Math.abs(pp - c);
        v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
      }
      cur[i] = v & 0xff;
    }
  }
  // normalize to RGBA
  const rgba = Buffer.alloc(ihdr.w * ihdr.h * 4);
  for (let i = 0; i < ihdr.w * ihdr.h; i++) {
    if (ihdr.color === 6) { out.copy(rgba, i * 4, i * 4, i * 4 + 4); }
    else if (ihdr.color === 2) { rgba[i*4]=out[i*3]; rgba[i*4+1]=out[i*3+1]; rgba[i*4+2]=out[i*3+2]; rgba[i*4+3]=255; }
    else if (ihdr.color === 3) { const ix=out[i]; rgba[i*4]=plte[ix*3]; rgba[i*4+1]=plte[ix*3+1]; rgba[i*4+2]=plte[ix*3+2]; rgba[i*4+3]=trns&&ix<trns.length?trns[ix]:255; }
    else if (ihdr.color === 0) { rgba[i*4]=rgba[i*4+1]=rgba[i*4+2]=out[i]; rgba[i*4+3]=255; }
    else if (ihdr.color === 4) { rgba[i*4]=rgba[i*4+1]=rgba[i*4+2]=out[i*2]; rgba[i*4+3]=out[i*2+1]; }
  }
  return { w: ihdr.w, h: ihdr.h, rgba };
}

const CRC_TABLE = (() => {
  const t = new Int32Array(256);
  for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; t[n] = c; }
  return t;
})();
function crc32(buf) {
  let c = ~0;
  for (const b of buf) c = CRC_TABLE[(c ^ b) & 0xff] ^ (c >>> 8);
  return ~c >>> 0;
}
function chunk(type, data) {
  const out = Buffer.alloc(12 + data.length);
  out.writeUInt32BE(data.length, 0);
  out.write(type, 4, "ascii");
  data.copy(out, 8);
  out.writeUInt32BE(crc32(Buffer.concat([Buffer.from(type, "ascii"), data])), 8 + data.length);
  return out;
}
function encodePNG(w, h, rgba) {
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4);
  ihdr[8] = 8; ihdr[9] = 6;                     // RGBA8
  const raw = Buffer.alloc(h * (1 + w * 4));
  for (let y = 0; y < h; y++) rgba.copy(raw, y * (1 + w * 4) + 1, y * w * 4, (y + 1) * w * 4);
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk("IHDR", ihdr), chunk("IDAT", zlib.deflateSync(raw, { level: 9 })), chunk("IEND", Buffer.alloc(0)),
  ]);
}

const [, , cmd, src, ...args] = process.argv;
const img = decodePNG(src);
if (cmd === "info") { console.log(JSON.stringify({ w: img.w, h: img.h })); process.exit(0); }
if (cmd === "crop") {
  const [x, y, w, h, scale, out, grid] = [+args[0], +args[1], +args[2], +args[3], +args[4], args[5], +args[6] || 0];
  const ow = w * scale, oh = h * scale;
  const rgba = Buffer.alloc(ow * oh * 4);
  for (let oy = 0; oy < oh; oy++) for (let ox = 0; ox < ow; ox++) {
    const sx = x + Math.floor(ox / scale), sy = y + Math.floor(oy / scale);
    const si = (sy * img.w + sx) * 4, di = (oy * ow + ox) * 4;
    if (sx < img.w && sy < img.h) img.rgba.copy(rgba, di, si, si + 4);
    if (grid && (ox % (grid * scale) === 0 || oy % (grid * scale) === 0)) {
      rgba[di] = 255; rgba[di + 1] = 0; rgba[di + 2] = 255; rgba[di + 3] = 255;
    }
  }
  fs.writeFileSync(out, encodePNG(ow, oh, rgba));
  console.log("wrote", out, ow + "x" + oh);
}
