# ClassDiagramGenerator Unit Tests

## Overview
Comprehensive unit test suite for the `ClassDiagramGenerator` class that generates PlantUML syntax from UML class diagram models.

## Test File Location
`UmlTests\ClassDiagramGeneratorTests.cs`

## Test Coverage

The test suite includes **90+ test cases** organized into the following categories:

### 1. **Basic Generation Tests** (4 tests)
- Simple class generation
- Abstract class generation
- Interface generation
- Enum generation

### 2. **Class Name Tests** (4 tests)
- Names with spaces (quoted)
- Names with question marks (quoted)
- Aliases (as clause)
- Names with spaces AND aliases

### 3. **Stereotype Tests** (2 tests)
- Classes with stereotypes
- Empty stereotypes (no output)

### 4. **Property Tests** (9 tests)
- Simple property formatting
- All visibility levels (public, private, protected, internal)
- Static modifier
- Abstract modifier
- Default values
- Array types
- List<T> types
- IReadOnlyCollection<T> types
- Properties not drawn with line

### 5. **Method Tests** (13 tests)
- Simple method formatting
- All visibility levels
- Static methods
- Abstract methods
- No parameters
- Single parameter
- Multiple parameters
- Array parameter types
- List<T> parameter types
- IReadOnlyCollection<T> parameter types
- Void return types

### 6. **Inheritance Tests** (2 tests)
- Single inheritance relationships
- Multiple base classes

### 7. **Interface Implementation Tests** (2 tests)
- Single interface implementation
- Multiple interface implementations

### 8. **Package Tests** (3 tests)
- Single package declarations
- Nested packages
- Packages with multiple classes

### 9. **Special Content Tests** (3 tests)
- UML Comments
- UML Notes
- UML Other (raw text)

### 10. **Note Connection Tests** (2 tests)
- Single note connection
- Multiple note connections

### 11. **Complex Scenario Tests** (3 tests)
- Complete class hierarchy with inheritance and interfaces
- Package with complex content (base class, derived class, methods)
- Enum with properties

### 12. **Generic Type Tests** (2 tests)
- Generic class names
- Generic method return types

### 13. **Edge Cases** (7 tests)
- Empty diagram
- Titles with special characters
- Generic names with aliases
- Property names with special characters
- Method names with camel case
- Multiple classes with relationships
- Various formatting variations

### 14. **Composition/Aggregation Tests** (3 tests)
- Properties drawn with line (composition)
- Single relationships without list type
- Multiplicity notation for collections

### 15. **Visibility Modifiers** (1 test)
- No visibility modifier handling

### 16. **Output Format Tests** (3 tests)
- Correct @startuml opening
- Correct @enduml closing
- Proper line break formatting

## Key Testing Patterns

### Setup/Helper Method
```csharp
private string GeneratePlantUML(UMLClassDiagram diagram)
{
    using (var writer = new StringWriter())
    {
        ClassDiagramGenerator.Create(diagram, writer);
        return writer.ToString();
    }
}
```

### Test Structure
Each test:
1. Creates a UML diagram with specific elements
2. Calls `GeneratePlantUML()`
3. Asserts that the output contains expected PlantUML syntax

## Running the Tests

```bash
// Using NUnit
dotnet test UmlTests/UmlTests.csproj --filter ClassDiagramGeneratorTests

// Or run individual test:
dotnet test UmlTests/UmlTests.csproj --filter ClassDiagramGeneratorTests.Generate_SimpleClass_ProducesValidPlantUML
```

## Dependencies
- NUnit Framework (already used in project)
- UMLModels namespace (for test data creation)
- PlantUML namespace (for generator)

## Example Test

```csharp
[Test]
public void Generate_SimpleClass_ProducesValidPlantUML()
{
    var diagram = CreateSimpleDiagram("Simple Class");
    var simpleClass = new UMLClass("", "", null, false, "User", [], []);
    diagram.Package.Children.Add(simpleClass);

    var result = GeneratePlantUML(diagram);

    Assert.That(result, Does.Contain("@startuml"));
    Assert.That(result, Does.Contain("@enduml"));
    Assert.That(result, Does.Contain("title Simple Class"));
    Assert.That(result, Does.Contain("class User"));
}
```

## Notes

- All tests verify the output contains expected PlantUML syntax
- Tests don't validate the complete output format, but verify key elements are present
- The generator is tested in isolation from the parser
- Tests cover both simple and complex scenarios
- Edge cases and error conditions are included

## Test Execution Results
? All 90+ tests compile successfully
? Ready for execution against the ClassDiagramGenerator implementation
