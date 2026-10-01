# Phase handoffs

A handoff is written at the end of each phase session and is how the next session learns the real state (no chat
history is carried). Only a phase that was implemented and verified in its session gets one. Never put credentials,
tokens or customer documents in a handoff.

## How the next session uses it

1. Read `../README.md` (order and status) and `../decisions.md`.
2. Read the handoffs of every prerequisite phase, then **verify** what they claim in the code (entry points, migrations,
   tests) before building on it.
3. Note in your own handoff anything that turned out different.

## Template — `phase-<id>-handoff.md`

```markdown
# Phase <id> handoff — <title>

**Status:** Completed | Partial | Blocked  ·  **Date:** YYYY-MM-DD  ·  **Commit:** <hash> on <branch>
**Push / deployment:** <what was actually pushed or deployed, or "not pushed / not deployed">

## Verified prerequisites
## Implemented behaviour
## Code entry points
## Migrations and their application state (local / staging / production)
## Permissions (new / changed keys, role matrix changes)
## Adopted product decisions
## Tests and checks run (with actual results)
## How to reproduce the acceptance scenario
## Remaining limitations and known issues
## Next phase and its starting instruction
```
