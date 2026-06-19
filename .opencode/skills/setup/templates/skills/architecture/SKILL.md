---
name: architecture
description: Use when designing or reviewing the project structure. Covers {{ARCH_PATTERN}} layers, SOLID principles, CQRS, and dependency rules.
---

# Architecture Standards

See `docs/Architecture/architecture.md` for the architecture diagram and
full documentation. This skill covers rules and conventions.

## Project structure

Project structure is defined by the solution type. For a Clean Architecture
.NET solution, the layout follows:

```
src/
  {Project}.Domain/           # Entities, Value Objects, Enums, Domain Events
  {Project}.Application/      # Commands, Queries, DTOs, Interfaces, Validators
  {Project}.Infrastructure/   # {{ORMLIB}} DbContext, Repositories, External Services
  {Project}.Api/              # Controllers, Middleware, Program.cs
tests/
  {Project}.Domain.Tests/
  {Project}.Application.Tests/
  {Project}.Infrastructure.Tests/
  {Project}.Api.Tests/
```

For non-.NET projects, the structure follows the conventions of the
respective platform.

## Dependency rules

Dependencies flow inward toward the domain/core layer:

```
Domain/Core -> (nothing)
Application/UseCases -> Domain/Core
Infrastructure -> Application
Presentation -> Application
```

Circular dependencies are forbidden.

## SOLID applied

| Principle | How we apply |
|-----------|--------------|
| Single Responsibility | One class = one reason to change |
| Open/Closed | Extend via new handlers or decorators |
| Liskov Substitution | Interface implementations are drop-in |
| Interface Segregation | Small, focused interfaces |
| Dependency Inversion | High-level modules define interfaces; low-level modules implement them |

## CQRS with MediatR (if applicable)

- **Commands**: mutate state. Named `{Action}{Entity}Command`.
- **Queries**: return data. Named `Get{Entity}Query`.
- **Handlers**: `IRequestHandler<TRequest, TResponse>`. One handler per
  command/query.
- **Validators**: FluentValidation `AbstractValidator<T>`.
- **Behaviours**: cross-cutting via `IPipelineBehavior`.

## Exception handling

- Expected failures return result objects with error codes.
- Unexpected exceptions are caught by global middleware.
- Validation failures return structured error responses.
