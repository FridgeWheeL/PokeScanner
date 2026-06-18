# AGENTS.md — Project Instructions

This file is loaded at the start of every session. It defines the standards,
workflow, and expectations for all work in this repository.

---

## 1. Purpose

This is a PokeScanner project using Clean Architecture with
MSTest for testing. The setup is designed to be adaptable to
any project size or type within the PokeScanner namespace.

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
- **Formatting**: 4-space indentation, no tabs. Follow
  `.editorconfig` if present.

## 3. Testing Standards (MSTest)

Apply the **dotnet-testing** skill for full details. Key rules:
- Test naming: `MethodName_StateUnderTest_ExpectedBehavior`
- Use `[TestMethod]` for unit tests, `[DataRow]` for
  parameterised.
- AAA (Arrange-Act-Assert) with blank line separators.
- Mock external dependencies with Moq; never mock system under
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
- **Infrastructure layer**: Persistence (None), external services,
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
   - Invoke `@planner` to design → writes `docs/TICKET-N/plan.md`
   - Invoke `@coding-agent` to implement
   - Invoke `@test-specialist` to add/update tests
   - Invoke `@reviewer` to review changes
   - Collect documentation flags from planner, coding-agent,
     test-specialist, and reviewer
   - Invoke `@solutions-architect` to process all flags and update docs
   - Invoke `@cleanup-agent` for final polish (code only)
   - Update `docs/TICKET-N/status.md` with progress summary

## 7. Sub-agent Usage

| Sub-agent | When to use |
|-----------|-------------|
| `@planner` | New ticket start. Produces a per-ticket implementation plan working within the existing architecture. Read-only. Never makes architecture decisions — escalates them to solutions-architect. |
| `@coding-agent` | Writing or modifying production code. |
| `@test-specialist` | Writing or fixing tests. Has dotnet-testing skill context. |
| `@reviewer` | After implementation is done. Reviews code, tests, standards. Read-only. |
| `@cleanup-agent` | Final pass: adds minimal constructive comments, removes AI conversational artifacts, formats whitespace, organizes imports. |
| `@solutions-architect` | Designing the overall solution structure, choosing technologies, defining high-level architecture, maintaining `docs/architecture.md` and `docs/stack.md`. Reviews planner's plans for architectural consistency. All architecture decisions go through this agent. |

The primary agent orchestrates the workflow. Use sub-agents sequentially,
not in parallel. After each sub-agent finishes, inspect the result before
calling the next.

## 8. Skills System

Skills are reusable instruction sets registered under `.opencode/skills/`.
Each skill has a `description` containing trigger keywords — when those
keywords match the conversation, the skill is auto-loaded and its
instructions become available.

Agents reference skills by name in their prompts. For example, the
test-specialist's description includes "MSTest" which matches
the dotnet-testing skill description, ensuring the skill is loaded when
that agent is active.

To create a new skill: add a folder under `.opencode/skills/<name>/` with a
`SKILL.md` containing `name` and `description` frontmatter.

## 9. Communication Rules

- No conversational AI phrases ("As an AI...", "Let me know if...",
  "I hope this helps...").
- Be direct and technical.
- Acknowledge with "Done" or the minimal needed response.
- When reporting errors, provide the exact error message and location.
