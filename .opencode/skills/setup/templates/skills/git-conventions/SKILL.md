---
name: git-conventions
description: Use when branching, committing, or reviewing PRs. Covers branch naming, commit message format, and PR process.
---

# Git Conventions

## Branch naming

| Pattern | When |
|---------|------|
| `feature/TICKETN` | New feature or enhancement |
| `bugfix/TICKETN` | Bug fix |
| `refactor/TICKETN` | Code restructuring, no behavior change |
| `docs/TICKETN` | Documentation only |
| `test/TICKETN` | Adding or fixing tests |

Always reference a ticket number when one exists.
Ticket artifacts live under `docs/Tasks/TICKETN-Short-Description/`.

## Commit messages

Use conventional commits:

```
<type>(<scope>): <subject>

<body>
```

- **Types**: `feature`, `bugfix`, `test`, `refactor`, `docs`, `style`
- **Scope**: optional, lowercase (e.g., `api`, `auth`, `ui`)
- **Subject**: imperative mood, <=50 chars, no trailing period
- **Body**: optional, wrap at 72 chars, explain *why* not *what*

Examples:
```
feature(auth): add JWT token refresh endpoint

The existing token expiry of 15 minutes was too short for long-running
sessions. This adds a /auth/refresh endpoint that issues a new token
given a valid refresh token.
```

```
bugfix: handle null reference in OrderService.GetTotal
```

```
test: add coverage for PricingService edge cases
```

## Workflow

- Commit early, commit often. Each commit should compile and pass tests.
- Rebase locally (`git rebase -i`) to clean up history before push.
- Do not push until commits are clean and tested.
- Do not create PRs unless explicitly asked.

## Code review

- Review the diff before committing. No debugging code, no commented-out
  code.
- Ensure no secrets, keys, or connection strings are committed.
- Ensure no `TODO`, `FIXME`, or `HACK` without a ticket reference.
