# T4 Studio

[![CI](https://github.com/jkljajic/T4Studio/actions/workflows/ci.yml/badge.svg)](https://github.com/jkljajic/T4Studio/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/T4Studio.Engine.svg)](https://www.nuget.org/packages/T4Studio.Engine/)

T4 Studio is a modernized distribution of the upstream T4 text templating engine. It processes Visual Studio-compatible `.tt` templates from the command line, from MSBuild, or through the library API.

This fork is based on Mono.TextTemplating / mono/t4. Existing `.tt` templates remain supported. Project, package, folder, assembly, and runtime namespaces now use the T4 Studio identity, while the Visual Studio-compatible `Microsoft.VisualStudio.TextTemplating` API surface remains available where implemented.

## Why It Exists

T4 is still useful for practical code generation, but many projects need a current .NET build, Roslyn compilation, NuGet packaging, and editor support outside the original Visual Studio-only workflow. T4 Studio focuses on those areas while preserving the behavior expected by existing templates.

Current focus:

- Modern Roslyn-backed template compilation.
- NuGet packages for engine, CLI, and MSBuild integration.
- VS Code and Visual Studio editing experience.
- Clear diagnostics, repeatable builds, and release automation.

## Packages

| Package | Purpose |
|---|---|
| `T4Studio.Engine` | Core T4 engine library. The assembly is `T4Studio.Engine`; the runtime namespace is `T4Studio`. |
| `T4Studio.Cli` | .NET global tool that exposes the `t4studio` command. |
| `T4Studio.Build` | MSBuild integration for transforming `.tt` files during `dotnet build`. |

There is not a separate `T4Studio.Roslyn` package yet; Roslyn support is currently part of `T4Studio.Engine`. `T4Studio.VSCode` and `T4Studio.Debugging` are not NuGet packages in this repository at this time. The repo does include editor extension sources.

## Install

Install the CLI:

```bash
dotnet tool install -g T4Studio.Cli
```

Install the engine library:

```bash
dotnet add package T4Studio.Engine
```

Install MSBuild integration:

```xml
<PackageReference Include="T4Studio.Build" Version="0.1.0" PrivateAssets="all" />
```

## CLI Usage

```bash
t4studio template.tt
t4studio -o output.cs template.tt
t4studio -c MyNamespace.MyTemplate template.tt
t4studio -r MyAssembly.dll -u System.Linq -I templates -P lib -o output.cs template.tt
```

Useful options:

```text
-o, --out=VALUE   Output file
-r=VALUE          Reference assembly, repeatable
-u=VALUE          Import namespace, repeatable
-I=VALUE          Include search path, repeatable
-P=VALUE          Reference search path, repeatable
-dp=VALUE         Directive processor as name!class!assembly
-a=VALUE          Parameter as name!value
-c=VALUE          Preprocess into namespace.class
```

## Library Usage

The library API uses the `T4Studio` namespace:

```csharp
using T4Studio;

var generator = new TemplateGenerator();
generator.Refs.Add(typeof(MyType).Assembly.Location);
generator.Imports.Add("System.Linq");
generator.AddParameter(null, null, "UserName", "Alice");
generator.ProcessTemplate("template.tt", "output.txt");
```

For DI-aware hosting:

```csharp
using T4Studio.Hosting;

var host = new TemplatingHostBuilder(serviceProvider)
    .WithAssemblyReference(typeof(MyService).Assembly.Location)
    .WithImport("System.Linq")
    .WithParameter(null, null, "ConnectionString", connectionString)
    .Build();
```

## MSBuild Usage

`T4Studio.Build` auto-discovers `.tt` files and transforms them after build.

```xml
<PropertyGroup>
  <EnableDefaultT4Items>true</EnableDefaultT4Items>
</PropertyGroup>

<ItemGroup>
  <T4PostBuildTemplate Include="Templates\*.tt">
    <LastGenOutput>$(OutDir)%(Filename).generated.cs</LastGenOutput>
    <T4Param_TargetAssembly>$(TargetPath)</T4Param_TargetAssembly>
  </T4PostBuildTemplate>
</ItemGroup>
```

Any item metadata named `T4Param_<Key>` is passed to the template as a parameter.

## Compatibility

T4 Studio keeps compatibility with existing `.tt` templates as the primary constraint. That means:

- Existing T4 syntax remains supported: `<# #>`, `<#= #>`, `<#+ #>`, and `<#@ #>`.
- T4 Studio runtime APIs use the `T4Studio` namespace.
- The Visual Studio-compatible `Microsoft.VisualStudio.TextTemplating` API surface remains available where implemented.
- Upstream copyright and MIT license notices are preserved.

## Build

```bash
dotnet restore
dotnet build -c Release
```

## Test

```bash
dotnet test -c Release
```

Focused test examples:

```bash
dotnet test tests/T4Studio.Tests -c Release --filter "FullyQualifiedName~Roslyn"
dotnet test tests/T4Studio.Tests -c Release --filter "FullyQualifiedName~EdgeCases"
```

## Pack

```bash
dotnet pack -c Release -o ./artifacts/packages
```

Expected NuGet outputs:

- `T4Studio.Engine.0.1.0.nupkg`
- `T4Studio.Engine.0.1.0.snupkg`
- `T4Studio.Cli.0.1.0.nupkg`
- `T4Studio.Cli.0.1.0.snupkg`
- `T4Studio.Build.0.1.0.nupkg`
- `T4Studio.Build.0.1.0.snupkg`

## Editor Extensions

The repository currently contains:

- `.vscode/extensions/t4-syntax` - VS Code syntax highlighting extension source.
- `src/T4Studio.Vsix` - Visual Studio extension project for T4 language support.

These are editor extension assets, not NuGet packages.

## Release

Packages are published by the GitHub Actions workflow in `.github/workflows/publish-nuget.yml` when a tag matching `v*.*.*` is pushed.

```bash
git tag v0.1.0
git push origin v0.1.0
```

The workflow requires the `NUGET_API_KEY` repository secret.

See [docs/release.md](docs/release.md) for the full checklist.

## Upstream Attribution

T4 Studio is based on Mono.TextTemplating / mono/t4. The original implementation and compatibility work came from the Mono project and community contributors. This fork preserves upstream attribution and license notices while preparing a modern NuGet-distributed project under the T4 Studio identity.

## License

This project uses the MIT license, matching the existing upstream licensing in this repository. Keep the existing copyright and license notices when redistributing source or packages.

