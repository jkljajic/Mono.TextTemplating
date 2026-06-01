# Mono.TextTemplating — Modern Cross-Platform T4 Engine

[![Build & Test](https://github.com/jkljajic/Mono.TextTemplating/actions/workflows/ci.yml/badge.svg)](https://github.com/jkljajic/Mono.TextTemplating/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Mono.TextTemplating.svg)](https://www.nuget.org/packages/Mono.TextTemplating/)

**A modern, cross-platform implementation of Visual Studio's T4 text templating engine.** Process `.tt` template files on .NET 10 with Roslyn compilation, MSBuild integration, and DI support.

## Quick Start

```bash
# CLI tool
dotnet tool install -g Mono.TextTemplating.Cli
dotnet t4 transform -i template.tt -o output.cs
dotnet t4 preprocess -i template.tt -c MyClass -ns MyApp.Generated -o output.cs

# NuGet library
dotnet add package Mono.TextTemplating
```

```csharp
// API usage
var generator = new TemplateGenerator();
generator.Refs.Add(typeof(MyType).Assembly.Location);
generator.Imports.Add("System.Linq");
generator.ProcessTemplate("template.tt", "output.txt");
```

## Features

| Feature | Status |
|---|---|
| `.tt` template processing | ✅ Full T4 syntax |
| Preprocessed (runtime) templates | ✅ |
| Roslyn compilation backend | ✅ Default |
| Host-specific templates | ✅ |
| Custom directive processors | ✅ |
| Include directives | ✅ |
| Cross-platform (Windows/macOS/Linux) | ✅ |
| Nullable reference types enabled | ✅ |
| MSBuild build-time integration | ✅ `.props` + `.targets` |
| `dotnet-t4` global tool | ✅ |
| DI-aware host (`TemplatingHostBuilder`) | ✅ |
| SignalR → TypeScript code generation | ✅ [Sample](samples/SignalR.TsGeneration/) |

## Build & Test

```bash
dotnet build -c Release
dotnet test tests/Mono.TextTemplating.Tests -c Release
```

**65 tests, all passing.**

## Packages

| Package | Purpose |
|---|---|
| `Mono.TextTemplating` | Core T4 engine library |
| `Mono.TextTemplating.Build` | MSBuild task + `.props`/`.targets` for `dotnet build` integration |
| `Mono.TextTemplating.Cli` | `dotnet-t4` global CLI tool |

## MSBuild Integration

```xml
<PackageReference Include="Mono.TextTemplating.Build" Version="3.0.0-*" PrivateAssets="all" />
```

Place `.tt` files in your project — they're auto-transformed post-build. Pass parameters via item metadata:

```xml
<T4PostBuildTemplate Include="Templates/**/*.tt">
  <T4Param_TargetAssembly>$(TargetPath)</T4Param_TargetAssembly>
</T4PostBuildTemplate>
```

## Architecture

```
.tt file → Tokeniser → ParsedTemplate → CodeDOM AST → Roslyn Compile → Execute → output
```

See [ARCHITECTURE.md](docs/ARCHITECTURE.md) for details.

## Documentation

- [ARCHITECTURE.md](docs/ARCHITECTURE.md) — Pipeline, key types, cross-cutting concerns
- [TESTING.md](docs/TESTING.md) — Test strategy, coverage map, fixture patterns
- [KNOWN_ISSUES.md](docs/KNOWN_ISSUES.md) — T4 pitfalls: bare `return;`, CodeDOM limits, CallContext
- [SDLC_IMPROVEMENT_PLAN.md](docs/SDLC_IMPROVEMENT_PLAN.md) — Original analysis & modernization plan

## SignalR Sample

See [`samples/SignalR.TsGeneration/`](samples/SignalR.TsGeneration/) — complete sample demonstrating:
- SignalR hub definitions (ChatHub, NotificationHub)
- T4 template that discovers hubs and generates TypeScript clients
- DI-aware host builder
- MSBuild post-build `.tt` → `.ts` pipeline

```bash
dotnet build samples/SignalR.TsGeneration -c Release
# → bin/Release/net10.0/HubClientGenerator.ts (generated TypeScript clients)
```

## License

MIT — Originally from the [MonoDevelop](https://github.com/mono/monodevelop) project.
