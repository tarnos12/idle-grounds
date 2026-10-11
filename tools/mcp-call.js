// Direct MCP-for-Unity client (streamable HTTP), for when the session's MCP connection is stale.
// Usage: node tools/mcp-call.js <tool> '<json args>'      e.g. node tools/mcp-call.js execute_code '{"action":"execute","code":"return 1;"}'
//        node tools/mcp-call.js --list
// Pins the idle-grounds instance (the server is shared with another project).
const URL = process.env.MCP_URL || "http://127.0.0.1:8080/mcp";
const INSTANCE = process.env.MCP_INSTANCE || "idle-grounds@d0cd7413e288ec3f";
let session = null, nextId = 1;

async function rpc(method, params, notify) {
  const body = { jsonrpc: "2.0", method, params };
  if (!notify) body.id = nextId++;
  const headers = { "Content-Type": "application/json", Accept: "application/json, text/event-stream" };
  if (session) headers["Mcp-Session-Id"] = session;
  const res = await fetch(URL, { method: "POST", headers, body: JSON.stringify(body) });
  const sid = res.headers.get("mcp-session-id"); if (sid) session = sid;
  if (notify) return null;
  const text = await res.text();
  // SSE ("data: {...}") or plain JSON
  const lines = text.split(/\r?\n/).filter(l => l.startsWith("data:")).map(l => l.slice(5).trim());
  const msgs = (lines.length ? lines : [text]).filter(Boolean).map(s => JSON.parse(s));
  const msg = msgs.find(m => m.id === body.id) || msgs[msgs.length - 1];
  if (msg.error) throw new Error(JSON.stringify(msg.error));
  return msg.result;
}

(async () => {
  await rpc("initialize", { protocolVersion: "2025-03-26", capabilities: {}, clientInfo: { name: "idle-grounds-cli", version: "1" } });
  await rpc("notifications/initialized", {}, true);
  if (process.argv[2] === "--list") {
    const r = await rpc("tools/list", {});
    console.log(r.tools.map(t => t.name).join("\n"));
    return;
  }
  await rpc("tools/call", { name: "set_active_instance", arguments: { instance: INSTANCE } });
  const tool = process.argv[2], args = process.argv[3] ? JSON.parse(process.argv[3]) : {};
  const r = await rpc("tools/call", { name: tool, arguments: args });
  for (const c of r.content || []) console.log(c.type === "text" ? c.text : `[${c.type}]`);
  if (r.isError) process.exitCode = 1;
})().catch(e => { console.error("mcp-call:", e.message); process.exit(2); });
