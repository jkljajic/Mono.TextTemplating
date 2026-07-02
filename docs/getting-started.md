# Getting Started

T4 Studio processes Visual Studio-compatible `.tt` templates using the `T4Studio` engine API.

## Install the CLI

```bash
dotnet tool install -g T4Studio.Cli
```

Transform a template:

```bash
t4studio -o output.cs template.tt
```

Preprocess a template into a runtime class:

```bash
t4studio -c MyProject.GeneratedTemplate template.tt
```

## Install the Library

```bash
dotnet add package T4Studio.Engine
```

```csharp
using T4Studio;

var generator = new TemplateGenerator();
generator.ProcessTemplate("template.tt", "output.txt");
```

## Add MSBuild Integration

```xml
<PackageReference Include="T4Studio.Build" Version="0.1.0" PrivateAssets="all" />
```

By default, `.tt` files are discovered as `T4PostBuildTemplate` items and transformed after `Build`.

```xml
<ItemGroup>
  <T4PostBuildTemplate Include="Templates\*.tt">
    <LastGenOutput>$(OutDir)%(Filename).generated.cs</LastGenOutput>
  </T4PostBuildTemplate>
</ItemGroup>
```

Set `EnableDefaultT4Items=false` to disable automatic discovery.

## Compatibility

Existing `.tt` syntax remains supported. The engine API now uses the `T4Studio` namespace, and the Visual Studio-compatible `Microsoft.VisualStudio.TextTemplating` API surface remains available where implemented.

