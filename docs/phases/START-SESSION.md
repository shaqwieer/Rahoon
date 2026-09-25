# Prompt to start a new session

Copy this into a new Claude Code session opened in the repo folder:

```text
Continue رهون phase by phase. First read docs/product/product-direction.md (product source of truth:
individual-first, open questions Q1–Q15), then docs/phases/README.md and the phase file marked ▶ CURRENT.
Work only on that phase, on its first unfinished step; don't touch other phases.
Before starting, check git status and `git worktree list`, and tell me in 3–5 lines what the step will do
and which open product questions (if any) it depends on. If a question blocks the step, stop and ask me
instead of guessing.
When the step is done: run the checks in docs/phases/README.md ("How a step is done"), tick the step,
update the status table (tech status + direction review) and Findings, commit, and tell me what is
finished and what the next step is.
```

To target a specific step, add a line such as `Do Phase 1A step 3.`

**If you have answers to product questions**, paste them into the session and say so, for example: `Answers: Q1 = in-platform intake; Q11 = rescheduling only for now; …`. Claude records them in `docs/product/product-direction.md` §6 before building anything that depends on them.

When a phase's *Definition of done* is fully ticked, the last step moves the **▶ CURRENT** marker in `docs/phases/README.md` to the next phase.
