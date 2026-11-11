# Releasing Unisono to NuGet

Releases are published by `.github/workflows/release.yml` through **NuGet Trusted Publishing**: the workflow gets a
one-hour key from nuget.org over OpenID Connect, so no API key is stored anywhere. (nuget.org capped API keys at
30 days in August 2026 and expired older ones on 1 November 2026.)

## One-time setup (account owner)

1. **nuget.org policy.** Sign in to nuget.org, open your profile menu, choose **Trusted Publishing**, add a policy:
   - Policy Name: anything, e.g. `Unisono release`
   - Package Owner: your profile (`egarim`); CI/CD Provider: GitHub Actions
   - Repository Owner: `egarim` (the GitHub owner)
   - Repository: `unisono`
   - Workflow File: `release.yml` (file name only, not the workflow's display name)
   - Environment: `release`
   - Scopes: Push → "Push new packages and package versions"
   - Glob Patterns and Packages: `Unisono.*` (required; without it the policy can push nothing)

   For a public repository nuget.org resolves the GitHub owner and repository IDs at once and the policy shows
   **Active**; for a private one it is temporary for 7 days until the first publish. The policy must exist before the
   workflow runs: the OIDC exchange has nothing to match otherwise.
2. **GitHub environment.** In the repo, Settings → Environments → New environment `release`. Optionally require your
   own review before the job runs.
3. **GitHub secret.** Settings → Secrets and variables → Actions → New repository secret `NUGET_USER` with your
   nuget.org **profile name** (not your email). This is the only secret the workflow uses.
4. After the first publish, request the `Unisono.` **ID prefix reservation** for the verified checkmark; the
   request form is linked from the nuget.org docs page "Package ID prefix reservation".

## Every release

1. Update `CHANGELOG.md` and the one-line `PackageReleaseNotes` in `src/Directory.Build.props`; bump `Version` there
   so local packs match.
2. Commit, then tag and push the tag; the tag minus the `v` is the published version:

   ```bash
   git tag v0.1.0-preview.1
   git push origin v0.1.0-preview.1
   ```

3. Watch the **Release to NuGet** run. It packs the five packages with the tag's version, uploads them as a workflow
   artifact, logs in with OIDC and pushes `.nupkg` + `.snupkg`. Indexing on nuget.org takes a few minutes.

`workflow_dispatch` runs the same job by hand with an explicit version (for example to republish after a failed push):

```bash
gh workflow run release.yml --ref v0.1.0-preview.1 -f version=0.1.0-preview.1
```

## Local pack (for inspection, not for publishing)

```bash
dotnet pack src/Unisono.Core/Unisono.Core.csproj -c Release -o artifacts/nupkg -p:ValidateXcodeVersion=false
unzip -l artifacts/nupkg/Unisono.Core.*.nupkg
```

## Notes

- The App Intents packages target `net10.0-ios` and `net10.0-maccatalyst`, so the job runs on a macOS runner and
  installs the `ios` and `maccatalyst` workloads; `Unisono.AppFunctions` targets `net10.0-android` and needs the
  `android` workload. `ValidateXcodeVersion=false` tolerates a runner Xcode that is a
  minor version away from the workload's expectation.
- The metadata follows Microsoft's package authoring guidance: license expression (MIT), README, 128 px icon,
  project and repository URLs with SourceLink, tags, release notes, SemVer prerelease suffixes.

## Troubleshooting

- `Token exchange failed (HTTP 401) ... No matching trust policy owned by user 'NAME' was found.` nuget.org found no
  policy **created by** the profile `NAME` that matches the run. Either `NUGET_USER` is not the profile that created
  the policy (the name shown top right on nuget.org, not an email), or one of owner / repository / workflow file /
  environment differs, or the policy was never saved. For a policy that has not published yet nuget.org deliberately
  hides which field failed, so check all four against the list above, then rerun with `gh workflow run` as shown.
- `Input required and not supplied: user`: the `NUGET_USER` secret is missing in the repository.
- The push step uses `--skip-duplicate`, so rerunning a version that is already on nuget.org is harmless.
