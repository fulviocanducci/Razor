// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Remote.Razor
{
    internal class RazorLanguageService : RazorServiceBase
    {
        private readonly static RazorConfiguration DefaultConfiguration = FallbackRazorConfiguration.MVC_2_0;

        public RazorLanguageService(Stream stream, IServiceProvider serviceProvider)
            : base(stream, serviceProvider)
        {
        }

        public async Task<TagHelperResolutionResult> GetTagHelpersAsync(
            ProjectSnapshotHandle projectHandle, 
            string factoryTypeName,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var project = await GetProjectSnapshotAsync(projectHandle, cancellationToken).ConfigureAwait(false);

            var templateEngine = CreateTemplateEngine(project, factoryTypeName);

            var descriptors = new List<TagHelperDescriptor>();

            var providers = templateEngine.Engine.Features.OfType<ITagHelperDescriptorProvider>().ToArray();

            var results = new List<TagHelperDescriptor>();
            var context = TagHelperDescriptorProviderContext.Create(results);
            context.SetCompilation(await project.WorkspaceProject.GetCompilationAsync());

            for (var i = 0; i < providers.Length; i++)
            {
                var provider = providers[i];
                provider.Execute(context);
            }

            var diagnostics = new List<RazorDiagnostic>();
            var resolutionResult = new TagHelperResolutionResult(results, diagnostics);

            return resolutionResult;
        }

        internal RazorTemplateEngine CreateTemplateEngine(ProjectSnapshot project, string factoryTypeName)
        {
            // This section is really similar to the code DefaultTemplatEngineFactoryService
            // but with a few differences that are significant in the remote scenario
            //
            // Most notably, we are going to find the Tag Helpers using a compilation, and we have
            // no editor settings.
            Action<IRazorEngineBuilder> configure = (b) =>
            {
                b.Features.Add(new DefaultTagHelperDescriptorProvider() { DesignTime = true });
            };

            // The default configuration currently matches MVC-2.0. Beyond MVC-2.0 we added SDK support for 
            // properly detecting project versions, so that's a good version to assume when we can't find a
            // configuration.
            var configuration = project?.Configuration ?? DefaultConfiguration;

            // If there's no factory to handle the configuration then fall back to a very basic configuration.
            //
            // This will stop a crash from happening in this case (misconfigured project), but will still make
            // it obvious to the user that something is wrong.
            var factory = CreateFactory(configuration, factoryTypeName) ?? RazorServices.FallbackTemplateEngineFactory;
            return factory.Create(configuration, EmptyProject.Instance, configure);
        }

        private ITemplateEngineFactory CreateFactory(RazorConfiguration configuration, string factoryTypeName)
        {
            if (factoryTypeName == null)
            {
                return null;
            }

            return (ITemplateEngineFactory)Activator.CreateInstance(Type.GetType(factoryTypeName, throwOnError: true));
        }

        public Task<IEnumerable<DirectiveDescriptor>> GetDirectivesAsync(Guid projectIdBytes, string projectDebugName, CancellationToken cancellationToken = default(CancellationToken))
        {
            var projectId = ProjectId.CreateFromSerialized(projectIdBytes, projectDebugName);

            var engine = RazorEngine.Create();
            var directives = engine.Features.OfType<IRazorDirectiveFeature>().FirstOrDefault()?.Directives;
            return Task.FromResult(directives ?? Enumerable.Empty<DirectiveDescriptor>());
        }

        public Task<GeneratedDocument> GenerateDocumentAsync(Guid projectIdBytes, string projectDebugName, string filePath, string text, CancellationToken cancellationToken = default(CancellationToken))
        {
            var projectId = ProjectId.CreateFromSerialized(projectIdBytes, projectDebugName);

            var engine = RazorEngine.Create();

            RazorSourceDocument source;
            using (var stream = new MemoryStream())
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);

                stream.Seek(0L, SeekOrigin.Begin);
                source = RazorSourceDocument.ReadFrom(stream, filePath, Encoding.UTF8);
            }

            var code = RazorCodeDocument.Create(source);
            engine.Process(code);

            var csharp = code.GetCSharpDocument();
            if (csharp == null)
            {
                throw new InvalidOperationException();
            }

            return Task.FromResult(new GeneratedDocument() { Text = csharp.GeneratedCode, });
        }
    }
}
