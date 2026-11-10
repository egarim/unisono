#!/usr/bin/env python3
"""S3 without a model: drive both apps over MCP the way an agent would.

Reads the 'Standup, Monday' note in Unisono Notes, creates one TaskTracker task per open action item,
then appends a line to the note listing what was created. Both apps must be running (Mac Catalyst, or two
simulators) with their MCP endpoints up. Standard library only; reuses scripts/mcp-call.py's client.

Usage: scripts/s3-orchestrate.py [notes-url] [tasks-url]
"""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from importlib import import_module
mcp_call = import_module("mcp-call")  # scripts/mcp-call.py

NOTES = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:8789/mcp"
TASKS = sys.argv[2] if len(sys.argv) > 2 else "http://127.0.0.1:8787/mcp"
OPEN_ITEM = re.compile(r"^\s*[-*]\s*\[\s\]\s*(.+?)\s*$")


def connect(url: str) -> mcp_call.Client:
    c = mcp_call.Client(url)
    c.request("initialize", {"protocolVersion": "2025-06-18", "capabilities": {},
                             "clientInfo": {"name": "unisono-s3-orchestrator", "version": "0"}})
    c.notify("notifications/initialized")
    return c


def call(client: mcp_call.Client, tool: str, args: dict):
    result = client.request("tools/call", {"name": tool, "arguments": args})
    text = next((b["text"] for b in result.get("content", []) if b.get("type") == "text"), "null")
    if result.get("isError"):
        raise RuntimeError(f"{tool}: {text}")
    return json.loads(text)


def main() -> int:
    notes, tasks = connect(NOTES), connect(TASKS)
    try:
        standup = next((n for n in call(notes, "list_notes", {"query": "Standup"}) if n["title"].startswith("Standup")), None)
        if standup is None:
            print("no standup note found"); return 1
        note = call(notes, "get_note", {"id": standup["id"]})
        items = [m.group(1) for m in (OPEN_ITEM.match(l) for l in note["body"].splitlines()) if m]
        print(f"note '{note['title']}' has {len(items)} open action item(s)")

        existing = {t["title"] for t in call(tasks, "list_tasks", {"showCompleted": True})}
        created = []
        for item in items:
            if item in existing:
                print(f"  skip (exists): {item}"); continue
            t = call(tasks, "create_task", {"title": item, "priority": "high", "category": "work",
                                            "dueDate": "2026-11-14T17:00:00Z"})
            created.append(t["title"]); print(f"  created: {t['title']}  [{t['priority']}/{t['category']}] due {t['dueDate']}")

        if created:
            line = "Created in TaskTracker (high, work, due Fri 14 Nov): " + "; ".join(created)
            updated = call(notes, "append_to_note", {"id": note["id"], "text": line})
            print(f"appended to note; updatedAt={updated['updatedAt']}")
        else:
            print("nothing new to create; note left unchanged")
        return 0
    finally:
        notes.close(); tasks.close()


if __name__ == "__main__":
    sys.exit(main())
