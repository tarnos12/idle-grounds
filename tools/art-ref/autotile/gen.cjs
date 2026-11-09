// Idle Grounds auto-tile templates (ART-SPEC 3.10). Usage: node tools/art-ref/autotile/gen.cjs <outdir>; then
// powershell -File tools/art-ref/autotile/label.ps1 -dir <outdir> and label2.ps1 for the labelled guides.
// Wang-16 corner tiles: index = NW*1 + NE*2 + SW*4 + SE*8 (1 = terrain); sheet layout and alpha are generated here.
const P=require(require('path').join(__dirname,'png.cjs'));
const OUT=process.argv[2]||'.';const path=require('path');const _w=P.write;P.write=(f,...a)=>_w(path.join(OUT,f),...a);
const GRID=["01000","01110","11101","00100","01000"].map(r=>r.split('').map(Number));
const T=32,THR=0.684,A=0.06;
const s=t=>t*t*(3-2*t);
function tileMask(nw,ne,sw,se){const m=[];for(let y=0;y<T;y++){m.push([]);for(let x=0;x<T;x++){const u=(x+0.5)/T,v=(y+0.5)/T,su=s(u),sv=s(v);
 const F=nw*(1-su)*(1-sv)+ne*su*(1-sv)+sw*(1-su)*sv+se*su*sv;
 const p=A*(Math.sin(2*Math.PI*u)*Math.sin(Math.PI*v)+Math.sin(Math.PI*u)*Math.sin(2*Math.PI*v));
 m[y].push(F>=THR+p?1:0);}}return m;}
// sheet
const W=128,H=160;const sheet=[...Array(H)].map(()=>Array(W).fill(0));const tiles={};const layout=[];
for(let r=0;r<4;r++){layout.push([]);for(let k=0;k<4;k++){const nw=GRID[r][k],ne=GRID[r][k+1],sw=GRID[r+1][k],se=GRID[r+1][k+1];const idx=nw|ne<<1|sw<<2|se<<3;layout[r].push(idx);const m=tileMask(nw,ne,sw,se);tiles[idx]=m;for(let y=0;y<T;y++)for(let x=0;x<T;x++)sheet[r*T+y][k*T+x]=m[y][x];}}
for(let k=0;k<4;k++)for(let y=0;y<T;y++)for(let x=0;x<T;x++)sheet[128+y][k*T+x]=1;
// rim classification (B pixel within 2 px chebyshev of an A pixel, treating sheet tile-locally with wrap through neighbours in the continuous picture; row 4 none)
function cls(img,w,h){const out=img.map(r=>r.slice());for(let y=0;y<h;y++)for(let x=0;x<w;x++){if(!img[y][x])continue;let rim=0;for(let dy=-2;dy<=2;dy++)for(let dx=-2;dx<=2;dx++){const yy=y+dy,xx=x+dx;if(yy<0||xx<0||yy>=h||xx>=w)continue;if(!img[yy][xx]&&Math.abs(dx)+Math.abs(dy)<=2)rim=1;}out[y][x]=rim?2:1;}return out;}
// only rows 0..127 are a continuous picture; rows 128.. are fill
const sc=cls(sheet,W,H);for(let y=128;y<H;y++)for(let x=0;x<W;x++)sc[y][x]=1;
function toRGBA(c,w,h,pal){const b=Buffer.alloc(w*h*4);for(let y=0;y<h;y++)for(let x=0;x<w;x++){const o=(y*w+x)*4;const p=pal[c[y][x]];b[o]=p[0];b[o+1]=p[1];b[o+2]=p[2];b[o+3]=p[3];}return b;}
// exact alpha template: B opaque white, A transparent
P.write('autotile_wang16_alpha_128x160.png',W,H,toRGBA(sheet,W,H,[[0,0,0,0],[255,255,255,255]]));
// guide colours: A = transparent, B core = P?? light jade, B rim = darker
P.write('autotile_wang16_guide_128x160.png',W,H,toRGBA(sc,W,H,[[0,0,0,0],[134,179,74,255],[63,110,52,255]]));
// compose 47 blob
const order=[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,19,23,27,31,38,39,46,47,55,63,76,77,78,79,95,110,111,127,137,139,141,143,155,159,175,191,205,207,223,239,255];
const N=1,E=2,S=4,Wb=8,NE=16,SE=32,SW=64,NWb=128;
const q=(m,b)=>(m&b)?1:0;
function blobCell(m){const c=[...Array(T)].map(()=>Array(T).fill(0));const h=T/2;
 const put=(idx,sx,sy,dx,dy)=>{const t=tiles[idx];for(let y=0;y<h;y++)for(let x=0;x<h;x++)c[dy+y][dx+x]=t[sy+y][sx+x];};
 const id=(nw,ne,sw,se)=>nw|ne<<1|sw<<2|se<<3;
 put(id(q(m,NWb),q(m,N),q(m,Wb),1),h,h,0,0);   // NW quadrant <- SE quadrant
 put(id(q(m,N),q(m,NE),1,q(m,E)),0,h,h,0);     // NE quadrant <- SW quadrant
 put(id(q(m,Wb),1,q(m,SW),q(m,S)),h,0,0,h);    // SW quadrant <- NE quadrant
 put(id(1,q(m,E),q(m,S),q(m,SE)),0,0,h,h);     // SE quadrant <- NW quadrant
 return c;}
