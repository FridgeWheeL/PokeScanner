# AGENTS.md — Project Instructions

This file is loaded at the start of every session. It defines the standards,
workflow, and expectations for all work in this repository.

---

## 1. Purpose

This is a **{{SOLUTION_NAME}}** project ({{SOLUTION_TYPE}}). See
`docs/Architecture/stack.md` for the technology stack and
`docs/Architecture/architecture.md` for the architecture overview.

## 2. Coding Standards

{{SECTION_CODING_STANDARDS}}

## 3. Testing

{{SECTION_TESTING}}

## 4. Git Workflow

Apply the **git-conventions** skill for full details. Key rules:
- Branch: `feature/TICKETN`, `bugfix/TICKETN`
- Commits: conventional commits (`feature:`, `bugfix:`, `test:`,
  `refactor:`). Imperative mood, 50-char subject, body wraps at 72.
- Commit early, commit often — each commit should compile and pass tests.

## 5. Architecture

{{SECTION_ARCHITECTURE}}
## 6. Ticket Workflow

Apply the **ticket-workflow** skill for full details. Standard flow:

1. Create branch: `git checkout -b feature/TICKETN`
2. Ticket documents live at `docs/Tasks/TICKETN-Short-Description/`
   with:
   - `requirement.md` — what needs to be done
3. Start work via `/ticket TICKETN` or by saying "Work on TICKETN".
4. The agent will:
   - Read requirement.md and any existing status.md
   {{GRILL_ME_STEP}}
   - Invoke `@planner` to design → writes `plan.md`
   - ✅ **Show the plan to the user** → "Does this plan look good?"
     - If the user requests changes, iterate with the planner
     - Only proceed when the user explicitly says "go ahead"
   - Invoke `@coding-agent` to implement
   - ✅ **Show summary of changes** → "Review and continue?"
   - Invoke `@test-specialist` to add/update tests
   - ✅ **Show test results** → "Tests pass. Continue?"
   - Invoke `@reviewer` to review changes
   - Collect documentation flags from planner, coding-agent,
     test-specialist, and reviewer
   - Invoke `@solutions-architect` to process all flags and update docs
   - Invoke `@cleanup-agent` for final polish (code only)
   - ✅ **Ask about commit** → follow the commit workflow (see §10)
   - Update `status.md` with progress summary

## 7. Sub-agent Usage

| Sub-agent | When to use |
|-----------|-------------|
| `@planner` | New ticket start. Produces a per-ticket implementation plan working within the existing architecture. Read-only. Never makes architecture decisions — escalates them to solutions-architect. |
| `@coding-agent` | Writing or modifying production code. |
| `@test-specialist` | Writing or fixing tests. |
| `@reviewer` | After implementation is done. Reviews code, tests, standards. Read-only. |
| `@cleanup-agent` | Final pass: adds minimal constructive comments, removes AI conversational artifacts, formats whitespace, organizes imports. |
| `@solutions-architect` | Designing the overall solution structure, choosing technologies, defining high-level architecture, maintaining architecture docs. Reviews planner's plans for architectural consistency. |

The primary agent orchestrates the workflow. Sub-agents must be invoked one
at a time, never chained automatically. After each sub-agent completes,
inspect its output and present the result to the user. Never invoke the next
sub-agent automatically — always wait for the user's explicit approval to
proceed.

## 8. Skills System

Skills are reusable instruction sets registered under `.opencode/skills/`.
Each skill has a `description` containing trigger keywords — when those
keywords match the conversation, the skill is auto-loaded and its
instructions become available.

To create a new skill: add a folder under `.opencode/skills/<name>/` with a
`SKILL.md` containing `name` and `description` frontmatter.

## 9. Communication Rules

- No conversational AI phrases ("As an AI...", "Let me know if...",
  "I hope this helps...").
- Be direct and technical.
- Acknowledge with "Done" or the minimal needed response.
- When reporting errors, provide the exact error message and location.

## 10. Commit Workflow

This applies at the end of every ticket workflow AND whenever the user
directly asks to commit.

1. Agent asks: "Would you like to commit these changes?"
2. If yes, agent runs `git diff --stat` to summarize changes.
3. Agent proposes a commit message in this exact format:

   ```
   TICKETN: Brief description of the change

   - Bullet summary of each logical change
   - Another change
   ```

4. Agent shows the message: "Proposed commit message:\n\n```\nTICKETN: ...\n```\n\nConfirm? [Y/n]"
5. If user confirms → `git commit` (local only — never include `--amend`
   or `--force` unless explicitly asked)
6. Agent asks: "Push to remote? [Y/n]"
7. If yes → `git push`

Rules:
- Never commit without showing the message first and getting explicit
  confirmation.
- Never push without asking first.
- Always use `TICKETN:` prefix in the subject line (no dash after number).
- The commit must be local-only until push is confirmed.
- If `git status` shows no changes, inform the user rather than attempting
  a commit.

## 11. Custom Conventions

{{CUSTOM_RULES}}
