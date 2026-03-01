# Next Session Prompt (Copy/Paste)

Use this prompt at the start of a new session:

```text
Load project context from:
- /Users/jozefrandjak/Documents/git/InterReactMCP/ResearchPlatform/docs/session-handoff.md
- /Users/jozefrandjak/Documents/git/InterReactMCP/ResearchPlatform/docs/module-boundaries.md
- /Users/jozefrandjak/Documents/git/InterReactMCP/ResearchPlatform/docs/configuration.md

Current objective:
- Continue ResearchPlatform build from completed T-001, T-002, T-003, T-004, and T-005.
- Start with T-006 (point-in-time index constituents for SP500/SP100).

Constraints:
- Keep module boundaries strict (modules reference contracts only).
- Keep research-only scope (no live execution yet).
- Preserve existing unrelated files in parent repo.

Before coding, run:
- ./scripts/check-module-boundaries.sh
- ./scripts/validate-config-json.sh
- ./scripts/validate-config.sh

Then propose and implement T-006 end-to-end.
```