const strip=[...Array(T)].map(()=>Array(T*47).fill(0));order.forEach((m,f)=>{const c=blobCell(m);for(let y=0;y<T;y++)for(let x=0;x<T;x++)strip[y][f*T+x]=c[y][x];});
P.write('autotile_blob47_from_wang_1504x32.png',T*47,T,toRGBA(strip,T*47,T,[[0,0,0,0],[255,255,255,255]]));
// demo: a painted cell map (B region with 2-3 cell steps + a pond hole), composed via blob rules
const map=[
"................",
"....######......",
"..##########....",
"..###########...",
".#############..",
".####..########.",
".####..########.",
"..############..",
"...##########...",
"....#######.....",
"......###.......",
"................"];
const MH=map.length,MW=map[0].length;const isB=(x,y)=>y>=0&&x>=0&&y<MH&&x<MW&&map[y][x]==='#';
const demo=[...Array(MH*T)].map(()=>Array(MW*T).fill(0));
for(let y=0;y<MH;y++)for(let x=0;x<MW;x++){if(!isB(x,y))continue;let m=0;if(isB(x,y-1))m|=N;if(isB(x+1,y))m|=E;if(isB(x,y+1))m|=S;if(isB(x-1,y))m|=Wb;
 if((m&N)&&(m&E)&&isB(x+1,y-1))m|=NE;if((m&S)&&(m&E)&&isB(x+1,y+1))m|=SE;if((m&S)&&(m&Wb)&&isB(x-1,y+1))m|=SW;if((m&N)&&(m&Wb)&&isB(x-1,y-1))m|=NWb;
 const c=blobCell(m);for(let yy=0;yy<T;yy++)for(let xx=0;xx<T;xx++)demo[y*T+yy][x*T+xx]=c[yy][xx];}
const dc=cls(demo,MW*T,MH*T);
P.write('autotile_demo_patch_512x384.png',MW*T,MH*T,toRGBA(dc,MW*T,MH*T,[[196,154,92,255],[134,179,74,255],[63,110,52,255]]));
// cell grid overlay demo (for evidence): same but with map cells
require('fs').writeFileSync(path.join(OUT,'layout.json'),JSON.stringify({grid:GRID,layout,thr:THR,wobble:A},null,1));
console.log('layout',JSON.stringify(layout));
// seam check: for every pair of tiles sharing an edge corner-pair, compare border-adjacent columns
let bad=0;const keys=Object.keys(tiles).map(Number);
for(const a of keys)for(const b of keys){const ta=tiles[a],tb=tiles[b];
 // horizontal neighbour: a's NE,SE == b's NW,SW
 if(((a>>1)&1)===(b&1)&&((a>>3)&1)===((b>>2)&1)){for(let y=0;y<T;y++)if(ta[y][T-1]!==tb[y][0])bad++;}
 if(((a>>2)&1)===(b&1)&&((a>>3)&1)===((b>>1)&1)){for(let x=0;x<T;x++)if(ta[T-1][x]!==tb[0][x])bad++;}}
console.log('border mismatches (adjacent px differ across compatible edges):',bad);
// ---- dual-grid render of the same map (the contract): display tile (i,j), i in 0..MW, j in 0..MH,
// corners = data cells (i-1,j-1) (i,j-1) (i-1,j) (i,j); drawn with a half-cell offset
const dg=[...Array(MH*T)].map(()=>Array(MW*T).fill(0));
for(let j=0;j<=MH;j++)for(let i=0;i<=MW;i++){const id=(isB(i-1,j-1)?1:0)|(isB(i,j-1)?2:0)|(isB(i-1,j)?4:0)|(isB(i,j)?8:0);if(!id)continue;const t=tiles[id];
 for(let y=0;y<T;y++)for(let x=0;x<T;x++){const X=(i-1)*T+T/2+x,Y=(j-1)*T+T/2+y;if(X<0||Y<0||X>=MW*T||Y>=MH*T)continue;dg[Y][X]=t[y][x];}}
const dgc=cls(dg,MW*T,MH*T);
P.write('autotile_demo_dualgrid_512x384.png',MW*T,MH*T,toRGBA(dgc,MW*T,MH*T,[[196,154,92,255],[134,179,74,255],[63,110,52,255]]));
// ---- side-face strip template 64x128: left W face (x0-17), right E face (x46-63), 4 cells tall, continuous
{const w=64,h=128;const a=Buffer.alloc(w*h*4),g=Buffer.alloc(w*h*4);
 const col=(x)=>{ if(x<=17){ if(x<=3)return [46,51,69]; if(x===17)return [195,214,122]; return [110,69,36]; }
  if(x>=46){ const e=x-32; if(e===14)return [195,214,122]; if(e>=28)return [46,51,69]; return [110,69,36]; } return null; };
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){const o=(y*w+x)*4;const c=col(x);if(!c)continue;a[o]=a[o+1]=a[o+2]=255;a[o+3]=255;g[o]=c[0];g[o+1]=c[1];g[o+2]=c[2];g[o+3]=255;}
 P.write('autotile_sidewall_alpha_64x128.png',w,h,a);P.write('autotile_sidewall_guide_64x128.png',w,h,g);}
