#!/usr/bin/env python3

import json
import sys
import os
from pathlib import Path
from datetime import datetime

payload = json.load(sys.stdin)

inputs = payload.get("inputs", {})
response = payload.get("response")

tool_name = payload.get(
    "tool_name",
    payload.get("tool", "UNKNOWN")
)

input_json = json.dumps(inputs, ensure_ascii=False)
response_json = json.dumps(response, ensure_ascii=False)

if tool_name.startswith("mcp__idea__"):
    category = "idea"

elif tool_name.startswith("mcp__github__"):
    category = "github"

else:
    category = "other"

entry = {
    "timestamp": datetime.utcnow().isoformat(),

    "category": category,

    "response_head": str(response)[:200],

    "tool": tool_name,

    "input_size_bytes": len(input_json.encode("utf-8")),

    "response_size_bytes": len(
        response_json.encode("utf-8")
    ),

    "total_size_bytes":
        len(input_json.encode("utf-8"))
        +
        len(response_json.encode("utf-8")),

    "input_keys": list(inputs.keys()),

    "response_type":
        type(response).__name__
        if response is not None
        else "None"
}

# Anchor to the project root: this hook runs with whatever cwd the tool call
# had, so a relative path would scatter log files across the tree.
project_dir = os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()

log_file = Path(project_dir) / "docs" / "hooks" / "tool-usage.jsonl"

log_file.parent.mkdir(
    parents=True,
    exist_ok=True
)

with open(
        log_file,
        "a",
        encoding="utf-8"
) as f:
    f.write(
        json.dumps(entry, ensure_ascii=False)
        + "\n"
    )