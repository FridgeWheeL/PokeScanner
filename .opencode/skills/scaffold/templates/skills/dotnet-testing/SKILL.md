---
name: {{TEST_SKILL}}
description: Use when writing or modifying {{TEST_FRAMEWORK}} tests for .NET projects. Covers naming, structure, mocking, assertions, and conventions.
---

# .NET Testing Standards ({{TEST_FRAMEWORK}})

## Project structure

- Test project lives at `tests/{Project}.Tests/` mirroring
  `src/{Project}/`.
- One test class per production class.
- Namespace matches the source namespace + `.Tests`.
- Class name: `{ClassName}Tests`.

## Naming

```
MethodName_StateUnderTest_ExpectedBehavior
```

Examples:
- `GetById_WhenEntityExists_ReturnsEntity()`
- `CreateOrder_WhenStockInsufficient_ThrowsValidationException()`

## Framework & tooling

- **Test runner**: {{TEST_FRAMEWORK}}
- **Mocking**: {{MOCK_LIBRARY}}
- **Assertions**: FluentAssertions or built-in `Assert`

## AAA pattern

```csharp
{{TEST_CLASS_ATTR}}
public class PricingServiceTests
{
    {{TEST_METHOD_ATTR}}
    public void CalculateTotal_WhenDiscountApplied_ReturnsDiscountedAmount()
    {
        // Arrange
        var service = new PricingService();

        // Act
        var result = service.CalculateTotal(100m, 0.1m);

        // Assert
        result.Should().Be(90m);
    }
}
```

Use blank lines between AAA sections.

## Parameterised tests

```csharp
{{TEST_METHOD_ATTR}}
{{TEST_DATA_ROW}}(0, false)
{{TEST_DATA_ROW}}(1, true)
{{TEST_DATA_ROW}}(100, true)
public void IsEligible_WhenAgeIsGiven_ReturnsExpectedResult(
    int age, bool expected)
{
    // Arrange
    var service = new EligibilityService();

    // Act
    var result = service.IsEligible(age);

    // Assert
    result.Should().Be(expected);
}
```

## Exception testing

```csharp
{{TEST_METHOD_ATTR}}
public void GetById_WhenIdIsNegative_ThrowsArgumentOutOfRangeException()
{
    // Arrange
    var service = new FooService();

    // Act & Assert
    Assert.ThrowsException<ArgumentOutOfRangeException>(
        () => service.GetById(-1));
}
```

## Mocking rules

- Mock only external dependencies (repositories, HTTP clients, file system).
- Never mock the system under test.

```csharp
{{MOCK_USING}}

var repo = {{MOCK_CREATE}};
repo.GetById(Arg.Any<int>()).Returns((Widget)null);

var service = new WidgetService(repo);
var result = service.GetWidget(42);
result.Should().BeNull();
repo{{MOCK_RECEIVE}}(1).GetById(42);
```
