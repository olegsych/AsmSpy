# Checkout

On Windows, enable Developer Mode or use an elevated shell to create symlinks.

```bat
git clone -c core.symlinks=true --recurse-submodules https://github.com/olegsych/AsmSpy.git
cd AsmSpy
```

## Build and test

Use the SDK selected by `global.json`. Run tests only on Windows: reflection-only loading, binding redirects, and GAC
lookup require .NET Framework. On Linux, replace the test command with `dotnet build -c Debug --no-restore`.

```powershell
dotnet restore
dotnet test -c Debug --no-restore -bl:out\logs\test.binLog
dotnet build -c Release --no-restore -bl:out\logs\build.binLog
dotnet pack -c Release --no-build -bl:out\logs\pack.binLog
```

When updating test dependencies, keep xUnit and the
coverage extension compatible with the selected Microsoft.Testing.Platform major version.

## Executable packaging

After Release build and pack, run on Windows with Chocolatey installed:

```powershell
pwsh -NoProfile -File .\CreateRelease.ps1
```

Keep exactly one Core `nupkg` in `out/packages`; remove stale generated packages before repacking another commit.
Publish packages from `out/packages` to NuGet and `out/chocolatey` to Chocolatey. The executable ZIP is in `out`.
ILMerge cannot read portable PDBs, so only the unmerged assemblies retain debug symbols.
