# Code Citations

## License: unknown
https://github.com/signumsoftware/southwind/blob/fbc5d761c46af6e4a2ae13f3717a66c02e28c237/Southwind.Test.Logic/Southwind.Test.Logic.csproj

```
Great question! Let me explain how `dotnet test` works:

## How `dotnet test` Locates and Runs Tests

### 1. **Test Project Discovery**
When you run `dotnet test` in a directory, it:
- Scans for `.csproj` files recursively
- Looks for projects marked with `<IsTestProject>true</IsTestProject>` property
- Alternatively, it looks for projects that reference test framework packages (xUnit, NUnit, MSTest)

In your case, [MLSNext.Tests.csproj](MLSNext.Tests/MLSNext.Tests.csproj) has:
```xml
<PropertyGroup>
    <IsTestProject>true</IsTestProject>
    ...
</PropertyGroup>
```

This explicitly tells dotnet: "I'm a test project."

### 2. **Test Class & Method Discovery**
Once a test project is identified, `dotnet test`:
- Builds the project (compiles to `.dll`)
- Loads the compiled assembly
- Uses a **test adapter** (like xUnit, NUnit, MSTest) to scan for test classes

Your project uses **xUnit**, which looks for:
- Classes with `[Fact]` or `[Theory]` attributes
- Methods decorated with these attributes are test methods
- No naming convention required (unlike NUnit's `Test` suffix)

**Example from your tests:**
```csharp
public class FunctionsIntegrationTests
{
    [Fact]  // ← xUnit looks for this attribute
    public async Task GetMatches_WithNoFilters_ReturnsAllMatches()
    {
        // Test code here
    }
}
```

### 3. **Test Adapters**
Test adapters are the "bridge" between `dotnet test` CLI and specific test frameworks. Your `.csproj` file references:

```xml
<PackageReference Include="xunit" Version="2.6.6" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.6">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtrans
```


## License: unknown
https://github.com/signumsoftware/southwind/blob/fbc5d761c46af6e4a2ae13f3717a66c02e28c237/Southwind.Test.Logic/Southwind.Test.Logic.csproj

```
Great question! Let me explain how `dotnet test` works:

## How `dotnet test` Locates and Runs Tests

### 1. **Test Project Discovery**
When you run `dotnet test` in a directory, it:
- Scans for `.csproj` files recursively
- Looks for projects marked with `<IsTestProject>true</IsTestProject>` property
- Alternatively, it looks for projects that reference test framework packages (xUnit, NUnit, MSTest)

In your case, [MLSNext.Tests.csproj](MLSNext.Tests/MLSNext.Tests.csproj) has:
```xml
<PropertyGroup>
    <IsTestProject>true</IsTestProject>
    ...
</PropertyGroup>
```

This explicitly tells dotnet: "I'm a test project."

### 2. **Test Class & Method Discovery**
Once a test project is identified, `dotnet test`:
- Builds the project (compiles to `.dll`)
- Loads the compiled assembly
- Uses a **test adapter** (like xUnit, NUnit, MSTest) to scan for test classes

