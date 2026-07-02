# T4 Studio SDLC Modernization & Revival Plan

Current package identity is `T4Studio.Engine`, `T4Studio.Cli`, and `T4Studio.Build`. This document is a historical modernization plan and still refers to `T4Studio` where it discusses upstream architecture, assemblies, namespaces, or compatibility.

**Status:** Deep Analysis Complete | **Date:** 2026-05-31 | **Author:** Jovo (Architect)

---

## Executive Summary

T4Studio is a clean, well-architected T4 template engine last touched in July 2012. The core pipeline (Tokeniser → ParsedTemplate → CodeDOM → Compile → Execute) is **functionally correct and builds on .NET 10**. However, it is trapped in .NET Framework era patterns: non-SDK projects, NUnit 2.x, CodeDOM dependency, AppDomain usage, and zero DevOps infrastructure.

**Verdict:** The engine's core logic is worth saving. The revival needs surgical modernization in layers — infrastructure first, then API surface, then compiler backend. The CodeDOM → Roslyn migration is the hardest problem.

---

## 1. Current State Assessment

### 1.1 Architecture (4-layer pipeline)

```
┌──────────────┐    ┌───────────────┐    ┌─────────────────┐    ┌──────────────┐
│  Tokeniser   │ →  │ ParsedTemplate│ →  │  TemplatingEngine│ → │  Execute     │
│  (lexer)     │    │  (AST builder) │    │  (CodeDOM gen +  │    │  CompiledTpl │
│              │    │                │    │   compile)       │    │              │
└──────────────┘    └───────────────┘    └─────────────────┘    └──────────────┘
```

- **Tokeniser** (295 LOC): State-machine lexer. Clean. Works for all T4 syntax.
- **ParsedTemplate** (339 LOC): Builds segment AST with include resolution. Clean.
- **TemplatingEngine** (963 LOC): The beast. Gets settings, generates CodeDOM, compiles, returns `CompiledTemplate`. Tightly coupled to `System.CodeDom`.
- **CompiledTemplate** (113 LOC): Wraps compiled assembly, runs `TransformText()`.

### 1.2 API Surface (Microsoft.VisualStudio.TextTemplating namespace)

```
ITextTemplatingEngine          — ProcessTemplate / PreprocessTemplate
ITextTemplatingEngineHost      — 12 members (host contract)
TextTransformation             — base class for generated code
ITextTemplatingSession         — session state
DirectiveProcessor             — extensibility point
```

### 1.3 Build Results (as of .NET 10 SDK)

| Project | Build | Issues |
|---|---|---|
| `T4Studio.dll` | ✅ PASS | None |
| `t4studio` | ✅ PASS | None |
| `T4Studio.Tests.dll` | ❌ FAIL | NUnit 2.x not found |

### 1.4 Critical Technical Debt

| Issue | Severity | Impact |
|---|---|---|
| `System.CodeDom` dependency | **Critical** | Dead-end API; no Roslyn integration; deprecated |
| `MarshalByRefObject` / AppDomain | **Critical** | Not supported in .NET Core+; breaks `RecyclableAppDomain` |
| Hardcoded `"C#v3.5"` language string | **High** | Only supports C# 3.5-era CodeDOM; no modern C# |
| `new System.Random().Next()` for class naming | **Medium** | Collision-prone; non-deterministic |
| NUnit 2.x binary references | **Medium** | Tests don't run; blocks verification |
| Non-SDK `.csproj` | **Medium** | Blocks multi-targeting, NuGet packaging |
| `Assembly.LoadFrom` with no shadow copy | **High** | Assembly locking; can't overwrite outputs |
| `CompiledTemplate` leaks `AssemblyResolve` handler | **High** | Event leak if Dispose not called |
| `EncodingHelper.GetEncoding` — hardcoded `Encoding` fallback | **Low** | Might not match VS behavior |
| No async support | **Low** | All I/O is synchronous |

---

## 2. The T4 Ecosystem Context

### 2.1 Why T4 matters (still)

T4 is Microsoft's built-in code generation engine, deeply embedded in:
- **Visual Studio**: Entity Framework, ASP.NET scaffolding, service references
- **.NET SDK**: `Microsoft.VisualStudio.TextTemplating` ships with VS/MSBuild
- **Build-time codegen**: `.tt` files with `TextTemplatingFileGenerator` custom tool
- **Runtime templates**: Preprocessed templates for dynamic codegen

