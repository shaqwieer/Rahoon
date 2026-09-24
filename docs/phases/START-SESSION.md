# Prompt to start a new session

Copy this into a new Claude Code session opened in the repo folder:

```text
Continue رهون phase by phase. Read docs/phases/README.md, then the phase file marked ▶ CURRENT.
Work only on the first unfinished step of that phase (one step per session, don't touch later phases).
Before starting, check git status and `git worktree list`, and tell me in 3–5 lines what the step will do.
When the step is done: run the checks listed in docs/phases/README.md ("How a step is done"),
tick the step in the phase file, update its status table and Findings, commit, and tell me
what is finished and what the next step is.
```

To work on a specific step instead, add a line such as: `Do Phase 1A step 3.`

When a phase's *Definition of done* is fully ticked, move the **▶ CURRENT** marker in `docs/phases/README.md` to the next phase. That happens as part of the last step.
