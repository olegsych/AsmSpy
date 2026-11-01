# Engineering system

AsmSpy uses [csharp.common](https://github.com/olegsych/csharp.common) through the
`modules/csharp.common` submodule, pinned to `addade9af30506901d01db03b7701af3c6aa946f`.
This is the same shared-system revision used by inspector and fuzzy, not an implicit tracking of the latest revision.
The root build props/targets, SDK selection, editor settings, machine provisioning scripts, signing key, and GitHub
agent/instruction/prompt/skill directories are relative symlinks. GitHub workflows, Git ignore/attributes, and
Copilot instructions are consumer-owned copies.

## Checkout

On Windows, enable Developer Mode or use an elevated shell before cloning so Git can create real symlinks.

```powershell
git clone -c core.symlinks=true --recurse-submodules https://github.com/olegsych/AsmSpy.git
Set-Location AsmSpy
git submodule update --init --recursive
```

The same recursive clone works on Linux. A non-recursive clone has missing shared configuration; a Windows clone with
`core.symlinks=false` has text placeholders instead of working configuration. Neither is a buildable checkout.
In an existing checkout, inspect local changes before setting `git config core.symlinks true` and synchronizing.

To deliberately synchronize the shared links and copies, use the documented shared initializer:

```powershell
pwsh -NoProfile -File .\modules\csharp.common\init.ps1
git diff --cached
```

It requires PowerShell supporting `Resolve-Path -RelativeBasePath`, and Windows symlink permissions. It stages changes,
overwrites managed copies, and removes mismatching managed files/directories, including an existing signing key.
Do not run it blindly on a customized checkout. Review and retain AsmSpy's project, CI, release, and ignore customizations
before committing. Updating the submodule gitlink alone does not synchronize copied workflows.
Root `init.cmd`/`init.sh` optionally provision a development machine; they are not repository synchronization or CI setup.

## Build and test

Install the .NET 10 SDK selected by the shared `global.json`; it rolls forward within .NET 10.
Windows execution additionally requires .NET Framework 4.7.2 or a compatible later runtime.
Reference assemblies are restored from NuGet, so compilation on either OS does not require Visual Studio.

```powershell
dotnet restore
dotnet test -c Debug --no-restore -bl:out\logs\test.binLog
dotnet build -c Release --no-restore -bl:out\logs\build.binLog
dotnet pack -c Release --no-build -bl:out\logs\pack.binLog
```

The root solution is `AsmSpy.slnx`. Build outputs are in `bin/<configuration>/<framework>`, intermediates in
`obj/<project>`, and NuGet packages/symbols in `out/packages`. With `CI=true`, Windows tests emit JUnit and Cobertura
reports into `out/tests` through xUnit v3 and Microsoft.Testing.Platform. Central versions in `Directory.Packages.props`
keep coverage 18.0.6 and MTP 1.9.1 together with xUnit 3.x.

Tests exercise Framework-only reflection loading, binding redirects, and GAC lookup. They execute only on Windows;
Linux CI compiles Debug instead of calling `dotnet test`, then builds Release and packs Core. The shared targets also
disable Framework test execution off Windows. One pre-existing system-assembly enumeration test remains skipped because
the test output does not have a stable set of system assemblies. Distribution tests verify the unsigned Core identity
and run the merged CLI without sibling DLLs.

Core and CLI continue to target `net472`; the test fixture remains `netstandard2.0`. No product frameworks were added.
All projects explicitly opt out of shared strong-name signing to preserve existing assembly identity.
Core and CLI retain their nullable-oblivious contracts rather than changing the public API as part of an engineering
migration. Core overrides shared package authors to retain original attribution and does not synthesize incomplete
XML API documentation. Its package now accurately declares `lib/net472`, replacing the old mismatched `net462` nuspec.

## Executable packaging

On Windows, building the CLI also runs the centrally restored ILMerge to produce `bin/Release/net472/AsmSpy.exe`.
ILMerge cannot read portable PDBs, so merging disables debug-symbol processing; unmerged assemblies retain their symbols,
including Core's `snupkg`. The CLI is explicitly neither an MTP application nor an SDK-packable `dotnet tool`.

After Release build and pack, install Chocolatey and run:

```powershell
pwsh -NoProfile -File .\CreateRelease.ps1
```

This produces `out/asmspy.<version>.zip`, `out/executable`, and `out/chocolatey/asmspy.<version>.nupkg`.
The Chocolatey package embeds the executable, configuration, MIT license, and third-party notices; it does not download
an unversioned external ZIP. Packaging verifies matching Core/package/executable builds and takes its version from the
built Core package. Keep exactly one Core `nupkg` in
`out/packages`; remove stale generated packages before repacking a different commit.

## Versions and publication

`version.json` preserves AsmSpy's 1.3 release line. Nerdbank.GitVersioning derives the patch from Git history and adds
a commit suffix to non-public builds. Product assembly versions stay `1.2.0.0`; file/informational versions track the
build. Full history is required. The former AppVeyor counter and ZeroToNine source-rewriting tool are no longer used.

CI builds master pushes, pull requests targeting master, and `v*` tags on Windows and Ubuntu, and always uploads `out`.
Ordinary master pushes only produce artifacts. To publish, tag a master commit with `v<computed-package-version>`;
use the Core package version from that commit's successful master build. The tag workflow rebuilds, verifies that the
commit is on master and that both package versions match the tag, then publishes Core to NuGet and the executable package
to Chocolatey. It does not auto-create tags or GitHub Releases. ZIPs remain available in Windows build artifacts.

Configure repository secrets `NUGET_API_KEY` and `CHOCOLATEY_API_KEY` for tag publication.
`CODECOV_TOKEN` enables optional report uploads; missing reports (including Linux runs) are handled explicitly.
NuGet packages and Chocolatey packages are kept in separate directories to prevent accidental cross-publication.