T4 has **no viable open-source alternative** for the `.tt` format. Razor replaced it for HTML, but T4 is still used for general-purpose code generation in thousands of enterprise projects.

### 2.2 What exists today

| Tool | Status |
|---|---|
| Microsoft's T4 engine (`Microsoft.VisualStudio.TextTemplating.dll`) | Closed-source, ships with VS Build Tools |
| `dotnet-t4` (unofficial) | Several exist but are incomplete |
| T4Studio (this project) | Most complete open-source implementation |
| `T4.Build` NuGet package | Wraps MSBuild integration |

### 2.3 The opportunity

The .NET ecosystem lacks a **first-class, cross-platform, NuGet-delivered T4 engine** that:
- Works on `dotnet build` (Linux, macOS, Windows)
- Integrates with MSBuild without Visual Studio
- Has a modern Roslyn-backed compiler
- Supports .NET 6/7/8/9/10+
- Has a NuGet-based distribution model

**This project is the best starting point to fill that gap.**

---

## 3. SDLC Modernization — Phase Plan

### Phase 0: Foundation (Weeks 1–2)

> **Goal:** Make the project build, test, and package with zero production code changes

#### 0.1 — Convert to SDK-style projects
```xml
<!-- T4Studio.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
```

**Why:** Enables multi-targeting, NuGet packaging, nullable annotations, and modern build infrastructure without touching source.

#### 0.2 — Replace NUnit 2.x with xUnit + NuGet
- Remove `lib/nunit.*.dll` binaries from repo
- Add `<PackageReference Include="xunit" />` + `xunit.runner.visualstudio`
- Convert `[TestFixture]` / `[Test]` → `public class` / `[Fact]`
- Convert `Assert.AreEqual` → `Assert.Equal`

**Why:** NUnit 2.x is a binary in the repo. xUnit is the modern standard, NuGet-delivered.

#### 0.3 — Add CI/CD (GitHub Actions)
```yaml
name: Build & Test
on: [push, pull_request]
jobs:
  build:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            9.0.x
            10.0.x
      - run: dotnet build -c Release
      - run: dotnet test -c Release
```

**Why:** Zero current automation. Every change is unverified.

#### 0.4 — Add NuGet packaging metadata
```xml
<PropertyGroup>
  <PackageId>T4Studio.Engine</PackageId>
  <Version>0.1.0</Version>
  <Authors>Jovo Kljajic</Authors>
  <Description>T4 Studio engine library for Visual Studio-compatible T4 templates</Description>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageReadmeFile>README.md</PackageReadmeFile>
</PropertyGroup>
```

#### 0.5 — Add `.gitignore` / `.editorconfig` / `Directory.Build.props`
- `.editorconfig` for consistent formatting
- `Directory.Build.props` for shared properties
- Update `.gitignore` for modern .NET artifacts

---

### Phase 1: Stabilize API (Weeks 3–4)

> **Goal:** Fix bugs, add nullable annotations, improve error handling, document public API

#### 1.1 — Enable nullable reference types
- Add `#nullable enable` throughout (or `<Nullable>enable</Nullable>` at project level)
- Fix all nullability warnings
- Add null-guard patterns to public API

**Impact:** Catches bugs, improves IDE experience for consumers.

#### 1.2 — Fix known bugs
| Bug | Fix |
|---|---|
| `CompiledTemplate` `AssemblyResolve` leak | Unsubscribe in `Dispose` (already done, verify) |
| `EncodingHelper.GetEncoding` fallback | Use `EncodingHelper.GetEncoding` from Microsoft's implementation |
| Random class name collision | Use `Guid.NewGuid().ToString("N")` or deterministic hash of content |
| `StringBuilder` parameter null check in `TextTransformation.Write` | Add proper `ArgumentNullException` |
| `TemplateGenerator.ResolveAssemblyReference` — `Assembly.LoadFrom` locking | Use `Assembly.Load(File.ReadAllBytes(...))` or `MetadataLoadContext` |

