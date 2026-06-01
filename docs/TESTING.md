# Testing Strategy — Mono.TextTemplating

Comprehensive test coverage for the T4 template engine. **65 tests, all passing.**

## Test Suite Overview

| Test File | Tests | Focus |
|---|---|---|
| `ParsingTests.cs` | 2 | Tokeniser state machine, directive parsing |
| `GenerationTests.cs` | 3 | CodeDOM code generation with newline variants |
| `TemplateEnginePreprocessTemplateTests.cs` | 2 | Preprocessed templates, include+class feature combination |
| `TemplatingEngineEdgeCases.cs` | 27 | Engine edge cases — bare `return;`, class features, imports, blocks |
| `RoslynCompilerTests.cs` | 8 | Roslyn backend — framework refs, duplicates, debug, language mapping |
| `DiTemplateHostTests.cs` | 20 | DI host — service resolution, session, parameters |
| `SignalRTemplateTests.cs` | 8 | Full hub → TypeScript template integration |
| **Total** | **65** | |

## Edge Cases Covered

### Bare `return;` in TransformText (CRITICAL)
T4 compiles `<# ... #>` blocks into `TransformText()` which returns `string`. A bare `return;` is invalid C#.

```t4
<!-- BROKEN — "An object of a type convertible to 'string' is required" -->
<# return; #>

<!-- FIXED — use if/else nesting -->
<# if (condition) { #>content<# } else { Write("fallback"); } #>
```

**Tests:** `BareReturnInMainBlock_ShouldReportClearError`, `BareReturnInHostSpecificTemplate_ShouldReportClearError`

### Class Feature Methods
Verifies `<#+ ... #>` helper methods work with: `typeof()`, generic type definitions (`typeof(List<>)`, `typeof(Task<>)`, `typeof(Nullable<>)`), and multiple methods.

**Tests:** `MultipleClassFeatureMethods_ShouldCompile`, `ClassFeatureWithTypeofExpressions_ShouldCompile`, `ClassFeatureWithGenericTypeDefinitions_ShouldCompile`, `ClassFeatureWithTaskTypeof_ShouldCompile`

### Nested `if/else` Across Content Blocks
T4 supports splitting control flow across content blocks:

```t4
<# if (a) { #>A<# } else { #>B<# } #>
```

**Tests:** `NestedIfElseAcrossContentBlocks_ShouldCompile`, `DeeplyNestedIfElse_ShouldCompile`

### Import + Class Feature Interactions
Verifies `System.IO`, `System.Reflection`, and `System.Linq` imports don't break class feature compilation.

**Tests:** `SystemIOImportWithClassFeatures_ShouldCompile`, `SystemIOFullyQualifiedWithClassFeatures_ShouldCompile`, `SystemReflectionImportWithClassFeatures_ShouldCompile`, `DuplicateImports_ShouldNotCauseError`

### Host-Specific Templates
`hostspecific="true"` generates a `Host` property on the generated class.

**Tests:** `HostSpecificTemplate_ShouldGenerateHostProperty`, `TemplateWithNullConditionalHostAccess_ShouldCompile`

### Error Handling
Templates with invalid code should return `null` + errors, not throw.

**Tests:** `CompilerError_ShouldNotCrashEngine`, `MissingLanguage_ShouldReportError`, `UnresolvedAssemblyReference_ShouldReportError`

### Roslyn Compiler
Framework assembly names (e.g. "System.Linq") are filtered — only file paths create `MetadataReference`.

**Tests:** `FrameworkAssemblyNames_ShouldBeFilteredOut`, `DuplicateReferences_ShouldNotCauseError`, `NullReferences_ShouldBeFilteredOut`, `DebugMode_ShouldIncludeDebugInfo`, `InvalidCSharpCode_ShouldReturnErrors`, `LanguageVersionMapping_ShouldSupportCommonT4Strings`

### SignalR Template Integration
The hub→TypeScript template compiles, generates valid output, and handles missing assemblies gracefully.

**Tests:** `FullHubGeneratorTemplate_ShouldCompile`, `TemplateWithoutTargetAssembly_ShouldReturnPlaceholder`, `TemplateWithCurrentAssembly_ShouldDiscoverTypes`, `TemplateGeneratesValidTypeScript_ForKnownMethod`, `TemplateWithAssemblyGetTypes_ShouldNotThrowReflectionTypeLoadException`

### DI Host (`DiTemplateHost` / `TemplatingHostBuilder`)
Fluent builder API, service resolution, session state, parameter passing.

**Tests:** `Build_Host_ShouldImplementHostInterface`, `GetService_ShouldReturnRegisteredService`, `WithSession_ShouldSetSessionState`, `WithParameter_ShouldBeResolvable`, `ResolveParameterValue_FallsBackToDi_WhenNotRegistered`, `ProcessTemplateContent_ShouldWork`

## Running Tests

```bash
# All tests
dotnet test tests/Mono.TextTemplating.Tests -c Release

# Specific category
dotnet test tests/Mono.TextTemplating.Tests -c Release --filter "FullyQualifiedName~EdgeCases"

# With coverage
dotnet test tests/Mono.TextTemplating.Tests -c Release --collect:"XPlat Code Coverage"
```

## Test Fixture Pattern

```csharp
public class MyTests
{
    TemplatingEngine engine = new TemplatingEngine();
    DummyHost host = new DummyHost();

    public MyTests()
    {
        host.StandardAssemblyReferences.Add(typeof(TextTransformation).Assembly.Location);
        host.StandardImports.Add("System");
    }

    [Fact]
    public void MyTest()
    {
        var template = @"<#@ template language=""C#"" #>...";
        var result = engine.ProcessTemplate(template, host);
        Assert.False(host.Errors.HasErrors);
        AssertOutput("expected", result);
    }

    static void AssertOutput(string expected, string actual)
    {
        expected = expected.Replace("\r\n", "\n").Replace("\r", "\n");
        actual = (actual ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
        Assert.Equal(expected, actual);
    }
}
```
