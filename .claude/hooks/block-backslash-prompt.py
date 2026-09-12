#!/usr/bin/env python3
r"""UserPromptSubmit hook: reject prompts that start with a backslash.

Catches the common typo of reaching for `\` instead of `/` when invoking a
slash command. The prompt is blocked (never sent to the model) and the user
gets a short reminder.
"""

import json
import sys

payload = json.load(sys.stdin)
prompt = payload.get("prompt", "")

if prompt.lstrip().startswith("\\"):
    print(json.dumps({
        "decision": "block",
        "reason": (
            "Повідомлення починається з `\\`. Команди Claude Code вводяться з `/` "
            "(наприклад `/help`, `/config`, `/code-review`). "
            "Наберіть команду з `/`, або, якщо це справді був звичайний текст, "
            "приберіть `\\` на початку."
        ),
    }))

sys.exit(0)
