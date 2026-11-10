#!/usr/bin/env python3
"""Call tools on a Unisono MCP endpoint from the command line (Streamable HTTP, stateful session).

Usage:
  scripts/mcp-call.py <url> list
  scripts/mcp-call.py <url> call <tool> ['{"json":"arguments"}']   (repeatable: call t1 {...} call t2 {...})

Prints each tool result's text content (JSON results pretty-printed). Standard library only.
"""
import json
import sys
import urllib.request


class Client:
    def __init__(self, url: str):
        self.url, self.session, self.next_id = url, None, 1

    def _post(self, body: dict) -> dict | None:
        data = json.dumps(body).encode()
        req = urllib.request.Request(self.url, data=data, method="POST", headers={
            "Content-Type": "application/json", "Accept": "application/json, text/event-stream",
            **({"Mcp-Session-Id": self.session} if self.session else {})})
        with urllib.request.urlopen(req, timeout=30) as resp:
            if resp.headers.get("Mcp-Session-Id"):
                self.session = resp.headers["Mcp-Session-Id"]
            if resp.status == 202:
                return None
            text = resp.read().decode()
        for line in text.splitlines():            # SSE: take the data: line(s)
            if line.startswith("data:"):
                return json.loads(line[5:].strip())
        return json.loads(text) if text.strip() else None

    def request(self, method: str, params: dict | None = None) -> dict:
        body = {"jsonrpc": "2.0", "id": self.next_id, "method": method}
        if params is not None:
            body["params"] = params
        self.next_id += 1
        msg = self._post(body)
        if msg is None:
            raise RuntimeError(f"no response for {method}")
        if "error" in msg:
            raise RuntimeError(f"{method} failed: {msg['error']}")
        return msg["result"]

    def notify(self, method: str) -> None:
        self._post({"jsonrpc": "2.0", "method": method})

    def close(self) -> None:
        if self.session:
            req = urllib.request.Request(self.url, method="DELETE", headers={"Mcp-Session-Id": self.session})
            try:
                urllib.request.urlopen(req, timeout=10).close()
            except Exception:
                pass


def main(argv: list[str]) -> int:
    if len(argv) < 3:
        print(__doc__)
        return 2
    client = Client(argv[1])
    client.request("initialize", {"protocolVersion": "2025-06-18", "capabilities": {},
                                  "clientInfo": {"name": "unisono-mcp-call", "version": "0"}})
    client.notify("notifications/initialized")
    try:
        i = 2
        while i < len(argv):
            if argv[i] == "list":
                for tool in client.request("tools/list")["tools"]:
                    props = list(tool.get("inputSchema", {}).get("properties", {}).keys())
                    print(f"{tool['name']}({', '.join(props)}): {tool.get('description', '')}")
                i += 1
            elif argv[i] == "call":
                tool = argv[i + 1]
                args = json.loads(argv[i + 2]) if i + 2 < len(argv) and argv[i + 2] != "call" else {}
                i += 3 if args or (i + 2 < len(argv) and argv[i + 2] != "call") else 2
                result = client.request("tools/call", {"name": tool, "arguments": args})
                print(f"== {tool} {json.dumps(args)}" + ("  [error]" if result.get("isError") else ""))
                for block in result.get("content", []):
                    if block.get("type") == "text":
                        text = block["text"]
                        try:
                            print(json.dumps(json.loads(text), indent=2, ensure_ascii=False))
                        except ValueError:
                            print(text)
            else:
                print(f"unknown command {argv[i]}")
                return 2
    finally:
        client.close()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
