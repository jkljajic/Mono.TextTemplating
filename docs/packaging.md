# Packaging

T4 Studio uses simple project-file versioning for the initial release. The shared version is defined in `Directory.Build.props`.

## Package IDs

| Project | Package ID | Packable |
|---|---|---|
| `src/T4Studio.Engine` | `T4Studio.Engine` | Yes |
| `src/T4Studio.Cli` | `T4Studio.Cli` | Yes |
| `src/T4Studio.Build` | `T4Studio.Build` | Yes |
| `src/T4Studio.Vsix` | None | No |

The core assembly is `T4Studio.Engine`; the runtime namespace is `T4Studio`.

## Metadata

Shared metadata:

- Version: `0.1.0`
- Authors: `Jovo Kljajic`
- Company: `T4 Studio`
- Repository: `https://github.com/jkljajic/T4Studio`
- License: `MIT`
- Symbols: `snupkg`
- Readme: `README.md`

## Local Validation

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet pack -c Release -o ./artifacts/packages
```

Expected package files include `.nupkg` and `.snupkg` outputs for each packable project.

## MSBuild Package Assets

`T4Studio.Build` packs the existing source files under package-ID-matching paths:

- `build/T4Studio.Build.props`
- `build/T4Studio.Build.targets`

NuGet imports build assets by package ID, so these packaged paths must stay aligned with the `T4Studio.Build` package ID.

