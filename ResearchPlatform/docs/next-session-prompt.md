# Next Session Prompt (Copy/Paste)

Use this prompt at the start of a new session:

```text
Load project context from:
- ./docs/session-handoff.md
- ./docs/module-boundaries.md
- ./docs/configuration.md

Current objective:
- Continue ResearchPlatform build from completed T-001 through T-011.
- Start with T-012 (scheduler + retries + alerts).

Constraints:
- Keep module boundaries strict (modules reference contracts only).
- Keep research-only scope (no live execution yet).
- Preserve existing unrelated files in parent repo.

Before coding, run:
- ./scripts/check-module-boundaries.sh
- ./scripts/validate-config-json.sh
- ./scripts/validate-config.sh

Then propose and implement T-012 end-to-end.
```
