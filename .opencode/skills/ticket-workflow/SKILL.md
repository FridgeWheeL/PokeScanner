---
name: ticket-workflow
description: Use when working on a ticket or feature branch. Covers creating requirement.md, designing via plan.md, tracking progress in status.md, and resuming work across sessions.
---

# Ticket Workflow

## Starting a new ticket

1. Create a branch:
   ```
   git checkout -b feat/TICKET-N
   ```

2. Create `docs/TICKET-N/requirement.md`:

   ```markdown
   # TICKET-N: Brief Title

   ## Requirements
   - Bullet list of functional requirements

   ## Acceptance Criteria
   - [ ] Criterion 1
   - [ ] Criterion 2

   ## Notes
   Implementation hints, links to relevant code, dependencies.
   ```

3. Start a session:
   - Use `/ticket TICKET-N` command, or
   - Say "Work on TICKET-N" in natural language.

## Resuming work on an existing ticket

When the user says "Continue TICKET-N" or uses `/ticket TICKET-N` on an
existing ticket, the agent should:

1. Read `docs/TICKET-N/requirement.md` (the what).
2. Read `docs/TICKET-N/plan.md` (the design, if it exists).
3. Read `docs/TICKET-N/status.md` (what has been done, what's next).
4. Check the current branch matches the ticket.
5. Resume from the last point in `status.md`.

## Status tracking

After each session, update `docs/TICKET-N/status.md`:

```markdown
# Status: TICKET-N

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

## Branch safety

- Always verify the current branch matches the ticket before making changes.
- Never commit, push, or create PRs unless explicitly asked.
- If the working tree is dirty when starting, ask the user before discarding
  or stashing.
