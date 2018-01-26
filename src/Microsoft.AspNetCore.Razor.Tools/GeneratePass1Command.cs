// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.AspNetCore.Razor.Tools
{
    internal class GeneratePass1Command : CommandBase
    {
        public GeneratePass1Command(Application parent)
            : base(parent, "generate-pass1")
        {
            Sources = Option("-s|--source", ".cshtml files to compile", CommandOptionType.MultipleValue);
            OutputFiles = Option("-o|--output", "output file names", CommandOptionType.MultipleValue);
            ProjectDirectory = Option("-p", "project root directory", CommandOptionType.SingleValue);
        }

        public CommandOption Sources { get; }

        public CommandOption OutputFiles { get; }

        public CommandOption ProjectDirectory { get; }

        protected override Task<int> ExecuteCoreAsync()
        {
            var result = ExecuteCore(
                projectDirectory: ProjectDirectory.Value(),
                sources: Sources.Values.ToArray(),
                outputFiles: OutputFiles.Values.ToArray());

            return Task.FromResult(result);
        }

        protected override bool ValidateArguments()
        {
            if (string.IsNullOrEmpty(OutputFiles.Value()))
            {
                Error.WriteLine($"{OutputFiles.ValueName} not specified.");
                return false;
            }

            if (Sources.Values.Count == 0)
            {
                Error.WriteLine($"{Sources.LongName} should have at least one value.");
                return false;
            }

            if (Sources.Values.Count != OutputFiles.Values.Count)
            {
                Error.WriteLine($"{Sources.LongName} and {OutputFiles.LongName} should have the same number of values.");
                return false;
            }

            if (string.IsNullOrEmpty(ProjectDirectory.Value()))
            {
                ProjectDirectory.Values.Add(Environment.CurrentDirectory);
            }

            return true;
        }

        private int ExecuteCore(string projectDirectory, string[] sources, string[] outputFiles)
        {
            var engine = RazorEngine.Create(b =>
            {
                RazorExtensions.Register(b);

                b.Phases.Insert(b.Phases.Count - 1, new TheEliminator());
            });

            var templateEngine = new MvcRazorTemplateEngine(engine, RazorProject.Create(projectDirectory));

            var workItems = GetRazorFiles(projectDirectory, sources, outputFiles);
            var results = GenerateCode(templateEngine, workItems);

            var success = true;

            foreach (var result in results)
            {
                if (result.CSharpDocument.Diagnostics.Count > 0)
                {
                    success = false;
                    foreach (var error in result.CSharpDocument.Diagnostics)
                    {
                        Console.Error.WriteLine(error.ToString());
                    }
                }

                File.WriteAllText(result.OutputFilePath, result.CSharpDocument.GeneratedCode);
            }

            return success ? 0 : -1;
        }

        private List<WorkItem> GetRazorFiles(string projectDirectory, string[] sources, string[] outputFiles)
        {
            var trimLength = projectDirectory.EndsWith("/") ? projectDirectory.Length - 1 : projectDirectory.Length;

            var items = new List<WorkItem>(sources.Length);
            for (var i = 0; i < sources.Length; i++)
            {
                var sourceFilePath = Path.Combine(projectDirectory, sources[i]);
                var outputFilePath = Path.Combine(projectDirectory, outputFiles[i]);
                if (sourceFilePath.StartsWith(projectDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    var viewEnginePath = sourceFilePath.Substring(trimLength).Replace('\\', '/');
                    items.Add(new WorkItem(sourceFilePath, viewEnginePath, outputFilePath));
                }
            }

            return items;
        }

        private OutputItem[] GenerateCode(RazorTemplateEngine templateEngine, IReadOnlyList<WorkItem> sources)
        {
            var outputs = new OutputItem[sources.Count];
            Parallel.For(0, outputs.Length, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, i =>
            {
                var source = sources[i];

                var csharpDocument = templateEngine.GenerateCode(source.ViewEnginePath);
                outputs[i] = new OutputItem(source, csharpDocument);
            });

            return outputs;
        }

        private struct OutputItem
        {
            public OutputItem(
                WorkItem input,
                RazorCSharpDocument cSharpDocument)
            {
                Input = input;
                CSharpDocument = cSharpDocument;
            }

            public WorkItem Input { get; }

            public string OutputFilePath => Input.OutputFilePath;

            public RazorCSharpDocument CSharpDocument { get; }
        }

        private struct WorkItem
        {
            public WorkItem(string sourceFilePath, string viewEnginePath, string outputFilePath)
            {
                SoureFilePath = sourceFilePath;
                ViewEnginePath = viewEnginePath;
                OutputFilePath = outputFilePath;
            }

            public string SoureFilePath { get; }

            public string OutputFilePath { get; }

            public string ViewEnginePath { get; }

            public Stream CreateReadStream()
            {
                // We are setting buffer size to 1 to prevent FileStream from allocating it's internal buffer
                // 0 causes constructor to throw
                var bufferSize = 1;
                return new FileStream(
                    SoureFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    bufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
            }
        }
    }
}