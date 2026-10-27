#!/usr/bin/env python3
"""PostToolUse guard for the append-only workflow logs.

Checks the lines appended to docs/workflow/history.jsonl and
docs/workflow/findings.jsonl since the previous check: each timestamp must be
UTC ISO-8601, not in the future and not earlier than the line before it.
A shrunken file means the log was rewritten. A violation exits with code 2, so
Claude Code shows the message to the agent; nothing is modified.

Lines already present when the guard first runs are its baseline and are not
checked — older lines hold estimated times (story-orchestrator SKILL.md,
Timestamps).
"""

import json
import os
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

LOGS = ("docs/workflow/history.jsonl", "docs/workflow/findings.jsonl")
STATE = "docs/hooks/log-timestamps-state.json"
FUTURE_TOLERANCE = timedelta(minutes=2)

try:
    json.load(sys.stdin)
except ValueError:
    pass

project_dir = Path(os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd())
state_file = project_dir / STATE
try:
    state = json.loads(state_file.read_text(encoding="utf-8"))
except (OSError, ValueError):
    state = {}

now = datetime.now(timezone.utc)
problems = []


def parse(ts):
    if not isinstance(ts, str) or not ts.endswith("Z"):
        return None
    try:
        return datetime.fromisoformat(ts[:-1]).replace(tzinfo=timezone.utc)
    except ValueError:
        return None


for rel in LOGS:
    path = project_dir / rel
    if not path.exists():
        continue
    lines = [l for l in path.read_text(encoding="utf-8").splitlines() if l.strip()]
    seen = state.get(rel)
    state[rel] = len(lines)
    if seen is None or len(lines) == seen:
        continue
    if len(lines) < seen:
        problems.append(f"{rel}: had {seen} lines, now {len(lines)} - the log is append-only.")
        continue
    prev = None
    if seen > 0:
        try:
            prev = parse(json.loads(lines[seen - 1]).get("timestamp"))
        except ValueError:
            prev = None
        # A baseline line stamped in the future is itself an invented time;
        # ordering against it would reject every honest line until then.
        if prev is not None and prev > now:
            prev = None
    for n in range(seen, len(lines)):
        try:
            raw = json.loads(lines[n]).get("timestamp")
        except ValueError:
            problems.append(f"{rel} line {n + 1}: not valid JSON.")
            continue
        ts = parse(raw)
        if ts is None:
            problems.append(f"{rel} line {n + 1}: timestamp {raw!r} is not UTC ISO-8601 ending in Z.")
            continue
        if ts > now + FUTURE_TOLERANCE:
            problems.append(f"{rel} line {n + 1}: timestamp {raw} is in the future (now {now:%Y-%m-%dT%H:%M:%SZ}).")
        if prev is not None and ts < prev:
            problems.append(f"{rel} line {n + 1}: timestamp {raw} is earlier than the line before it ({prev:%Y-%m-%dT%H:%M:%SZ}).")
        prev = ts

state_file.parent.mkdir(parents=True, exist_ok=True)
state_file.write_text(json.dumps(state), encoding="utf-8")

if problems:
    print(
        "Workflow log timestamp check failed:\n- " + "\n- ".join(problems)
        + "\nTake every time from `date -u +%Y-%m-%dT%H:%M:%SZ` (story-orchestrator SKILL.md, Timestamps)."
        " Do not edit the earlier lines; stop and report this to the human.",
        file=sys.stderr,
    )
    sys.exit(2)
