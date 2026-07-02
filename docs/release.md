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

10. Verify the `publish-nuget.yml` GitHub Actions run.
11. Verify the packages on nuget.org.

## Required Secret

Set this GitHub repository secret before pushing a release tag:

```text
NUGET_API_KEY
```

The publish workflow pushes only `.nupkg` files to `https://api.nuget.org/v3/index.json` and uses `--skip-duplicate`.