Your project uses **xUnit**, which looks for:
- Classes with `[Fact]` or `[Theory]` attributes
- Methods decorated with these attributes are test methods
- No naming convention required (unlike NUnit's `Test` suffix)

**Example from your tests:**
```csharp
public class FunctionsIntegrationTests
{
    [Fact]  // ← xUnit looks for this attribute
    public async Task GetMatches_WithNoFilters_ReturnsAllMatches()
    {
        // Test code here
    }
}
```

### 3. **Test Adapters**
Test adapters are the "bridge" between `dotnet test` CLI and specific test frameworks. Your `.csproj` file references:

```xml
<PackageReference Include="xunit" Version="2.6.6" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.6">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtrans
```


## License: unknown
https://github.com/signumsoftware/southwind/blob/fbc5d761c46af6e4a2ae13f3717a66c02e28c237/Southwind.Test.Logic/Southwind.Test.Logic.csproj

```
Great question! Let me explain how `dotnet test` works:

## How `dotnet test` Locates and Runs Tests

### 1. **Test Project Discovery**
When you run `dotnet test` in a directory, it:
- Scans for `.csproj` files recursively
- Looks for projects marked with `<IsTestProject>true</IsTestProject>` property
- Alternatively, it looks for projects that reference test framework packages (xUnit, NUnit, MSTest)

In your case, [MLSNext.Tests.csproj](MLSNext.Tests/MLSNext.Tests.csproj) has:
```xml
<PropertyGroup>
    <IsTestProject>true</IsTestProject>
    ...
</PropertyGroup>
```

This explicitly tells dotnet: "I'm a test project."

### 2. **Test Class & Method Discovery**
Once a test project is identified, `dotnet test`:
- Builds the project (compiles to `.dll`)
- Loads the compiled assembly
- Uses a **test adapter** (like xUnit, NUnit, MSTest) to scan for test classes

Your project uses **xUnit**, which looks for:
- Classes with `[Fact]` or `[Theory]` attributes
- Methods decorated with these attributes are test methods
- No naming convention required (unlike NUnit's `Test` suffix)

**Example from your tests:**
```csharp
public class FunctionsIntegrationTests
{
    [Fact]  // ← xUnit looks for this attribute
    public async Task GetMatches_WithNoFilters_ReturnsAllMatches()
    {
        // Test code here
    }
}
```

### 3. **Test Adapters**
Test adapters are the "bridge" between `dotnet test` CLI and specific test frameworks. Your `.csproj` file references:

```xml
<PackageReference Include="xunit" Version="2.6.6" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.6">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtrans
```


## License: unknown
https://github.com/signumsoftware/southwind/blob/fbc5d761c46af6e4a2ae13f3717a66c02e28c237/Southwind.Test.Logic/Southwind.Test.Logic.csproj

```
Great question! Let me explain how `dotnet test` works:

## How `dotnet test` Locates and Runs Tests

### 1. **Test Project Discovery**
When you run `dotnet test` in a directory, it:
- Scans for `.csproj` files recursively
- Looks for projects marked with `<IsTestProject>true</IsTestProject>` property
- Alternatively, it looks for projects that reference test framework packages (xUnit, NUnit, MSTest)

In your case, [MLSNext.Tests.csproj](MLSNext.Tests/MLSNext.Tests.csproj) has:
```xml
<PropertyGroup>
    <IsTestProject>true</IsTestProject>
    ...
</PropertyGroup>
```

This explicitly tells dotnet: "I'm a test project."

### 2. **Test Class & Method Discovery**
Once a test project is identified, `dotnet test`:
- Builds the project (compiles to `.dll`)
- Loads the compiled assembly
- Uses a **test adapter** (like xUnit, NUnit, MSTest) to scan for test classes

Your project uses **xUnit**, which looks for:
- Classes with `[Fact]` or `[Theory]` attributes
- Methods decorated with these attributes are test methods
- No naming convention required (unlike NUnit's `Test` suffix)

**Example from your tests:**
```csharp
public class FunctionsIntegrationTests
{
    [Fact]  // ← xUnit looks for this attribute
    public async Task GetMatches_WithNoFilters_ReturnsAllMatches()
    {
        // Test code here
    }
}
```

### 3. **Test Adapters**
Test adapters are the "bridge" between `dotnet test` CLI and specific test frameworks. Your `.csproj` file references:

```xml
<PackageReference Include="xunit" Version="2.6.6" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.6">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtrans
```


## License: unknown
https://github.com/signumsoftware/southwind/blob/fbc5d761c46af6e4a2ae13f3717a66c02e28c237/Southwind.Test.Logic/Southwind.Test.Logic.csproj

```
Great question! Let me explain how `dotnet test` works:

## How `dotnet test` Locates and Runs Tests

### 1. **Test Project Discovery**
When you run `dotnet test` in a directory, it:
- Scans for `.csproj` files recursively
- Looks for projects marked with `<IsTestProject>true</IsTestProject>` property
- Alternatively, it looks for projects that reference test framework packages (xUnit, NUnit, MSTest)

In your case, [MLSNext.Tests.csproj](MLSNext.Tests/MLSNext.Tests.csproj) has:
```xml
<PropertyGroup>
    <IsTestProject>true</IsTestProject>
    ...
</PropertyGroup>
```

This explicitly tells dotnet: "I'm a test project."

### 2. **Test Class & Method Discovery**
Once a test project is identified, `dotnet test`:
- Builds the project (compiles to `.dll`)
- Loads the compiled assembly
- Uses a **test adapter** (like xUnit, NUnit, MSTest) to scan for test classes

Your project uses **xUnit**, which looks for:
- Classes with `[Fact]` or `[Theory]` attributes
- Methods decorated with these attributes are test methods
- No naming convention required (unlike NUnit's `Test` suffix)

**Example from your tests:**
```csharp
public class FunctionsIntegrationTests
{
    [Fact]  // ← xUnit looks for this attribute
    public async Task GetMatches_WithNoFilters_ReturnsAllMatches()
    {
        // Test code here
    }
}
```

### 3. **Test Adapters**
Test adapters are the "bridge" between `dotnet test` CLI and specific test frameworks. Your `.csproj` file references:

```xml
<PackageReference Include="xunit" Version="2.6.6" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.6">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtrans
```


## License: unknown
https://github.com/signumsoftware/southwind/blob/fbc5d761c46af6e4a2ae13f3717a66c02e28c237/Southwind.Test.Logic/Southwind.Test.Logic.csproj

```
Great question! Let me explain how `dotnet test` works:

## How `dotnet test` Locates and Runs Tests

### 1. **Test Project Discovery**
When you run `dotnet test` in a directory, it:
- Scans for `.csproj` files recursively
- Looks for projects marked with `<IsTestProject>true</IsTestProject>` property
- Alternatively, it looks for projects that reference test framework packages (xUnit, NUnit, MSTest)

In your case, [MLSNext.Tests.csproj](MLSNext.Tests/MLSNext.Tests.csproj) has:
```xml
<PropertyGroup>
    <IsTestProject>true</IsTestProject>
    ...
</PropertyGroup>
```

This explicitly tells dotnet: "I'm a test project."

### 2. **Test Class & Method Discovery**
Once a test project is identified, `dotnet test`:
- Builds the project (compiles to `.dll`)
- Loads the compiled assembly
- Uses a **test adapter** (like xUnit, NUnit, MSTest) to scan for test classes

Your project uses **xUnit**, which looks for:
- Classes with `[Fact]` or `[Theory]` attributes
- Methods decorated with these attributes are test methods
- No naming convention required (unlike NUnit's `Test` suffix)

**Example from your tests:**
```csharp
public class FunctionsIntegrationTests
{
    [Fact]  // ← xUnit looks for this attribute
    public async Task GetMatches_WithNoFilters_ReturnsAllMatches()
    {
        // Test code here
    }
}
```

### 3. **Test Adapters**
Test adapters are the "bridge" between `dotnet test` CLI and specific test frameworks. Your `.csproj` file references:

```xml
<PackageReference Include="xunit" Version="2.6.6" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.6">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtrans
```


## License: unknown
https://github.com/signumsoftware/southwind/blob/fbc5d761c46af6e4a2ae13f3717a66c02e28c237/Southwind.Test.Logic/Southwind.Test.Logic.csproj

```
Great question! Let me explain how `dotnet test` works:

## How `dotnet test` Locates and Runs Tests

### 1. **Test Project Discovery**
When you run `dotnet test` in a directory, it:
- Scans for `.csproj` files recursively
- Looks for projects marked with `<IsTestProject>true</IsTestProject>` property
- Alternatively, it looks for projects that reference test framework packages (xUnit, NUnit, MSTest)

In your case, [MLSNext.Tests.csproj](MLSNext.Tests/MLSNext.Tests.csproj) has:
```xml
<PropertyGroup>
    <IsTestProject>true</IsTestProject>
    ...
</PropertyGroup>
```

This explicitly tells dotnet: "I'm a test project."

### 2. **Test Class & Method Discovery**
Once a test project is identified, `dotnet test`:
- Builds the project (compiles to `.dll`)
- Loads the compiled assembly
- Uses a **test adapter** (like xUnit, NUnit, MSTest) to scan for test classes

Your project uses **xUnit**, which looks for:
- Classes with `[Fact]` or `[Theory]` attributes
- Methods decorated with these attributes are test methods
- No naming convention required (unlike NUnit's `Test` suffix)

**Example from your tests:**
```csharp
public class FunctionsIntegrationTests
{
    [Fact]  // ← xUnit looks for this attribute
    public async Task GetMatches_WithNoFilters_ReturnsAllMatches()
    {
        // Test code here
    }
}
```

### 3. **Test Adapters**
Test adapters are the "bridge" between `dotnet test` CLI and specific test frameworks. Your `.csproj` file references:

```xml
<PackageReference Include="xunit" Version="2.6.6" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.6">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtrans
```


## License: unknown
https://github.com/signumsoftware/southwind/blob/fbc5d761c46af6e4a2ae13f3717a66c02e28c237/Southwind.Test.Logic/Southwind.Test.Logic.csproj

```
Great question! Let me explain how `dotnet test` works:

## How `dotnet test` Locates and Runs Tests

### 1. **Test Project Discovery**
When you run `dotnet test` in a directory, it:
- Scans for `.csproj` files recursively
- Looks for projects marked with `<IsTestProject>true</IsTestProject>` property
- Alternatively, it looks for projects that reference test framework packages (xUnit, NUnit, MSTest)

In your case, [MLSNext.Tests.csproj](MLSNext.Tests/MLSNext.Tests.csproj) has:
```xml
<PropertyGroup>
    <IsTestProject>true</IsTestProject>
    ...
</PropertyGroup>
```

This explicitly tells dotnet: "I'm a test project."

### 2. **Test Class & Method Discovery**
Once a test project is identified, `dotnet test`:
- Builds the project (compiles to `.dll`)
- Loads the compiled assembly
- Uses a **test adapter** (like xUnit, NUnit, MSTest) to scan for test classes

Your project uses **xUnit**, which looks for:
- Classes with `[Fact]` or `[Theory]` attributes
- Methods decorated with these attributes are test methods
- No naming convention required (unlike NUnit's `Test` suffix)

**Example from your tests:**
```csharp
public class FunctionsIntegrationTests
{
    [Fact]  // ← xUnit looks for this attribute
    public async Task GetMatches_WithNoFilters_ReturnsAllMatches()
    {
        // Test code here
    }
}
```

### 3. **Test Adapters**
Test adapters are the "bridge" between `dotnet test` CLI and specific test frameworks. Your `.csproj` file references:

```xml
<PackageReference Include="xunit" Version="2.6.6" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.6">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtrans
```


## License: unknown
https://github.com/signumsoftware/southwind/blob/fbc5d761c46af6e4a2ae13f3717a66c02e28c237/Southwind.Test.Logic/Southwind.Test.Logic.csproj

```
Great question! Let me explain how `dotnet test` works:

## How `dotnet test` Locates and Runs Tests

### 1. **Test Project Discovery**
When you run `dotnet test` in a directory, it:
- Scans for `.csproj` files recursively
- Looks for projects marked with `<IsTestProject>true</IsTestProject>` property
- Alternatively, it looks for projects that reference test framework packages (xUnit, NUnit, MSTest)

In your case, [MLSNext.Tests.csproj](MLSNext.Tests/MLSNext.Tests.csproj) has:
```xml
<PropertyGroup>
    <IsTestProject>true</IsTestProject>
    ...
</PropertyGroup>
```

This explicitly tells dotnet: "I'm a test project."

### 2. **Test Class & Method Discovery**
Once a test project is identified, `dotnet test`:
- Builds the project (compiles to `.dll`)
- Loads the compiled assembly
- Uses a **test adapter** (like xUnit, NUnit, MSTest) to scan for test classes

Your project uses **xUnit**, which looks for:
- Classes with `[Fact]` or `[Theory]` attributes
- Methods decorated with these attributes are test methods
- No naming convention required (unlike NUnit's `Test` suffix)

**Example from your tests:**
```csharp
public class FunctionsIntegrationTests
{
    [Fact]  // ← xUnit looks for this attribute
    public async Task GetMatches_WithNoFilters_ReturnsAllMatches()
    {
        // Test code here
    }
}
```

### 3. **Test Adapters**
Test adapters are the "bridge" between `dotnet test` CLI and specific test frameworks. Your `.csproj` file references:

```xml
<PackageReference Include="xunit" Version="2.6.6" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.6">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtrans
```


## License: unknown
https://github.com/signumsoftware/southwind/blob/fbc5d761c46af6e4a2ae13f3717a66c02e28c237/Southwind.Test.Logic/Southwind.Test.Logic.csproj

```
Great question! Let me explain how `dotnet test` works:

## How `dotnet test` Locates and Runs Tests

### 1. **Test Project Discovery**
When you run `dotnet test` in a directory, it:
- Scans for `.csproj` files recursively
- Looks for projects marked with `<IsTestProject>true</IsTestProject>` property
- Alternatively, it looks for projects that reference test framework packages (xUnit, NUnit, MSTest)

In your case, [MLSNext.Tests.csproj](MLSNext.Tests/MLSNext.Tests.csproj) has:
```xml
<PropertyGroup>
    <IsTestProject>true</IsTestProject>
    ...
</PropertyGroup>
```

This explicitly tells dotnet: "I'm a test project."

### 2. **Test Class & Method Discovery**
Once a test project is identified, `dotnet test`:
- Builds the project (compiles to `.dll`)
- Loads the compiled assembly
- Uses a **test adapter** (like xUnit, NUnit, MSTest) to scan for test classes

Your project uses **xUnit**, which looks for:
- Classes with `[Fact]` or `[Theory]` attributes
- Methods decorated with these attributes are test methods
- No naming convention required (unlike NUnit's `Test` suffix)

**Example from your tests:**
```csharp
public class FunctionsIntegrationTests
{
    [Fact]  // ← xUnit looks for this attribute
    public async Task GetMatches_WithNoFilters_ReturnsAllMatches()
    {
        // Test code here
    }
}
```

### 3. **Test Adapters**
Test adapters are the "bridge" between `dotnet test` CLI and specific test frameworks. Your `.csproj` file references:

```xml
<PackageReference Include="xunit" Version="2.6.6" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.6">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

These packages contain:
```


## License: unknown
https://github.com/signumsoftware/southwind/blob/fbc5d761c46af6e4a2ae13f3717a66c02e28c237/Southwind.Test.Logic/Southwind.Test.Logic.csproj

```
Great question! Let me explain how `dotnet test` works:

## How `dotnet test` Locates and Runs Tests

### 1. **Test Project Discovery**
When you run `dotnet test` in a directory, it:
- Scans for `.csproj` files recursively
- Looks for projects marked with `<IsTestProject>true</IsTestProject>` property
- Alternatively, it looks for projects that reference test framework packages (xUnit, NUnit, MSTest)

In your case, [MLSNext.Tests.csproj](MLSNext.Tests/MLSNext.Tests.csproj) has:
```xml
<PropertyGroup>
    <IsTestProject>true</IsTestProject>
    ...
</PropertyGroup>
```

This explicitly tells dotnet: "I'm a test project."

### 2. **Test Class & Method Discovery**
Once a test project is identified, `dotnet test`:
- Builds the project (compiles to `.dll`)
- Loads the compiled assembly
- Uses a **test adapter** (like xUnit, NUnit, MSTest) to scan for test classes

Your project uses **xUnit**, which looks for:
- Classes with `[Fact]` or `[Theory]` attributes
- Methods decorated with these attributes are test methods
- No naming convention required (unlike NUnit's `Test` suffix)

**Example from your tests:**
```csharp
public class FunctionsIntegrationTests
{
    [Fact]  // ← xUnit looks for this attribute
    public async Task GetMatches_WithNoFilters_ReturnsAllMatches()
    {
        // Test code here
    }
}
```

### 3. **Test Adapters**
Test adapters are the "bridge" between `dotnet test` CLI and specific test frameworks. Your `.csproj` file references:

```xml
<PackageReference Include="xunit" Version="2.6.6" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.6">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

These packages contain:
```

