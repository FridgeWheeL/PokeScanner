# AGENTS.md — Project Instructions

This file is loaded at the start of every session. It defines the standards,
workflow, and expectations for all work in this repository.

---

## 1. Purpose

This is a {{SOLUTION_NAME}} project using {{ARCH_PATTERN}} with
{{TEST_FRAMEWORK}} for testing. The setup is designed to be adaptable to
any project size or type within the {{ROOT_NAMESPACE}} namespace.

See `docs/stack.md` for the current technology stack and
`docs/architecture.md` for the solution architecture diagram.

## 2. Coding Standards

- **Naming**: PascalCase for classes, methods, properties, namespaces, and
  public members. `_camelCase` for private fields. camelCase for local
  variables and parameters.
- **File layout**: One type per file (exceptions: small enums, DTOs).
- **Documentation**: XML docs on all public APIs — summary, params, returns.
  Minimalist inline comments; prefer clear code over comments.
- **Null handling**: Enable nullable reference types. Use
  `ArgumentNullException.ThrowIfNull`.
- **Patterns**:
  - Constructor injection for dependencies (never property injection).
  - Interfaces for all services, repositories, and handlers.
  - Immutable DTOs and commands (`record` types).
  - Async all the way: suffix async methods with `Async`.
- **Error handling**: Use `Result<T>` or `OneOf` for expected failures;
  throw only for programmer errors.
- **Formatting**: {{EDITORCONFIG_INDENT}}-space indentation, no tabs. Follow
  `.editorconfig` if present.
{{CUSTOM_RULES}}
## 3. Testing Standards ({{TEST_FRAMEWORK}})

Apply the **{{TEST_SKILL}}** skill for full details. Key rules:
- Test naming: `MethodName_StateUnderTest_ExpectedBehavior`
- Use `{{TEST_METHOD_ATTR}}` for unit tests, `{{TEST_DATA_ROW}}` for
  parameterised.
- AAA (Arrange-Act-Assert) with blank line separators.
- Mock external dependencies with {{MOCK_LIBRARY}}; never mock system under
  test.

## 4. Git Workflow

Apply the **git-conventions** skill for full details. Key rules:
- Branch: `feat/TICKET-N`, `fix/TICKET-N`, `chore/TICKET-N`
- Commits: conventional commits (`feat:`, `fix:`, `test:`, `chore:`,
  `refactor:`). Imperative mood, 50-char subject, body wraps at 72.
- Commit early, commit often — each commit should compile and pass tests.

## 5. Architecture

Apply the **architecture** skill for full details. Key rules:
- **Domain layer**: Entities, Value Objects, Domain Events, Enums. Zero
  dependencies.
- **Application layer**: Commands/Queries, DTOs, Interfaces (not
  implementations), Mapping profiles. Depends only on Domain.
- **Infrastructure layer**: Persistence ({{ORMLIB}}), external services,
  file system. Depends on Application.
- **Presentation layer**: Controllers, Middleware, Program.cs. Depends on
  Application.
- Follow SOLID principles. Use MediatR for cross-cutting (validation,
  logging).

## 6. Ticket Workflow

Apply the **ticket-workflow** skill for full details. Standard flow:

1. Create branch: `git checkout -b feat/TICKET-N`
2. Create `docs/TICKET-N/requirement.md` with the format:
   - `# TICKET-N: Title`
   - `## Requirements` (bullet list)
   - `## Acceptance Criteria` (checked list)
   - `## Notes` (implementation hints, links)
3. Start work via `/ticket TICKET-N` or by saying "Work on TICKET-N".
4. The agent will:
   - Read `docs/TICKET-N/requirement.md` and any existing
     `docs/TICKET-N/status.md`
   {{GRILL_ME_STEP}}
   - Invoke `@planner` to design → writes `docs/TICKET-N/plan.md`
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
   - Update `docs/TICKET-N/status.md` with progress summary

## 7. Sub-agent Usage

| Sub-agent | When to use |
|-----------|-------------|
| `@planner` | New ticket start. Produces a per-ticket implementation plan working within the existing architecture. Read-only. Never makes architecture decisions — escalates them to solutions-architect. |
| `@coding-agent` | Writing or modifying production code. |
| `@test-specialist` | Writing or fixing tests. Has {{TEST_SKILL}} skill context. |
| `@reviewer` | After implementation is done. Reviews code, tests, standards. Read-only. |
| `@cleanup-agent` | Final pass: adds minimal constructive comments, removes AI conversational artifacts, formats whitespace, organizes imports. |
| `@solutions-architect` | Designing the overall solution structure, choosing technologies, defining high-level architecture, maintaining `docs/architecture.md` and `docs/stack.md`. Reviews planner's plans for architectural consistency. All architecture decisions go through this agent. |

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

Agents reference skills by name in their prompts. For example, the
test-specialist's description includes "{{TEST_FRAMEWORK}}" which matches
the {{TEST_SKILL}} skill description, ensuring the skill is loaded when
that agent is active.

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
   TICKET-N: Brief description of the change

   - Bullet summary of each logical change
   - Another change
   ```

4. Agent shows the message: "Proposed commit message:\n\n```\nTICKET-N: ...\n```\n\nConfirm? [Y/n]"
5. If user confirms → `git commit` (local only — never include `--amend`
   or `--force` unless explicitly asked)
6. Agent asks: "Push to remote? [Y/n]"
7. If yes → `git push`

Rules:
- Never commit without showing the message first and getting explicit
  confirmation.
- Never push without asking first.
- Always use `TICKET-N:` prefix in the subject line.
- The commit must be local-only until push is confirmed.
- If `git status` shows no changes, inform the user rather than attempting
  a commit.
