// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.Editor.Razor
{
    internal class DefaultTemplateEngineFactoryService : RazorTemplateEngineFactoryService
    {
        private readonly static RazorConfiguration DefaultConfiguration = FallbackRazorConfiguration.MVC_2_0;

        private readonly ProjectSnapshotManager _projectManager;
        private readonly IFallbackTemplateEngineFactory _defaultFactory;
        private readonly Lazy<ITemplateEngineFactory, ICustomTemplateEngineFactoryMetadata>[] _customFactories;

        public DefaultTemplateEngineFactoryService(
           ProjectSnapshotManager projectManager,
           IFallbackTemplateEngineFactory defaultFactory,
           Lazy<ITemplateEngineFactory, ICustomTemplateEngineFactoryMetadata>[] customFactories)
        {
            if (projectManager == null)
            {
                throw new ArgumentNullException(nameof(projectManager));
            }

            if (defaultFactory == null)
            {
                throw new ArgumentNullException(nameof(defaultFactory));
            }

            if (customFactories == null)
            {
                throw new ArgumentNullException(nameof(customFactories));
            }

            _projectManager = projectManager;
            _defaultFactory = defaultFactory;
            _customFactories = customFactories;
        }

        public override ITemplateEngineFactory FindSerializableFactory(ProjectSnapshot project)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            return SelectFactory(project.Configuration ?? DefaultConfiguration, requireSerializable: true);
        }

        public override RazorTemplateEngine Create(ProjectSnapshot project, Action<IRazorEngineBuilder> configure)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            return Create(Path.GetDirectoryName(project.FilePath), project.Configuration, configure);
        }

        public override RazorTemplateEngine Create(string directoryPath, Action<IRazorEngineBuilder> configure)
        {
            if (directoryPath == null)
            {
                throw new ArgumentNullException(nameof(directoryPath));
            }

            var project = FindProject(directoryPath);
            return Create(directoryPath, project?.Configuration, configure);
        }

        private RazorTemplateEngine Create(string directoryPath, RazorConfiguration configuration, Action<IRazorEngineBuilder> configure)
        {
            // When we're running in the editor, the editor provides a configure delegate that will include
            // the editor settings and tag helpers.
            // 
            // This service is only used in process in Visual Studio, and any other callers should provide these
            // things also.
            configure = configure ?? ((b) => { });

            // The default configuration currently matches MVC-2.0. Beyond MVC-2.0 we added SDK support for 
            // properly detecting project versions, so that's a good version to assume when we can't find a
            // configuration.
            configuration = configuration ?? DefaultConfiguration;

            // If there's no factory to handle the configuration then fall back to a very basic configuration.
            //
            // This will stop a crash from happening in this case (misconfigured project), but will still make
            // it obvious to the user that something is wrong.
            var factory = SelectFactory(configuration) ?? _defaultFactory;
            return factory.Create(configuration, RazorProject.Create(directoryPath), configure);
        }

        private ITemplateEngineFactory SelectFactory(RazorConfiguration configuration, bool requireSerializable = false)
        {
            for (var i = 0; i < _customFactories.Length; i++)
            {
                var factory = _customFactories[i];
                if (string.Equals(configuration.ConfigurationName, factory.Metadata.ConfigurationName))
                {
                    return requireSerializable && !factory.Metadata.SupportsSerialization ? null : factory.Value;
                }
            }

            return null;
        }

        private ProjectSnapshot FindProject(string directory)
        {
            directory = NormalizeDirectoryPath(directory);

            var projects = _projectManager.Projects;
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project.FilePath != null)
                {
                    if (string.Equals(directory, NormalizeDirectoryPath(Path.GetDirectoryName(project.FilePath)), StringComparison.OrdinalIgnoreCase))
                    {
                        return project;
                    }
                }
            }

            return null;
        }

        private string NormalizeDirectoryPath(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
