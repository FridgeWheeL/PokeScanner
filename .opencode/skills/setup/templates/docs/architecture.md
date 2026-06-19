# Solution Architecture

Maintained by `@solutions-architect`. Update whenever the architecture
changes.

## Architecture Overview

{{ARCHITECTURE_DIAGRAM}}

## Dependency Rules

{{DEPENDENCY_RULES}}

## Project Roles

{{PROJECT_ROLES_TABLE}}

## OpenCode Agent Workflow

```mermaid
graph LR
    USR["User: /ticket TICKETN"]

    subgraph Artifacts["Ticket Artifacts (docs/Tasks/TICKETN-*/)"]
        REQ["requirement.md"]
        STA["status.md"]
        PLAN["plan.md"]
    end

    subgraph Agents["OpenCode Sub-agents"]
        PLA["@planner"]
        COD["@coding-agent"]
        TST["@test-specialist"]
        REV["@reviewer"]
        CLN["@cleanup-agent"]
        ARC["@solutions-architect"]
    end

    USR --> REQ
    USR --> STA
    PLA --> PLAN
    COD --> SRC["src/"]
    TST --> TESTS["tests/"]
    REV --> ALL["All Files"]
    CLN --> ALL
    ARC -.-> PLA
    ARC -.-> ARCH["docs/*"]
```
