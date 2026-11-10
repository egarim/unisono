# Releasing Unisono to NuGet

Releases are published by `.github/workflows/release.yml` through **NuGet Trusted Publishing**: the workflow gets a
one-hour key from nuget.org over OpenID Connect, so no API key is stored anywhere. (nuget.org capped API keys at
30 days in August 2026 and expired older ones on 1 November 2026.)

## One-time setup (account owner)

1. **nuget.org policy.** Sign in to nuget.org, open your profile menu, choose **Trusted Publishing**, add a policy:
   - Repository Owner: `egarim`
   - Repository: `unisono`
   - Workflow File: `release.yml` (file name only)
   - Environment: `release`
   - Scopes: publish new packages and new versions; glob `Unisono.*`
2. **GitHub environment.** In the repo, Settings → Environments → New environment `release`. Optionally require your
   own review before the job runs.
3. **GitHub secret.** Settings → Secrets and variables → Actions → New repository secret `NUGET_USER` with your
   nuget.org **profile name** (not your email). This is the only secret the workflow uses.
4. After the first publish, request the `Unisono.` **ID prefix reservation** on nuget.org (Account → Package ID
   prefix reservation) for the verified checkmark.

## Every release

1. Update `CHANGELOG.md` and the one-line `PackageReleaseNotes` in `src/Directory.Build.props`; bump `Version` there
   so local packs match.
2. Commit, then tag and push the tag; the tag minus the `v` is the published version:

   ```bash
   git tag v0.1.0-preview.1
   git push origin v0.1.0-preview.1
   ```

3. Watch the **Release to NuGet** run. It packs the four packages with the tag's version, uploads them as a workflow
   artifact, logs in with OIDC and pushes `.nupkg` + `.snupkg`. Indexing on nuget.org takes a few minutes.

`workflow_dispatch` runs the same job by hand with an explicit version (for example to republish after a failed push).

## Local pack (for inspection, not for publishing)

```bash
dotnet pack src/Unisono.Core/Unisono.Core.csproj -c Release -o artifacts/nupkg -p:ValidateXcodeVersion=false
unzip -l artifacts/nupkg/Unisono.Core.*.nupkg
```

## Notes

- The App Intents packages target `net10.0-ios` and `net10.0-maccatalyst`, so the job runs on a macOS runner and
  installs the `ios` and `maccatalyst` workloads. `ValidateXcodeVersion=false` tolerates a runner Xcode that is a
  minor version away from the workload's expectation.
- The metadata follows Microsoft's package authoring guidance: license expression (MIT), README, 128 px icon,
  project and repository URLs with SourceLink, tags, release notes, SemVer prerelease suffixes.