#### 1.3 — Add XML documentation to all public API
- Every public type, method, property gets `<summary>` XML doc
- Document the T4 directive contract
- Document the host interface contract

#### 1.4 — Add `TemplateGenerator` async overloads
```csharp
public async Task<bool> ProcessTemplateAsync(string inputFile, string outputFile, CancellationToken ct = default)
```
Use `File.ReadAllTextAsync` / `File.WriteAllTextAsync` internally.

---

### Phase 2: Roslyn Migration (Weeks 5–8)

> **Goal:** Replace `System.CodeDom` with Roslyn (`Microsoft.CodeAnalysis`) for compilation

This is the **highest-risk, highest-reward** phase.

#### 2.1 — The problem with CodeDOM

`System.CodeDom` is:
- .NET Framework-era technology
- Not actively maintained for new C# features (tuples, records, pattern matching, etc.)
- Cannot target modern TFM
- The `CSharpCodeProvider` emits C# 5-era code at best
- No source-level debugging integration

#### 2.2 — Migration strategy: Dual-backend

**Do NOT rip out CodeDOM immediately.** Instead:

```
┌─────────────────────────────────────────────────┐
│              ITemplateCompiler                   │
│  (new interface)                                │
├───────────────────────┬─────────────────────────┤
│ CodeDomCompiler       │ RoslynCompiler          │
│ (existing, stable)    │ (new, modern)           │
└───────────────────────┴─────────────────────────┘
```

1. Extract `ITemplateCompiler` interface from `TemplatingEngine`
2. Implement `CodeDomTemplateCompiler` (migrate existing code)
3. Implement `RoslynTemplateCompiler` (new)
4. Add feature flag in `TemplateSettings` to select backend
5. Default to CodeDOM initially; Roslyn becomes default after validation

#### 2.3 — Roslyn compiler design

```csharp
public class RoslynTemplateCompiler : ITemplateCompiler
{
    public CompilerResults Compile(
        CodeCompileUnit codeDom,
        TemplateSettings settings,
        IEnumerable<string> references)
    {
        // 1. Convert CodeDOM → C# source text
        var sourceText = GenerateCSharp(codeDom, settings);

        // 2. Create Roslyn compilation
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var compilation = CSharpCompilation.Create(
            settings.Name,
            new[] { syntaxTree },
            references.Select(r => MetadataReference.CreateFromFile(r)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // 3. Emit to MemoryStream
        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        // 4. Map Roslyn diagnostics → CompilerErrorCollection
        return MapToCompilerResults(result, ms, settings);
    }
}
```

#### 2.4 — CodeDOM → Roslyn approach options

| Option | Effort | Risk |
|---|---|---|
| **A: Convert CodeDOM → C# text, then Roslyn compile** | Medium | Low — keeps CodeDOM AST generation, swaps backend |
| **B: Rewrite codegen to emit Roslyn SyntaxNodes directly** | High | High — changes entire generation pipeline |
| **C: Use Roslyn Source Generator approach** | Very High | Highest — architectural rewrite |

