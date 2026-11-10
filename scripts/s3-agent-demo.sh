#!/usr/bin/env bash
# S3 with a real agent: Claude Code drives both apps over MCP. Run from the repo root with both apps running.
# One-time setup (stored in Claude Code's local project config):
#   claude mcp add --transport http tasktracker http://127.0.0.1:8787/mcp
#   claude mcp add --transport http notes       http://127.0.0.1:8789/mcp
# Requires a signed-in Claude Code (`claude login`).
set -euo pipefail
claude -p "You have two apps connected over MCP: 'notes' (Unisono Notes) and 'tasktracker' (TaskTracker). \
Find the note titled 'Standup, Monday' in notes, read its full body, and create one task in tasktracker for each \
OPEN action item (unchecked '- [ ]' lines) that does not already exist as a task, with priority high, category work, \
due 2026-11-14T17:00:00Z. Then append one line to that note listing the tasks you created. Use only the MCP tools, \
do not ask questions, and finish with a short plain-text summary of exactly what you did." \
  --allowedTools "mcp__notes,mcp__tasktracker" "$@"
