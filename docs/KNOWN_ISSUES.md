# Known Issues & Pitfalls

## T4 Template Pitfalls

### 1. Bare `return;` in Template Body (CRITICAL)

**Symptom:** `An object of a type convertible to 'string' is required`

**Root cause:** T4 compiles `<# ... #>` blocks into the `TransformText()` method which returns `string`. A bare `return;` is illegal.

```t4
<!-- BROKEN -->
<# if (error) return; #>

<!-- FIXED — use if/else -->
<# if (error) { Write("error"); } else { #>content<# } #>

<!-- FIXED — return the GenerationEnvironment -->
<# if (error) return this.GenerationEnvironment.ToString(); #>
```

### 2. CodeDOM Compilation on .NET 10+

**Symptom:** `PlatformNotSupportedException: Operation is not supported on this platform` from `Microsoft.CSharp.CSharpCodeGenerator.FromFileBatch`

**Root cause:** `System.CodeDom` NuGet package doesn't support in-memory compilation on modern .NET.

**Fix:** The default compiler is now **Roslyn** (`Microsoft.CodeAnalysis`). Use `TemplateSettings.CompilerType = TemplateCompilerType.Roslyn` (default).

### 3. `ParameterDirectiveProcessor` Mono Hack

**Symptom:** `InvalidCastException` from `(CodeGenerator) provider.CreateGenerator()`

**Root cause:** Legacy Mono 2.x compatibility hack that tries to cast `ICodeGenerator` to `CodeGenerator`. Fixed in modern .NET by guarding with `useMonoHack` flag.

### 4. `CallContext` Removed in .NET Core

**Symptom:** `The type or namespace name 'Messaging' does not exist in the namespace 'System.Runtime.Remoting'`

**Root cause:** The `ParameterDirectiveProcessor` generates code that references `System.Runtime.Remoting.Messaging.CallContext`, which was removed in .NET Core.

**Fix:** Guarded with `#if NETFRAMEWORK`. On .NET Core+, parameter resolution falls back to session and host lookup only.

### 5. Template Compilation — Framework Assembly Names

**Symptom:** Roslyn failure when template uses `<#@ assembly name="System.Linq" #>`

**Root cause:** The engine resolves assembly references to paths. Framework assembly names like "System.Linq" aren't valid file paths.

**Fix:** RoslynTemplateCompiler filters out references that aren't valid file paths. Framework assemblies are provided by the runtime's TPA (Trusted Platform Assemblies).

### 6. Whitespace/Newline Inconsistencies

**Symptom:** Tests fail comparing expected vs actual output with `\r\n` vs `\n` mismatches.

**Root cause:** Different CodeDom versions emit different line ending patterns. The template output uses the system newline.

**Fix:** Use newline-agnostic comparison in tests. The `AssertOutput()` helper normalizes `\r\n` and `\r` to `\n` before comparing.

## Build/Infrastructure

### RecyclableAppDomain Not Supported on .NET Core+

`AppDomain.CreateDomain()` and `AppDomain.Unload()` throw `PlatformNotSupportedException` on .NET Core+. The `TemplatingAppDomainRecycler` has been guarded with `#if NETFRAMEWORK`. Template compilation runs in-process on .NET Core+.

### MSBuild Task Assembly Version Mismatch

When using the `Mono.TextTemplating.Build` package, ensure the task DLL and its dependencies in `tasks/net10.0/` are from the same build. Version mismatches between `Mono.TextTemplating.dll` copies causes `InvalidCastException` when loading generated template types.
