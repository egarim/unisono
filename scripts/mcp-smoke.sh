#!/usr/bin/env bash
# Exercises a Unisono MCP endpoint with plain curl, the way an external client does:
# initialize -> notifications/initialized -> tools/list -> tools/call -> DELETE session.
# Usage: scripts/mcp-smoke.sh <url> [tool-name] [json-arguments]
set -u
URL="${1:-http://127.0.0.1:8787/mcp}"
TOOL="${2:-}"
ARGS="${3:-{\}}"
H=(-H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream')

echo "== initialize"
INIT=$(curl -s -i "${H[@]}" -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"unisono-smoke","version":"0"}}}' "$URL")
echo "$INIT" | sed -n '1p;/^data:/p'
SID=$(printf '%s' "$INIT" | tr -d '\r' | awk -F': ' 'tolower($1)=="mcp-session-id"{print $2}')
echo "session: ${SID:-<none>}"
S=(-H "Mcp-Session-Id: $SID")

echo "== notifications/initialized"
curl -s -i "${H[@]}" "${S[@]}" -d '{"jsonrpc":"2.0","method":"notifications/initialized"}' "$URL" | sed -n '1p'

echo "== tools/list"
curl -s "${H[@]}" "${S[@]}" -d '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' "$URL" | sed -n '/^data:/p' | sed 's/^data: //' | python3 -I -c 'import sys,json
for line in sys.stdin:
    line=line.strip()
    if not line: continue
    msg=json.loads(line)
    for t in msg.get("result",{}).get("tools",[]):
        props=t.get("inputSchema",{}).get("properties",{})
        print("  %s: %s  params=%s" % (t["name"], t.get("description",""), list(props.keys())))'

if [ -n "$TOOL" ]; then
  echo "== tools/call $TOOL $ARGS"
  curl -s "${H[@]}" "${S[@]}" -d "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"$TOOL\",\"arguments\":$ARGS}}" "$URL" | sed -n '/^data:/p' | sed 's/^data: //'
  echo
fi

echo "== DELETE session"
curl -s -i -X DELETE "${S[@]}" "$URL" | sed -n '1p'
