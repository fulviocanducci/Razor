// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.Editor.Razor
{
    [ExportLanguageServiceFactory(typeof(RazorTemplateEngineFactoryService), RazorLanguage.Name, ServiceLayer.Default)]
    internal class DefaultTemplateEngineFactoryServiceFactory : ILanguageServiceFactory
    {
        private readonly Lazy<ITemplateEngineFactory, ICustomTemplateEngineFactoryMetadata>[] _customFactories;
        private readonly IFallbackTemplateEngineFactory _fallbackFactory;

        [ImportingConstructor]
        public DefaultTemplateEngineFactoryServiceFactory(
            IFallbackTemplateEngineFactory fallbackFactory,
            [ImportMany] IEnumerable<Lazy<ITemplateEngineFactory, ICustomTemplateEngineFactoryMetadata>> customFactories)
        {
            if (fallbackFactory == null)
            {
                throw new ArgumentNullException(nameof(fallbackFactory));
            }

            if (customFactories == null)
            {
                throw new ArgumentNullException(nameof(customFactories));
            }

            _fallbackFactory = fallbackFactory;
            _customFactories = customFactories.ToArray();
        }

        public ILanguageService CreateLanguageService(HostLanguageServices languageServices)
        {
            return new DefaultTemplateEngineFactoryService(
                languageServices.GetRequiredService<ProjectSnapshotManager>(),
                _fallbackFactory,
                _customFactories);
        }
    }
}