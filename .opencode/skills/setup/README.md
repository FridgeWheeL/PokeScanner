# OpenCode Setup Skill

Bootstraps a fully customized OpenCode configuration for any project type
— .NET, Terraform, Node.js, Python, mono-repos, and more.

## Purpose

Instead of manually creating AGENTS.md, opencode.json, agent files, skills,
and docs every time you start a new project, this skill:

1. **Scans** your codebase to detect solution type, project roles,
   packages, and coding conventions
2. **Asks** targeted questions to fill in what it couldn't detect
3. **Generates** a complete `.opencode/` configuration with only the
   agents and standards that are relevant to your project

## Quick start

Place this folder at `.opencode/skills/setup/` in any project, then ask
your OpenCode agent to run the setup skill:

```
/setup
```

Or use a trigger keyword like "setup", "configure", or "bootstrap".

## What it generates

```
AGENTS.md                  # Project instructions with conditional sections
opencode.json              # Agent registry with conditional entries
.opencode/agent/
  planner.md               # Ticket planning agent
  coding-agent.md          # Code implementation agent (if applicable)
  test-specialist.md       # Test writing agent (if tests detected)
  reviewer.md              # Code review agent
  cleanup-agent.md         # Cleanup/polish agent
  solutions-architect.md   # Architecture agent (if needed)
.opencode/skills/
  ticket-workflow/SKILL.md # Ticket workflow instructions
  git-conventions/SKILL.md # Git branch/commit conventions
  {test-framework}/        # Test framework skill (if tests detected)
  architecture/SKILL.md    # Architecture skill (if arch detected)
docs/Architecture/
  stack.md                 # Technology stack document
  architecture.md          # Architecture overview (if arch detected)
```

## How it works

The skill runs in 6 phases:

1. **Deep scan** — Detects solution type (.NET, Terraform, etc.),
   individual project roles, packages, CI config, coding conventions
2. **Questions** — Only asks about what wasn't detected (test framework,
   ORM, CI platform, etc.)
3. **Build substitution map** — Compiles all answers into a flat
   dictionary of `{{PLACEHOLDER}}` values
4. **Build section flags** — Computes conditional sections (coding
   standards, testing, architecture) based on solution type
5. **File generation** — Reads template files, substitutes placeholders,
   handles existing file consolidation
6. **Optional extras** — Offers grill-me skill installation etc.

## Portability

Copy the entire `.opencode/skills/setup/` folder to any project. No
dependencies, no config files needed outside this folder. Works with
OpenCode, Claude Code, and any other agent platform that loads skills
from `.opencode/skills/`.

## Customization

Edit template files in `templates/` to change the generated output.
Placeholders use `{{UPPER_CASE}}` syntax. Add new templates by adding
entries to the template file map in `SKILL.md`.
