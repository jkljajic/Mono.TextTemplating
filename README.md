# Mono.TextTemplating — Modern Cross-Platform T4 Engine

[![Build & Test](https://github.com/jkljajic/Mono.TextTemplating/actions/workflows/ci.yml/badge.svg)](https://github.com/jkljajic/Mono.TextTemplating/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Mono.TextTemplating.svg)](https://www.nuget.org/packages/Mono.TextTemplating/)

**A modern, cross-platform implementation of Visual Studio's T4 text templating engine.** Process `.tt` template files on .NET 10 with Roslyn compilation, DI-aware hosting, MSBuild integration, and a rich extensibility model.

---

## Quick Start

```bash
# Install CLI tool
dotnet tool install -g Mono.TextTemplating.Cli

# Transform a template
dotnet t4 -o output.cs template.tt

# Preprocess into a runtime class
dotnet t4 -c MyClass template.tt

# Full CLI options with references, imports, and parameters
dotnet t4 -r MyAssembly.dll -u System.Linq -o output.cs -P params template.tt
```

```csharp
// NuGet: dotnet add package Mono.TextTemplating

var generator = new TemplateGenerator();
generator.Refs.Add(typeof(MyType).Assembly.Location);
generator.Imports.Add("System.Linq");
generator.AddParameter(null, null, "UserName", "Alice");
generator.ProcessTemplate("template.tt", "output.txt");
```

---

## Features

### Template Engine

| Feature | Description |
|---|---|
| Full T4 syntax | `<# #>`, `<#= #>`, `<#+ #>`, `<#@ #>` directives |
| Expression blocks | `<#= expression #>` with implicit `ToString()` |
| Class features | `<#+ ... #>` helper methods, nested classes, fields |
| Control flow nesting | `if/else`, `foreach`, `for`, `while` across content blocks |
| Include directives | `<#@ include file="shared.tt" #>` with recursive resolution |
| Custom directive processors | Extensible via `DirectiveProcessor` and `RequiresProvidesDirectiveProcessor` |
| Parameter directives | `<#@ parameter name="x" type="int" #>` — resolved via session, DI, or CallContext (NETFX) |
| Assembly & import directives | `<#@ assembly #>`, `<#@ import #>` with Roslyn-backed resolution |
| Host-specific templates | `hostspecific="true"` exposes `this.Host` for runtime reflection |
| Debug mode | `debug="true"` emits `#line` directives for accurate error reporting |
| Preprocessed templates | `PreprocessTemplate()` generates standalone runtime classes |

### Compilation

| Feature | Description |
|---|---|
| Roslyn backend (default) | `Microsoft.CodeAnalysis.CSharp` — fast, modern, in-memory compilation |
| CodeDOM backend (legacy) | `System.CodeDom` — cross-platform on .NET Framework |
| Switchable compiler | `TemplateSettings.CompilerType = TemplateCompilerType.Roslyn` or `CodeDom` |
| Debug symbols | `debug="true"` emits PDBs and `#line` directives via Roslyn |
| Cross-platform | Windows, macOS, Linux — no Visual Studio or full .NET Framework required |

### DI-Aware Host

```csharp
var host = new TemplatingHostBuilder(serviceProvider)
    .WithAssemblyReference(typeof(MyService).Assembly.Location)
    .WithImport("System.Linq")
    .WithParameter(null, null, "ConnectionString", connStr)
    .WithSession(new Dictionary<string, object> { ["User"] = currentUser })
    .Build();

// Use the fluent builder directly
new TemplatingHostBuilder(services)
    .WithAssemblyReference(assemblyPath)
    .ProcessTemplateFile("template.tt", "output.txt");
```

The `DiTemplateHost` implements `ITextTemplatingEngineHost` and `ITextTemplatingSessionHost`, resolving services from the DI container and supporting session state and parameter passing.

### CLI Tool (`dotnet-t4`)

```
Usage: dotnet t4 [options] input-file

Options:
  -o, --out=VALUE          Output file (default: input.generated.ext)
  -r=VALUE                 Reference assembly (repeatable)
  -u=VALUE                 Import namespace (repeatable)
  -I=VALUE                 Include search path (repeatable)
  -P=VALUE                 Reference search path (repeatable)
  -dp=VALUE                Directive processor (name!class!assembly)
  -a=VALUE                 Parameter ([processorName]![directiveName]!name!value)
  -c=VALUE                 Preprocess into named class (namespace.classname)
  -h, -?, --help           Show help
```

The CLI auto-detects the output extension from `<#@ output extension="..." #>` (reads first 20 lines of template).

### MSBuild Integration

Add the `Mono.TextTemplating.Build` package to auto-transform `.tt` files during `dotnet build`:

```xml
<PackageReference Include="Mono.TextTemplating.Build" Version="3.0.0-*" PrivateAssets="all" />
```

**How it works:**

- `.props` auto-discovers `**/*.tt` files into the `T4PostBuildTemplate` item group
- `.targets` runs the `TransformT4Templates` task **post-build**, passing the target assembly and project references
- Generated files are added to `FileWrites` (Clean support) and `Content`
- Set `EnableDefaultT4Items=false` to disable auto-discovery
- Set `T4Preprocess=true` to generate runtime classes instead of output files

**MSBuild task properties:**

```xml
<TransformTemplates
  Templates="@(T4PostBuildTemplate)"
  OutputDir="$(IntermediateOutputPath)GeneratedT4"
  References="@(ReferencePath)"
  Imports="System.Linq;System.Collections.Generic"
  Preprocess="false">
  <Output TaskParameter="GeneratedFiles" ItemName="Compile" />
</TransformTemplates>
```

**Passing parameters via item metadata:**

```xml
<ItemGroup>
  <T4PostBuildTemplate Include="Templates\*.tt">
    <T4Param_TargetAssembly>$(TargetPath)</T4Param_TargetAssembly>
    <T4Param_BuildConfiguration>$(Configuration)</T4Param_BuildConfiguration>
  </T4PostBuildTemplate>
</ItemGroup>
```

Any metadata named `T4Param_<Key>` is passed to the template as a parameter `<Key, Value>`.

### SignalR → TypeScript Code Generation

See [`samples/SignalR.TsGeneration/`](samples/SignalR.TsGeneration/) — a complete end-to-end sample demonstrating T4-powered code generation in a real-world scenario:

- **Hub discovery** — `TemplateDiscovery.FindSubclassesOf<Hub>()` finds all SignalR hub types in referenced assemblies
- **Method discovery** — `GetTaskMethods()` filters to public, instance methods returning `Task`/`Task<T>`, excluding statics, non-public, events, and `CancellationToken` parameters
- **TypeScript type mapping** — `TsType()` maps C# types to TypeScript:
  - Primitives: `string`, `boolean`, `number`
  - `DateTime`/`Guid` → `string`
  - Enums → `number`, Arrays → `T[]`
  - `Nullable<T>` → `T | null`, `Task<T>` → `Promise<T>`
  - `List<T>`/`IEnumerable<T>` → `T[]`
  - `Dictionary<K,V>` → `Record<K, V>`
- **Generated output** — Connection factories, method stubs, JSDoc annotations, camelCase naming
- **Build integration** — `.tt` → `.ts` via post-build MSBuild target

```bash
dotnet build samples/SignalR.TsGeneration -c Release
# → bin/Release/net10.0/HubClientGenerator.ts
```

### Template Directives Reference

| Directive | Syntax | Description |
|---|---|---|
| `template` | `<#@ template language="C#" hostspecific="true" debug="true" #>` | Sets language, host-specific mode, debug |
| `output` | `<#@ output extension=".cs" encoding="utf-8" #>` | Sets file extension and encoding |
| `assembly` | `<#@ assembly name="MyAssembly.dll" #>` | References an assembly |
| `import` | `<#@ import namespace="System.Linq" #>` | Imports a namespace |
| `include` | `<#@ include file="shared.tt" #>` | Includes another template (recursive) |
| `parameter` | `<#@ parameter name="UserName" type="string" #>` | Declares a template parameter |
| `custom` | `<#@ myProcessor attr="value" #>` | Custom directive processor |

---

## Architecture

```
.tt file (string content)
        │
        ▼
┌──────────────────────────────────────────────┐
│  Tokeniser (State machine)                    │
│  ─────────────────────────                   │
│  Scans character-by-character for T4 tags:    │
│    <# ... #>  → Block                         │
│    <#= ... #> → Expression                    │
│    <#+ ... #> → Helper (class feature)        │
│    <#@ ... #> → Directive                     │
└──────────────────┬───────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────┐
│  ParsedTemplate                              │
│  ────────────────                            │
│  - Groups directive attributes               │
│  - Recursively resolves <#@ include #>       │
│  - Validates directive structure             │
│  Output: List<Directive> + List<TemplateSegment> │
└──────────────────┬───────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────┐
│  TemplatingEngine                            │
│  ─────────────────                           │
│  1. GetSettings() — parse template directive  │
│  2. ProcessDirectives() — run processors      │
│  3. GenerateCompileUnit() — build CodeDOM AST │
│  4. GenerateCode() — Roslyn / CodeDOM compile │
│  5. Create CompiledTemplate — wrap result     │
└──────────────────┬───────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────┐
│  CompiledTemplate                            │
│  ────────────────                            │
│  - Loads compiled assembly                   │
│  - Instantiates generated class              │
│  - Wires Host/Session properties             │
│  - .Process() runs TransformText()           │
│  - Returns string output                     │
└──────────────────────────────────────────────┘
```

### Key Types

| Type | LOC | Role |
|---|---|---|
| `TemplatingEngine` | 973 | Core orchestrator — parsing, AST generation, compilation, execution |
| `TemplateGenerator` | 395 | Default host — file I/O, assembly resolution, process/preprocess API |
| `Tokeniser` | 295 | State-machine lexer scanning `.tt` content for T4 tags |
| `ParsedTemplate` | 339 | AST builder — directive parsing, include resolution |
| `CompiledTemplate` | 113 | Runtime wrapper — loads and executes compiled template assembly |
| `TemplateSettings` | 75 | Settings bag parsed from `<#@ template #>` |
| `TextTransformation` | 219 | Base class — `Write`, `WriteLine`, `Error`, indentation |
| `TemplateDiscovery` | — | Assembly reflection helpers — type/method discovery |
| `RoslynTemplateCompiler` | — | Roslyn compilation backend |
| `CodeDomTemplateCompiler` | — | Legacy CodeDOM compilation backend |
| `DiTemplateHost` | — | DI-aware host implementing `ITextTemplatingEngineHost` |
| `TemplatingHostBuilder` | 108 | Fluent builder for configuring the DI host |

### Namespaces

| Namespace | Purpose |
|---|---|
| `Mono.TextTemplating` | Engine core — tokenizer, parser, compiler, template runner, DI host |
| `Microsoft.VisualStudio.TextTemplating` | Public API contracts matching Visual Studio T4 SDK |

More details in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

---

## Packages

| Package | Purpose |
|---|---|
| `Mono.TextTemplating` | Core T4 engine library |
| `Mono.TextTemplating.Build` | MSBuild task with `.props`/`.targets` for `dotnet build` integration |
| `Mono.TextTemplating.Cli` | `dotnet-t4` global CLI tool |

All packages target `net10.0`. Debug symbols, source link, and snupkg symbol packages are included.

---

## Build & Test

```bash
# Restore, build, and test
dotnet restore
dotnet build -c Release
dotnet test tests/Mono.TextTemplating.Tests -c Release

# Run with code coverage
dotnet test tests/Mono.TextTemplating.Tests -c Release --collect:"XPlat Code Coverage"

# Filter specific test categories
dotnet test tests/Mono.TextTemplating.Tests -c Release --filter "FullyQualifiedName~EdgeCases"
dotnet test tests/Mono.TextTemplating.Tests -c Release --filter "FullyQualifiedName~Roslyn"
dotnet test tests/Mono.TextTemplating.Tests -c Release --filter "FullyQualifiedName~DiTemplate"
```

**65 tests, all passing.** Test categories:
- **Parsing** — Tokeniser state machine, directive parsing
- **Generation** — CodeDOM code generation with newline variants
- **Preprocessing** — Preprocessed templates, include + class feature combinations
- **Engine edge cases** — Bare `return;`, class features, imports, host-specific, nested control flow
- **Roslyn compiler** — Framework assembly filtering, duplicates, debug mode, language mapping
- **DI host** — Service resolution, session, parameters, builder API, `ProcessTemplateContent`
- **SignalR integration** — Hub→TypeScript template compilation, placeholder output, type discovery

Full test documentation in [docs/TESTING.md](docs/TESTING.md).

---

## CI/CD

GitHub Actions workflow — [ci.yml](.github/workflows/ci.yml):

| Platform | .NET SDKs | Steps |
|---|---|---|
| `ubuntu-latest` | 8.0.x, 9.0.x, 10.0.x | Restore → Build → Test + Coverage → Pack → Upload |
| `windows-latest` | 8.0.x, 9.0.x, 10.0.x | Restore → Build → Test + Coverage |
| `macos-latest` | 8.0.x, 9.0.x, 10.0.x | Restore → Build → Test + Coverage |

Coverage reports (Cobertura XML) and NuGet packages are uploaded from Ubuntu.

---

## Project Structure

```
Mono.TextTemplating/
├── src/
│   ├── Mono.TextTemplating/            # Core engine library
│   │   ├── Compilation/                # ITemplateCompiler, RoslynTemplateCompiler, CodeDomTemplateCompiler
│   │   ├── Hosting/                    # DiTemplateHost, TemplatingHostBuilder
│   │   └── Microsoft.VisualStudio.TextTemplating/  # VS-compatible public API
│   ├── Mono.TextTemplating.Build/      # MSBuild task + .props/.targets
│   └── Mono.TextTemplating.Cli/        # dotnet-t4 CLI tool
├── tests/
│   ├── Mono.TextTemplating.Tests/      # 65 unit tests (xUnit)
│   └── Mono.TextTemplating.IntegrationTest/  # End-to-end integration test
├── samples/
│   └── SignalR.TsGeneration/           # SignalR hub → TypeScript code generation
├── docs/
│   ├── ARCHITECTURE.md                 # Pipeline, key types, concerns
│   ├── TESTING.md                      # Test strategy, edge cases, patterns
│   ├── KNOWN_ISSUES.md                 # T4 pitfalls, platform limitations
│   └── SDLC_IMPROVEMENT_PLAN.md        # Modernization plan & history
└── .github/workflows/ci.yml           # CI pipeline
```

---

## Known Issues

See [docs/KNOWN_ISSUES.md](docs/KNOWN_ISSUES.md) for details. Key items:

| Issue | Mitigation |
|---|---|
| Bare `return;` in template body | Use `if/else` nesting or return `this.GenerationEnvironment.ToString()` |
| CodeDOM not supported on .NET 10+ | Roslyn is the default compiler; CodeDOM guarded with `#if NETFRAMEWORK` |
| `CallContext` removed in .NET Core | Parameter resolution falls back to session/DI lookup |
| Framework assembly names (e.g. `System.Linq`) | Filtered by Roslyn compiler; runtime TPA provides them |
| `RecyclableAppDomain` on .NET Core+ | Guarded with `#if NETFRAMEWORK`; runs in-process |

---

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make changes — follow the [.editorconfig](.editorconfig) (4-space tabs, expression-bodied members, pattern matching)
4. Add tests covering your changes
5. Run `dotnet build -c Release` and `dotnet test tests/Mono.TextTemplating.Tests -c Release`
6. Submit a PR

Code quality settings (from `Directory.Build.props`):
- Analysis: `latest` level, `Recommended` mode
- Warnings as errors: off (warnings are logged, not fatal)
- Code style enforcement enabled

---

## License

MIT — Originally from the [MonoDevelop](https://github.com/mono/monodevelop) project. Revived and modernized for .NET 10.
