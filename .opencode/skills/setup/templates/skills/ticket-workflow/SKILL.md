---
name: ticket-workflow
description: Use when working on a ticket or feature branch. Covers creating requirement.md, designing via plan.md, tracking progress in status.md, and resuming work across sessions.
---

# Ticket Workflow

## Starting a new ticket

1. Create a branch:
   ```
    git checkout -b feature/TICKETN
   ```

2. Create the ticket directory:
   ```
   docs/Tasks/TICKETN-Short-Description/
   ```

3. Create `requirement.md` in that directory:

   ```markdown
   # TICKETN: Brief Title

   ## Requirements
   - Bullet list of functional requirements

   ## Acceptance Criteria
   - [ ] Criterion 1
   - [ ] Criterion 2

   ## Notes
   Implementation hints, links to relevant code, dependencies.
   ```

4. Start a session:
   - Use `/ticket TICKETN` command, or
   - Say "Work on TICKETN" in natural language.

## Resuming work on an existing ticket

When the user says "Continue TICKETN" or uses `/ticket TICKETN` on an
existing ticket, the agent should:

1. Find the ticket directory: `docs/Tasks/TICKETN-*/`
2. Read `requirement.md` (the what).
3. Read `plan.md` (the design, if it exists).
4. Read `status.md` (what has been done, what's next).
5. Check the current branch matches the ticket.
6. Resume from the last point in `status.md`.

## Status tracking

After each session, update `status.md` in the ticket directory:

```markdown
# Status: TICKETN

## Completed
- [x] Task 1
- [x] Task 2

## In Progress
- [ ] Task 3

## Next
- Task 4 (description)
- Task 5 (description)

## Notes
Blockers, decisions made, things to revisit.
```

The `status.md` file is the persistent state between sessions. Keep it
accurate and up to date.

## User checkpoints

The workflow has mandatory user approval gates:

1. **Plan approval** — After the planner produces `plan.md`, the primary
   agent shows you the plan. You can request changes or approve.
2. **Implementation review** — After coding completes, you see a summary of
   every file created and modified. You approve before tests run.
3. **Test results** — After tests execute, you see pass/fail counts. You
   approve before the review phase.
4. **Commit prompt** — At the end, you're asked whether to commit.

The primary agent never chains sub-agents automatically. Each stage pauses
for your input.

## Commit Workflow

Follow the commit workflow in AGENTS.md §10:
1. Agent asks "Would you like to commit these changes?"
2. If yes → `git diff --stat` for summary
3. Agent proposes `TICKETN: Description` format message
4. Shows you the message for confirmation
5. If confirmed → local `git commit`
6. Asks "Push to remote? [Y/n]"
7. If yes → `git push`

## Branch safety

- Always verify the current branch matches the ticket before making changes.
- Never commit, push, or create PRs unless explicitly asked.
- If the working tree is dirty when starting, ask the user before discarding
  or stashing.