**Recommendation: Option A** — Keep CodeDOM for AST generation, swap compile backend. This is the minimum change that unlocks all Roslyn benefits (modern C#, debugging, cross-platform) without rewriting the generation logic.

#### 2.5 — Remove AppDomain dependency

The `RecyclableAppDomain` pattern (isolated template compilation) must be reimagined:

| Current (.NET Framework) | Modern (.NET Core+) |
|---|---|
| `AppDomain.CreateDomain` | `AssemblyLoadContext` (unloadable) |
| `MarshalByRefObject` remoting | Direct in-process loading |
| `CreateInstanceFromAndUnwrap` | `Activator.CreateInstance` |

```csharp
public class TemplateAssemblyLoadContext : AssemblyLoadContext
{
    public TemplateAssemblyLoadContext() : base(isCollectible: true) { }

    protected override Assembly? Load(AssemblyName name)
    {
        // resolve from known references
        return null; // fall through to default
    }

    public Assembly LoadFromStream(Stream assembly, Stream? pdb)
    {
        return LoadFromStream(assembly, pdb);
    }
}
```

This gives us **collectible AssemblyLoadContext** — the modern equivalent of RecyclableAppDomain, but actually supported and maintained.

---

### Phase 3: MSBuild Integration (Weeks 9–10)

> **Goal:** Make `.tt` files work as MSBuild items in `dotnet build`

#### 3.1 — MSBuild task

```xml
<!-- T4Studio.Build NuGet package -->
<Project>
  <UsingTask TaskName="TransformTemplates"
             AssemblyFile="$(MSBuildThisFileDirectory)../tasks/T4Studio.Build.dll" />

  <ItemGroup>
    <T4Template Include="**/*.tt" />
    <None Remove="@(T4Template)" />
  </ItemGroup>

  <Target Name="TransformT4Templates" BeforeTargets="BeforeBuild">
    <TransformTemplates
      Templates="@(T4Template)"
      OutputDir="$(IntermediateOutputPath)Generated"
      References="@(ReferencePath)"
      ImportNamespaces="System;System.Linq">
      <Output TaskParameter="GeneratedFiles" ItemName="Compile" />
    </TransformTemplates>
  </Target>
</Project>
```

#### 3.2 — NuGet packages to ship

| Package | Contents |
|---|---|
| `T4Studio.Engine` | Core engine library |
| `T4Studio.Build` | MSBuild targets + task DLL |
| `T4Studio.Cli` | `t4studio` global tool |
| `T4Studio.Roslyn` | Future split if Roslyn is separated from the engine |
| `T4Studio.CodeAnalysis` | Roslyn Analyzer + Source Generator |

#### 3.3 — `t4studio` global tool

```bash
dotnet tool install -g T4Studio.Cli
t4studio -o output.cs template.tt
dotnet t4 preprocess -i template.tt -c MyTemplate -ns MyApp.Generated
```

---

### Phase 4: Ecosystem Expansion (Weeks 11–12)

#### 4.1 — Roslyn Analyzer for `.tt` files
- Validate T4 syntax at design time
- Provide code fixes for common issues
- Enable `#line` directive-based navigation

#### 4.2 — Roslyn Source Generator integration
- Option to run `.tt` files as source generators (no build-time tooling needed)
- `[T4Template("template.tt")]` attribute-based generation

#### 4.3 — VS Code / JetBrains Rider extensions
- Syntax highlighting for `.tt` files
- Code completion inside `<# ... #>` blocks
- Preview generated output

---

## 4. Technical Architecture — Target State

```
┌─────────────────────────────────────────────────────────────────┐
│                        NuGet Packages                            │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐      │
│  │ Core Engine   │  │ MSBuild Task  │  │ CLI Global Tool  │      │
│  │ net8.0-net10  │  │ net8.0-net10  │  │ net8.0-net10     │      │
│  └──────┬───────┘  └──────┬───────┘  └────────┬─────────┘      │
│         │                 │                   │                  │
│         └─────────┬───────┴───────────────────┘                  │
│                   │                                              │
│  ┌────────────────┴────────────────────────────┐                │
│  │          T4Studio Engine          │                │
│  │                                              │                │
│  │  ┌──────────┐  ┌───────────┐  ┌───────────┐ │                │
│  │  │ Tokeniser│→│  Parser   │→│ Compiler   │ │                │
│  │  └──────────┘  └───────────┘  └─────┬─────┘ │                │
│  │                                     │       │                │
│  │              ┌──────────────────────┼───────┤                │
│  │              │ ITemplateCompiler    │       │                │
│  │              ├──────────────────────┤       │                │
│  │              │ CodeDomCompiler      │       │                │
│  │              │ RoslynCompiler    ◄──┘       │                │
│  │              └──────────────────────┘       │                │
│  └────────────────────────────────────────────┘                │
│                                                                  │
│  ┌──────────────────────────────────────────┐                   │
│  │  AssemblyLoadContext (isolated compile)   │                   │
│  │  + TemplateAssemblyLoadContext            │                   │
│  └──────────────────────────────────────────┘                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 5. Risk Assessment

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| CodeDOM → Roslyn breaks existing templates | Medium | High | Dual-backend; extensive test suite; opt-in flag |
| AssemblyLoadContext behaves differently than AppDomain | Medium | Medium | Thorough unit tests; fallback to in-process compile |
| MSBuild integration conflicts with VS built-in T4 | Low | High | Namespace prefix; explicit opt-in via PackageReference |
| Template output differs from Microsoft T4 | Medium | Medium | Compatibility test suite against VS-generated output |
| Community doesn't adopt | Medium | N/A | Ship as NuGet; make drop-in simple; provide migration guide |
| Breaking changes in .NET 10+ AssemblyLoadContext | Low | Medium | Multi-target testing in CI |

---

## 6. Metrics & Success Criteria

| Metric | Current | Target |
|---|---|---|
| Build time | 2 seconds | <5 seconds (CI) |
| Test coverage | ~40% (estimate) | >80% line coverage |
| Cross-platform support | Windows only (path assumptions) | Windows + Linux + macOS |
| NuGet downloads | 0 | >10K/month within 6 months |
| CI pipeline | None | PR + merge on all 3 OS |
| .tt compatibility | Unknown | >95% with Microsoft T4 |
| Compilation performance | CodeDOM-bound | Roslyn: <50ms for typical template |

---

## 7. Immediate Next Actions (Priority-Ordered)

### 🔴 Week 1 — Do now
1. **Convert .csproj to SDK-style** (enables everything else)
2. **Fix tests with xUnit** (unblocks verification)
3. **Add GitHub Actions CI** (catches regressions)

### 🟡 Week 2 — Stability
4. **Full nullability audit** + `#nullable enable`
5. **XML documentation on public API**
6. **Fix `AssemblyResolve` leak** (if not already fixed)
7. **Fix `New Random().Next()`** class naming → deterministic

### 🟢 Weeks 3–4 — Modernization begins
8. **Extract `ITemplateCompiler` interface**
9. **Implement `RoslynTemplateCompiler`** (Option A: CodeDOM → C# text → Roslyn)
10. **Replace `RecyclableAppDomain` with `AssemblyLoadContext`**
11. **Add `t4studio` CLI tool package**

### 🔵 Weeks 5+ — Ecosystem
12. MSBuild `.targets` for build-time `.tt` processing
13. NuGet packaging pipeline
14. Roslyn Analyzer for `.tt` files
15. VS Code extension

---

## 8. Repository Structure — Target State

```
T4Studio/
├── .github/
│   └── workflows/
│       ├── ci.yml
│       └── release.yml
├── .editorconfig
├── .gitignore
├── Directory.Build.props
├── T4Studio.sln
├── README.md
├── src/
│   ├── T4Studio/
│   │   ├── T4Studio.csproj    (SDK-style, multi-target)
│   │   ├── Tokeniser.cs
│   │   ├── ParsedTemplate.cs
│   │   ├── TemplatingEngine.cs
│   │   ├── TemplateGenerator.cs
│   │   ├── Compiler/
│   │   │   ├── ITemplateCompiler.cs
│   │   │   ├── CodeDomTemplateCompiler.cs
│   │   │   └── RoslynTemplateCompiler.cs
│   │   ├── Hosting/
│   │   │   └── TemplateAssemblyLoadContext.cs
│   │   └── Microsoft.VisualStudio.TextTemplating/
│   │       ├── Interfaces.cs
│   │       ├── TextTransformation.cs
│   │       ├── Engine.cs
│   │       └── ...
│   ├── T4Studio.Cli/
│   │   ├── T4Studio.Cli.csproj
│   │   └── Program.cs (System.CommandLine)
│   └── T4Studio.Build/
│       ├── T4Studio.Build.csproj
│       ├── TransformTemplates.cs (MSBuild task)
│       └── build/
│           ├── T4Studio.Build.props
│           └── T4Studio.Build.targets
├── tests/
│   └── T4Studio.Tests/
│       ├── T4Studio.Tests.csproj
│       ├── TokeniserTests.cs
│       ├── ParsingTests.cs
│       ├── GenerationTests.cs
│       ├── PreprocessTemplateTests.cs
│       ├── RoslynCompilerTests.cs
│       └── CompatibilityTests.cs
└── docs/
    ├── SDLC_IMPROVEMENT_PLAN.md   (this document)
    ├── ARCHITECTURE.md
    └── MIGRATION_GUIDE.md
```

---

## 9. Four Mandatory Architecture Questions

### How does it scale?
- Template compilation is CPU-bound, not I/O-bound
- `AssemblyLoadContext` is collectible — no memory leak over long runs
- Roslyn compilation is parallelizable per-template (each template is independent)
- MSBuild task supports incremental builds (up-to-date check via timestamps)
- For high-throughput scenarios (1000+ templates), a `TransformMany` batch API minimizes JIT overhead

### How does it fail?
- Parse errors → `CompilerErrorCollection` logged to host
- Compile errors → Roslyn diagnostics mapped to `CompilerError`
- Runtime errors in `TransformText()` → caught, logged as `tt.Error()`
- Missing references → clear error: "Could not resolve assembly reference 'X'"
- Host resolution failures → exception with actionable message
- `AssemblyLoadContext` unload failures → fallback to in-process compile with warning

### How is it monitored?
- CI pipeline verifies every commit on all 3 OS
- Test suite verifies parse → generate → compile → execute end-to-end
- Compatibility tests against Microsoft T4 baseline
- MSBuild logs template count, timing, errors as structured MSBuild messages
- NuGet package with `SourceLink` for debugging into engine code

### How is it extended?
- `ITemplateCompiler` interface — plug in new compiler backends
- `DirectiveProcessor` — existing extensibility point for custom directives
- `ITextTemplatingEngineHost` — host can customize assembly resolution, include paths, session
- MSBuild task accepts arbitrary `[T4Template]` item metadata
- Source generator mode as an alternative entry point
- Roslyn Analyzer extensibility for `.tt`-specific diagnostics

---

## 10. What NOT to Change

- **The `Microsoft.VisualStudio.TextTemplating` namespace and interface contracts** — these are the compatibility surface with Microsoft's T4 API. Changing them breaks the value proposition.
- **The T4 template syntax** — must remain 100% compatible with Microsoft T4 `.tt` files.
- **The `TextTransformation` base class** — generated code inherits from this; cannot break.
- **The `TemplateGenerator` as a self-contained host** — this is the simplest "just give me a file" API, and it's valuable.
- **The MonoDevelop addin** — leave it in place but don't invest in it. MonoDevelop is gone; the addin is archival.

---

## Appendix A: Compatibility Matrix

| Feature | Microsoft T4 | T4Studio (current) | Target |
|---|---|---|---|
| `<#= expr #>` expressions | ✅ | ✅ | ✅ |
| `<# code #>` statement blocks | ✅ | ✅ | ✅ |
| `<#+ helpers #>` class features | ✅ | ✅ | ✅ |
| `<#@ template #>` directive | ✅ | ✅ | ✅ |
| `<#@ assembly #>` directive | ✅ | ✅ | ✅ |
| `<#@ import #>` directive | ✅ | ✅ | ✅ |
| `<#@ output #>` directive | ✅ | ✅ | ✅ |
| `<#@ include #>` directive | ✅ | ✅ | ✅ |
| `<#@ parameter #>` directive | ✅ | ✅ | ✅ |
| Custom directive processors | ✅ | ✅ | ✅ |
| `hostspecific="true"` | ✅ | ✅ | ✅ |
| `Culture` support | ✅ | ✅ | ✅ |
| Preprocessed templates | ✅ | ✅ | ✅ |
| MSBuild integration | ✅ | ❌ | ✅ (Phase 3) |
| `dotnet build` on Linux | ❌ (Windows only) | ❌ | ✅ (Phase 3) |
| Roslyn compilation | ❌ (CodeDOM) | ❌ | ✅ (Phase 2) |
| Source generators | ❌ | ❌ | ✅ (Phase 4) |
| `t4studio` CLI tool | Not applicable | Partially (`t4studio`) | Planned in Phase 3 |
| In-editor `.tt` support (VS Code/Rider) | ✅ (VS only) | ❌ | ✅ (Phase 4) |

---

## Appendix B: Build Verification

```bash
# Current state (2026-05-31)
dotnet build T4Studio.sln -c Release
# Result: Core library + CLI build PASS. Tests FAIL (NUnit 2.x missing).
# .NET SDK: 10.0.300
# Output: E:\build\AddIns\MonoDevelop.TextTemplating\
```

---

*This document is a living artifact. Update it as phases complete.*


