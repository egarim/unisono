#!/usr/bin/env python3
"""Validate MCP tools/list, resources/list and resources/read for a UI-enabled Unisono host.
Usage: python3 scripts/mcp-ui-smoke.py http://127.0.0.1:8792/mcp
Requires only the Python stdlib; works with SSE-wrapped MCP responses.
"""
import json
import sys
import urllib.request

URL = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:8792/mcp"
SESSION = None

def call(method, params=None, id=1):
    global SESSION
    body = {"jsonrpc": "2.0", "id": id, "method": method}
    if params is not None:
        body["params"] = params
    headers = {"Accept": "application/json, text/event-stream", "Content-Type": "application/json"}
    if SESSION:
        headers["Mcp-Session-Id"] = SESSION
    req = urllib.request.Request(URL, data=json.dumps(body).encode(), headers=headers)
    with urllib.request.urlopen(req, timeout=20) as res:
        SESSION = res.headers.get("Mcp-Session-Id", SESSION)
        payload = res.read().decode()
    for line in payload.splitlines():
        if line.startswith("data:"):
            result = json.loads(line[5:])
            if "error" in result:
                raise RuntimeError(result["error"])
            return result["result"]
    parsed = json.loads(payload)
    if "error" in parsed:
        raise RuntimeError(parsed["error"])
    return parsed["result"]

call("initialize", {
    "protocolVersion": "2025-06-18",
    "capabilities": {},
    "clientInfo": {"name": "unisono-ui-smoke", "version": "1.0"}
})
tools = call("tools/list", id=2)["tools"]
matched = next(t for t in tools if t["name"] == "calculate_pupusas")
assert matched["_meta"]["ui"]["resourceUri"] == "ui://pupuseria/order", matched
resources = call("resources/list", id=3)["resources"]
assert any(r["uri"] == "ui://pupuseria/order" for r in resources), resources
content = call("resources/read", {"uri": "ui://pupuseria/order"}, id=4)["contents"][0]
assert content["mimeType"] == "text/html;profile=mcp-app", content
assert "<html" in content["text"].lower(), "No HTML returned"
value = call("tools/call", {
    "name": "calculate_pupusas",
    "arguments": {"quantity": 3, "priceEach": 1.75}
}, id=5)
assert not value.get("isError"), value
print("PASS: UI tool metadata, resource list/read, and headless tool call")
