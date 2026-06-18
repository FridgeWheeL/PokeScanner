---
name: dotnet-testing
description: Use when writing or modifying MSTest tests for .NET projects. Covers naming, structure, mocking, assertions, and conventions.
---

# .NET Testing Standards (MSTest)

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

- **Test runner**: MSTest
- **Mocking**: Moq
- **Assertions**: FluentAssertions or built-in `Assert`

## AAA pattern

```csharp
[TestClass]
public class PricingServiceTests
{
    [TestMethod]
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
[TestMethod]
[DataRow(0, false)]
[DataRow(1, true)]
[DataRow(100, true)]
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
[TestMethod]
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
- using Moq;

```csharp
var mockRepo = new Mock<IRepository>();
mockRepo.Setup(r => r.GetById(It.IsAny<int>())).Returns<Task<Widget>>(null);

var service = new WidgetService(mockRepo.Object);
var result = await service.GetWidget(42);
result.Should().BeNull();
mockRepo.Verify(r => r.GetById(42), Times.Once);
```
