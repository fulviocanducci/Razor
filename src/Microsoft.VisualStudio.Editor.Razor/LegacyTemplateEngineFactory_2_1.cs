// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.VisualStudio.Editor.Razor
{
    // Currently we provide a fixed configuration for 2.1, but this is a point-in-time issue. We plan
    // to make the 2.1 configuration more flexible and less hardcoded.
    [ExportCustomTemplateEngineFactory("MVC-2.1", SupportsSerialization = true)]
    internal class LegacyTemplateEngineFactory_2_1 : ITemplateEngineFactory
    {
        private const string AssemblyName = "Microsoft.AspNetCore.Mvc.Razor.Extensions";
        private const string RazorExtensionsFullTypeName = "Microsoft.AspNetCore.Mvc.Razor.Extensions.RazorExtensions";
        private const string RegisterMethodName = "Register";
        private const string MvcRazorTemplateEngineFullTypeName = "Microsoft.AspNetCore.Mvc.Razor.Extensions.MvcRazorTemplateEngine";

        public RazorTemplateEngine Create(RazorConfiguration configuration, RazorProject project, Action<IRazorEngineBuilder> configure)
        {
            var assemblyName = new AssemblyName(typeof(LegacyTemplateEngineFactory_2_1).Assembly.FullName);
            assemblyName.Name = AssemblyName;

            var assembly = Assembly.Load(assemblyName);

            var extensionType = assembly.GetType(RazorExtensionsFullTypeName, throwOnError: true);
            var registerMethod = extensionType.GetMethod("Register");

            var templateEngineType = assembly.GetType(MvcRazorTemplateEngineFullTypeName, throwOnError: true);

            var engine = RazorEngine.CreateCore(configuration, b =>
            {
                configure?.Invoke(b);

                b.Features.Add(new DefaultTagHelperDescriptorProvider() { DesignTime = true });
                
                registerMethod.Invoke(null, new object[] { b });
            });

            return (RazorTemplateEngine)Activator.CreateInstance(templateEngineType, engine, project);
        }
    }
}
