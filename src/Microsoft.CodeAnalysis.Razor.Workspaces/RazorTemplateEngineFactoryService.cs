// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor
{
    internal abstract class RazorTemplateEngineFactoryService : ILanguageService
    {
        public abstract ITemplateEngineFactory FindSerializableFactory(ProjectSnapshot project);

        public abstract RazorTemplateEngine Create(ProjectSnapshot project, Action<IRazorEngineBuilder> configure);

        public abstract RazorTemplateEngine Create(string directoryPath, Action<IRazorEngineBuilder> configure);
    }
}