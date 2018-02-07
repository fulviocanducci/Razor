// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    internal class DefaultTagHelperResolver : TagHelperResolver
    {
        private readonly ErrorReporter _errorReporter;
        private readonly Workspace _workspace;
        private readonly RazorTemplateEngineFactoryService _factory;

        public DefaultTagHelperResolver(
            ErrorReporter errorReporter,
            Workspace workspace,
            RazorTemplateEngineFactoryService factory)
        {
            _errorReporter = errorReporter;
            _workspace = workspace;
            _factory = factory;
        }

        public async override Task<TagHelperResolutionResult> GetTagHelpersAsync(
            ProjectSnapshot project, 
            CancellationToken cancellationToken = default)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (!project.IsInitialized || project.Configuration == null)
            {
                return TagHelperResolutionResult.Empty;
            }

            // Not every custom factory supports the OOP host. Our priority system should work like this:
            //
            // 1. Use custom factory out of process
            // 2. Use custom factory in process
            // 3. Use fallback factory in process
            //
            // Calling into RazorTemplateEngineFactoryService.Create will accomplish #2 and #3 in one step.
            var factory = _factory.FindSerializableFactory(project);

            try
            {
                TagHelperResolutionResult result = null;
                if (factory != null)
                {
                    result = await ResolveTagHelpersOutOfProcessAsync(factory, project);
                    if (result != null)
                    {
                        return result;
                    }
                }

                // fall back to in process if needed.
                result = await ResolveTagHelpersInProcessAsync(project);
                return result;
            }
            catch (Exception exception)
            {
                _errorReporter.ReportError(exception, project.WorkspaceProject);

                throw new InvalidOperationException(
                    Resources.FormatUnexpectedException(
                        typeof(DefaultTagHelperResolver).FullName,
                        nameof(GetTagHelpersAsync)),
                    exception);
            }
        }

        protected virtual async Task<TagHelperResolutionResult> ResolveTagHelpersOutOfProcessAsync(
            ITemplateEngineFactory factory, 
            ProjectSnapshot project)
        {
            // We're being overly defensive here because the OOP host can return null for the client/session/operation
            // when it's disconnected (user stops the process).
            //
            // This will change in the future to an easier to consume API but for VS RTM this is what we have.
            var client = await RazorLanguageServiceClientFactory.CreateAsync(_workspace, CancellationToken.None);
            if (client != null)
            {
                using (var session = await client.CreateSessionAsync(project.WorkspaceProject.Solution))
                {
                    if (session != null)
                    {
                        var args = new object[]
                        {
                            Serialize(project),
                            factory == null ? null : factory.GetType().AssemblyQualifiedName,
                        };

                        var json = await session.InvokeAsync<JObject>("GetTagHelpersAsync", args, CancellationToken.None).ConfigureAwait(false);
                        var result = Deserialize(json);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }
            }

            return null;
        }

        protected virtual async Task<TagHelperResolutionResult> ResolveTagHelpersInProcessAsync(ProjectSnapshot project)
        {
            var templateEngine = _factory.Create(project, configure: null);

            var results = new List<TagHelperDescriptor>();
            var context = TagHelperDescriptorProviderContext.Create(results);
            context.SetCompilation(await project.WorkspaceProject.GetCompilationAsync());

            var providers = templateEngine.Engine.Features.OfType<ITagHelperDescriptorProvider>().ToArray();
            for (var i = 0; i < providers.Length; i++)
            {
                var provider = providers[i];
                provider.Execute(context);
            }

            var diagnostics = new List<RazorDiagnostic>();
            var resolutionResult = new TagHelperResolutionResult(results, diagnostics);

            return resolutionResult;
        }

        private JObject Serialize(ProjectSnapshot snapshot)
        {
            var serializer = new JsonSerializer();
            serializer.Converters.RegisterRazorConverters();

            return JObject.FromObject(snapshot, serializer);
        }

        private TagHelperResolutionResult Deserialize(JObject jsonObject)
        {
            var serializer = new JsonSerializer();
            serializer.Converters.RegisterRazorConverters();

            using (var reader = jsonObject.CreateReader())
            {
                return serializer.Deserialize<TagHelperResolutionResult>(reader);
            }
        }
    }
}
