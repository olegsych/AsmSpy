using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace AsmSpy.Core.Tests;

public sealed class DistributionTests
{
    [Fact]
    public void CoreAssemblyHasExpectedIdentity()
    {
        AssemblyName name = typeof(DependencyAnalyzer).Assembly.GetName();
        Assert.Equal("AsmSpy.Core", name.Name);
        Assert.Equal(new Version(ThisAssembly.AssemblyVersion), name.Version);
        Assert.Empty(name.GetPublicKeyToken());
    }

    [Fact]
    public async Task MergedExecutableRunsWithoutSiblingAssemblies()
    {
        string bin = Path.GetDirectoryName(typeof(DistributionTests).Assembly.Location);
        string executable = Path.Combine(bin, "AsmSpy.exe");
        AssemblyName name = AssemblyName.GetAssemblyName(executable);
        Assert.Equal("AsmSpy", name.Name);
        Assert.Equal(new Version(ThisAssembly.AssemblyVersion), name.Version);
        Assert.Empty(name.GetPublicKeyToken());

        string directory = Path.Combine(Path.GetTempPath(), "AsmSpy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            string isolated = Path.Combine(directory, "AsmSpy.exe");
            File.Copy(executable, isolated);
            File.Copy(Path.Combine(bin, "AsmSpy.CommandLine.exe.config"), isolated + ".config");
            using var process = Process.Start(new ProcessStartInfo(isolated, "--help") {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Unicode,
                StandardErrorEncoding = Encoding.Unicode,
                WorkingDirectory = directory
            });
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) {
                process.Kill();
                Assert.Fail("The isolated executable did not exit within 30 seconds.");
            }
            Assert.Equal(0, process.ExitCode);
            Assert.Contains("directoryOrFile", await output);
            Assert.Equal(string.Empty, await error);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }
}
