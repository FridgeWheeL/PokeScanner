# Solution Architecture

Maintained by `@solutions-architect`. Update whenever the architecture
changes.

## Architecture Overview

```mermaid
graph TD
    subgraph Domain["Domain Layer"]
        D_ENT["Entities"]
        D_VO["Value Objects"]
        D_EVT["Domain Events"]
        D_ENM["Enums"]
    end

    subgraph Application["Application Layer"]
        A_CMD["Commands / Queries"]
        A_DTO["DTOs"]
        A_INT["Interfaces"]
        A_VAL["Validators"]
        A_PIP["Pipeline Behaviours"]
    end

    subgraph Infrastructure["Infrastructure Layer"]
        I_DB["{{ORMLIB}} DbContext"]
        I_REP["Repository Implementations"]
        I_EXT["External Service Clients"]
    end

    subgraph Presentation["Presentation Layer"]
        P_CTL["Controllers / Endpoints"]
        P_MDW["Middleware"]
        P_PRG["Program.cs / DI Setup"]
    end

    Application --> Domain
    Infrastructure --> Application
    Presentation --> Application
    Infrastructure -.-> Application
```

## Dependency Rule

```
Domain (zero dependencies)
    ^
Application (depends on Domain only)
    ^
Infrastructure (depends on Application)
    ^
Presentation (depends on Application)
```

## OpenCode Agent Workflow

```mermaid
graph LR
    USR["User: /ticket TICKET-1"]

    subgraph Artifacts["Ticket Artifacts (docs/TICKET-1/)"]
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

## CQRS Flow

```mermaid
sequenceDiagram
    participant Client
    participant Controller
    participant MediatR
    participant Pipeline
    participant Handler
    participant Repository
    participant Db

    Client->>Controller: POST /api/orders
    Controller->>MediatR: Send(Command)
    MediatR->>Pipeline: Run Behaviours
    Pipeline->>Pipeline: Validation
    Pipeline->>Handler: Handle(command)
    Handler->>Repository: Add(entity)
    Repository->>Db: SaveChanges()
    Handler-->>Controller: Dto
    Controller-->>Client: 201 Created
```
