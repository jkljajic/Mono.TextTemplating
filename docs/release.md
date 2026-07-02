# Release

Use simple tag-based releases for the initial T4 Studio packages.

## Checklist

1. Update the version in `Directory.Build.props`.
2. Update `CHANGELOG.md`.
3. Restore dependencies:

   ```bash
   dotnet restore
   ```

4. Build release configuration:

   ```bash
   dotnet build -c Release
   ```

5. Run tests:

   ```bash
   dotnet test -c Release
   ```

6. Pack locally:

   ```bash
   dotnet pack -c Release -o ./artifacts/packages
   ```

7. Inspect the generated `.nupkg` and `.snupkg` files in `artifacts/packages`.
8. Commit the release changes.
9. Create and push the tag:

   ```bash
   git tag v0.1.0
   git push origin v0.1.0
   ```

10. Verify the `ci.yml` GitHub Actions run.
11. Verify the packages on nuget.org.

## Trusted Publishing

NuGet publishing uses trusted publishing from GitHub Actions. No `NUGET_API_KEY` secret is required.

NuGet must be configured to trust:

```text
Repository owner: jkljajic
Repository: T4Studio
Workflow: ci.yml
Environment: production
```

The publish job requests GitHub's OIDC token with `id-token: write`, runs only for `v*.*.*` tags, and pushes only `.nupkg` files to `https://api.nuget.org/v3/index.json` with `--skip-duplicate`.

## GitHub Packages

The same `ci.yml` tag run also publishes the `.nupkg` files to GitHub Packages:

```text
https://nuget.pkg.github.com/jkljajic/index.json
```

This uses the built-in `GITHUB_TOKEN`, not a personal access token. The publish job has:

```yaml
permissions:
  contents: write
  packages: write
  id-token: write
```

Consumers can add the GitHub Packages source when they want packages from GitHub instead of nuget.org:

```bash
dotnet nuget add source \
  --username USERNAME \
  --password TOKEN \
  --store-password-in-clear-text \
  --name github \
  "https://nuget.pkg.github.com/jkljajic/index.json"
```

Use a classic PAT for local restore/install from GitHub Packages. Inside GitHub Actions, use `GITHUB_TOKEN` when the package is associated with the same repository or the repository has been granted package access.

## VS Code Marketplace

The VS Code extension lives in `.vscode/extensions/t4-syntax`.

Package it locally:

```bash
cd .vscode/extensions/t4-syntax
vsce package
```

Publish through GitHub Actions by pushing a VS Code extension tag:

```bash
git tag vscode-v0.1.0
git push origin vscode-v0.1.0
```

The `.github/workflows/vscode-marketplace.yml` workflow packages the extension on pushes and pull requests. It publishes only for `vscode-v*.*.*` tags.

Publishing to the Visual Studio Marketplace from GitHub Actions requires this repository secret:

```text
VSCE_PAT
```

The token must be an Azure DevOps/Visual Studio Marketplace token that can manage the `t4studio` publisher. Microsoft recommends moving away from long-lived PATs; for now, NuGet trusted publishing and VS Code Marketplace publishing use different authentication systems.
