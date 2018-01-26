// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.VisualStudio.LanguageServices.Razor;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.Tools
{
    internal class GeneratePass2Command : CommandBase
    {
        public GeneratePass2Command(Application parent)
            : base(parent, "generate-pass2")
        {
            Sources = Option("-s|--source", ".cshtml files to compile", CommandOptionType.MultipleValue);
            OutputFiles = Option("-o|--output", "output file names", CommandOptionType.MultipleValue);
            ProjectDirectory = Option("-p", "project root directory", CommandOptionType.SingleValue);
            TagHelperManifest = Option("-t", "tag helper manifest file", CommandOptionType.SingleValue);
        }

        public CommandOption Sources { get; }

        public CommandOption OutputFiles { get; }

        public CommandOption ProjectDirectory { get; }

        public CommandOption TagHelperManifest { get; }

        protected override Task<int> ExecuteCoreAsync()
        {
            var result = ExecuteCore(
                projectDirectory: ProjectDirectory.Value(),
                tagHelperManifest: TagHelperManifest.Value(),
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

        private int ExecuteCore(string projectDirectory, string tagHelperManifest, string[] sources, string[] outputFiles)
        {
            tagHelperManifest = Path.Combine(projectDirectory, tagHelperManifest);

            var tagHelpers = GetTagHelpers(tagHelperManifest);

            var engine = RazorEngine.Create(b =>
            {
                RazorExtensions.Register(b);

                b.Features.Add(new StaticTagHelperFeature() { TagHelpers = tagHelpers, });
            });

            var templateEngine = new MvcRazorTemplateEngine(engine, RazorProject.Create(projectDirectory));

            var sourceItems = GetRazorFiles(projectDirectory, sources, outputFiles);
            var results = GenerateCode(templateEngine, sourceItems);

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

        private IReadOnlyList<TagHelperDescriptor> GetTagHelpers(string tagHelperManifest)
        {
            if (!File.Exists(tagHelperManifest))
            {
                return Array.Empty<TagHelperDescriptor>();
            }

            using (var stream = File.OpenRead(tagHelperManifest))
            {
                var reader = new JsonTextReader(new StreamReader(stream));

                var serializer = new JsonSerializer();
                serializer.Converters.Add(new RazorDiagnosticJsonConverter());
                serializer.Converters.Add(new TagHelperDescriptorJsonConverter());

                var descriptors = serializer.Deserialize<IReadOnlyList<TagHelperDescriptor>>(reader);
                return descriptors;
            }
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

        private class StaticTagHelperFeature : ITagHelperFeature
        {
            public RazorEngine Engine { get; set; }

            public IReadOnlyList<TagHelperDescriptor> TagHelpers { get; set; }

            public IReadOnlyList<TagHelperDescriptor> GetDescriptors() => TagHelpers;
        }
    }
}