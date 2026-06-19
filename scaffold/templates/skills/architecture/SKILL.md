---
name: architecture
description: Use when designing or reviewing the project structure. Covers {{ARCH_PATTERN}} layers, SOLID principles, CQRS, and dependency rules.
---

# Architecture Standards

See `docs/architecture.md` for the architecture diagram and full
documentation. This skill covers rules and conventions.

## Solution structure

```
src/
  {Project}.Domain/           # Entities, Value Objects, Enums, Domain Events
  {Project}.Application/      # Commands, Queries, DTOs, Interfaces, Mapping,
                              #   Validators
  {Project}.Infrastructure/   # {{ORMLIB}} DbContext, Repositories,
                              #   External Services
  {Project}.Api/              # Controllers, Middleware, Program.cs
tests/
  {Project}.Domain.Tests/
  {Project}.Application.Tests/
  {Project}.Infrastructure.Tests/
  {Project}.Api.Tests/
```

## Dependency rules

```
Domain -> (nothing)
Application -> Domain
Infrastructure -> Application
Presentation (Api) -> Application
```

- **Domain**: Zero dependencies. Pure C#.
- **Application**: Depends only on Domain. Contains MediatR handlers.
- **Infrastructure**: Depends on Application. Implements Application
  interfaces.
- **Api**: Depends on Application. Registers Infrastructure via DI in
  Program.cs.

Circular dependencies are forbidden.

## SOLID applied

| Principle | How we apply |
|-----------|--------------|
| Single Responsibility | One class = one reason to change |
| Open/Closed | Extend via new handlers or decorators |
| Liskov Substitution | Interface implementations are drop-in |
| Interface Segregation | Small, focused interfaces |
| Dependency Inversion | High-level modules define interfaces; low-level modules implement them |

## CQRS with MediatR

- **Commands**: mutate state. Named `{Action}{Entity}Command`.
- **Queries**: return data. Named `Get{Entity}Query`.
- **Handlers**: `IRequestHandler<TRequest, TResponse>`. One handler per
  command/query.
- **Validators**: FluentValidation `AbstractValidator<T>`.
- **Behaviours**: cross-cutting via `IPipelineBehavior`.

```csharp
public record CreateOrderCommand(CreateOrderDto Dto) : IRequest<OrderDto>;

public class CreateOrderCommandHandler
    : IRequestHandler<CreateOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(
        CreateOrderCommand request, CancellationToken ct) { ... }
}
```

## Dependency Injection registration

- Application layer: `AddApplication()` extension that registers MediatR,
  validators, mapping.
- Infrastructure layer: `AddInfrastructure()` extension that registers
  DbContext, repositories.
- Called from `Program.cs`.

## Exception handling

- Global middleware catches unhandled exceptions -> structured error
  response.
- Expected failures return `Result<T>` with error codes.
- Validation failures return `ProblemDetails` with field-level errors.
