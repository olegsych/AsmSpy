using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using AsmSpy.CommandLine.Visualizers;
using AsmSpy.Core;
using Microsoft.Extensions.CommandLineUtils;

namespace AsmSpy.CommandLine
{
    public class Program
    {
        readonly CommandLineApplication command = new CommandLineApplication(throwOnUnexpectedArg: true);
        readonly CommandArgument directoryOrFile;
        readonly CommandOption silent;
        readonly CommandOption nonsystem;
        readonly CommandOption all;
        readonly CommandOption referencedStartsWith;
        readonly CommandOption excludeAssemblies;
        readonly CommandOption includeSubDirectories;
        readonly CommandOption configurationFile;
        readonly CommandOption failOnMissing;
        readonly IEnumerable<IDependencyVisualizer> visualizers = new IDependencyVisualizer[]
        {
            new ConsoleVisualizer(),
            new ConsoleTreeVisualizer(),
            new DgmlExport(),
            new XmlExport(),
            new DotExport(),
            new BindingRedirectExport(),
        };

        Program()
        {
            directoryOrFile = command.Argument("directoryOrFile", "The directory to search for assemblies or file path to a single assembly");

            silent = command.Option("-s|--silent", "Do not show any message, only warnings and errors will be shown.", CommandOptionType.NoValue);
            nonsystem = command.Option("-n|--nonsystem", "Ignore 'System' assemblies", CommandOptionType.NoValue);
            all = command.Option("-a|--all", "List all assemblies and references.", CommandOptionType.NoValue);
            referencedStartsWith = command.Option("-rsw|--referencedstartswith", "Referenced Assembly should start with <string>. Will only analyze assemblies if their referenced assemblies starts with the given value.", CommandOptionType.SingleValue);
            excludeAssemblies = command.Option("-e|--exclude", "A partial assembly name which should be excluded. This option can be provided multiple times", CommandOptionType.MultipleValue);
            includeSubDirectories = command.Option("-i|--includesub", "Include subdirectories in search", CommandOptionType.NoValue);
            configurationFile = command.Option("-c|--configurationFile", "Use the binding redirects of the given configuration file (Web.config or App.config)", CommandOptionType.SingleValue);
            failOnMissing = command.Option("-f|--failOnMissing", "Whether to exit with an error code when AsmSpy detected Assemblies which could not be found", CommandOptionType.NoValue);

            foreach (IDependencyVisualizer v in visualizers)
                v.CreateOption(command);

            command.HelpOption("-? | -h | --help");

            command.OnExecute(Execute);
        }

        int Execute()
        {
            var options = new VisualizerOptions
            {
                SkipSystem = nonsystem.HasValue(),
                OnlyConflicts = !all.HasValue(),
                ReferencedStartsWith = referencedStartsWith.HasValue() ? referencedStartsWith.Value() : string.Empty,
                Exclude = excludeAssemblies.HasValue() ? excludeAssemblies.Values : new List<string>()
            };

            var logger = new ConsoleLogger(!silent.HasValue());
            WriteLogo(logger);

            Result<bool> result = GetFileList(directoryOrFile, includeSubDirectories, logger)
                .Bind(x => GetAppDomainWithBindingRedirects(configurationFile)
                    .Map(appDomain => DependencyAnalyzer.Analyze(
                        x.FileList,
                        appDomain,
                        logger,
                        options,
                        x.RootFileName)))
                .Map(analysis => Visualize(analysis, logger, options))
                .Bind(analysis => FailOnMissingAssemblies(analysis, failOnMissing));

            switch (result)
            {
                case Failure<bool> failure:
                    logger.LogError(failure.Message);
                    return -1;
                case Success<bool> success:
                    return 0;
                default:
                    throw new InvalidOperationException("Unexpected result type");
            }
        }

        DependencyAnalyzerResult Visualize(DependencyAnalyzerResult result, ILogger logger, VisualizerOptions options)
        {
            foreach (IDependencyVisualizer visualizer in visualizers.Where(x => x.IsConfigured()))
                visualizer.Visualize(result, logger, options);
            return result;
        }

        int Run(string[] args)
        {
            try
            {
                if (args == null || args.Length == 0)
                {
                    command.ShowHelp();
                    return 0;
                }

                return command.Execute(args);
            }
            catch (CommandParsingException e)
            {
                Console.WriteLine(e.Message);
                return -1;
            }
        }

        public static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.Unicode;
                var program = new Program();
                return program.Run(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return -1;
            }
        }

        private static void WriteLogo(ConsoleLogger console)
        {
            var program = Assembly.GetExecutingAssembly();
            string title = program.GetCustomAttribute<AssemblyTitleAttribute>().Title;
            string version = program.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            console.LogMessage($"{title} {version} {RuntimeInformation.ProcessArchitecture}");
            console.LogMessage(RuntimeInformation.FrameworkDescription);
            console.LogMessage($"{RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}");
        }

        private static Result<bool> FailOnMissingAssemblies(DependencyAnalyzerResult analysis, CommandOption failOnMissing)
        {
            if (!failOnMissing.HasValue() || !analysis.MissingAssemblies.Any())
                return Result<bool>.Succeed(true);

            var errors = new StringBuilder();
            foreach (AssemblyReferenceInfo assembly in analysis.MissingAssemblies) {
                errors.Append($"Assembly '{assembly.AssemblyName}' could not be resolved.");
                if (assembly.HasAlternativeVersion)
                    errors.Append($" Version {assembly.AlternativeFoundVersion.AssemblyName.Version} is available but requires a binding redirect.");
                errors.AppendLine();
            }

           return Result<bool>.Fail(errors.ToString());
        }

        private static Result<(List<FileInfo> FileList, string RootFileName)> GetFileList(CommandArgument directoryOrFile, CommandOption includeSubDirectories, ILogger logger)
        {
            var searchPattern = includeSubDirectories.HasValue() ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var directoryOrFilePath = directoryOrFile.Value;
            var directoryPath = directoryOrFilePath;

            if (!File.Exists(directoryOrFilePath) && !Directory.Exists(directoryOrFilePath))
            {
                return (string.Format(CultureInfo.InvariantCulture, "Directory or file: '{0}' does not exist.", directoryOrFilePath));
            }

            var rootFileName = "";
            if (File.Exists(directoryOrFilePath))
            {
                rootFileName = Path.GetFileName(directoryOrFilePath);
                logger.LogMessage($"Root assembly specified: '{rootFileName}'");
                directoryPath = Path.GetDirectoryName(directoryOrFilePath);
            }

            var directoryInfo = new DirectoryInfo(directoryPath);

            logger.LogMessage($"Checking for local assemblies in: '{directoryInfo}', {searchPattern}");

            var fileList = directoryInfo.GetFiles("*.dll", searchPattern).Concat(directoryInfo.GetFiles("*.exe", searchPattern)).ToList();

            return (fileList, rootFileName);
        }

        public static Result<AppDomain> GetAppDomainWithBindingRedirects(CommandOption configurationFile)
        {
            var configurationFilePath = configurationFile.Value();
            if (!string.IsNullOrEmpty(configurationFilePath) && !File.Exists(configurationFilePath))
            {
                return $"Directory or file: '{configurationFilePath}' does not exist.";
            }

            try
            {
                var domaininfo = new AppDomainSetup
                {
                    ConfigurationFile = configurationFilePath
                };
                return AppDomain.CreateDomain("AppDomainWithBindingRedirects", null, domaininfo);
            }
            catch (Exception ex)
            {
                return $"Failed creating AppDomain from configuration file with message {ex.Message}";
            }
        }
    }
}
