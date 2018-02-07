﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Editor.Razor;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    public class DefaultTagHelperResolverTest
    {
        public DefaultTagHelperResolverTest()
        {
            HostProject_For_2_0 = new HostProject("Test.csproj", FallbackRazorConfiguration.MVC_2_0);
            HostProject_For_NonSerializableConfiguration = new HostProject(
                "Test.csproj",
                new ProjectSystemRazorConfiguration(RazorLanguageVersion.Version_2_1, "Blazor-0.1", Array.Empty<RazorExtension>()));

            CustomFactories = new Lazy<ITemplateEngineFactory, ICustomTemplateEngineFactoryMetadata>[]
            {
                new Lazy<ITemplateEngineFactory, ICustomTemplateEngineFactoryMetadata>(
                    () => new LegacyTemplateEngineFactory_2_0(),
                    typeof(LegacyTemplateEngineFactory_2_0).GetCustomAttribute<ExportCustomTemplateEngineFactoryAttribute>()),

                // We don't really use this factory, we just use it to ensure that the call is going to go out of process.
                new Lazy<ITemplateEngineFactory, ICustomTemplateEngineFactoryMetadata>(
                    () => new LegacyTemplateEngineFactory_2_1(),
                    new ExportCustomTemplateEngineFactoryAttribute("Blazor-0.1") { SupportsSerialization = false, }),
            };

            FallbackFactory = new FallbackTemplateEngineFactory();

            Workspace = new AdhocWorkspace();

            var info = ProjectInfo.Create(ProjectId.CreateNewId("Test"), VersionStamp.Default, "Test", "Test", LanguageNames.CSharp, filePath: "Test.csproj");
            WorkspaceProject = Workspace.CurrentSolution.AddProject(info).GetProject(info.Id);

            ErrorReporter = new DefaultErrorReporter();
            ProjectManager = new TestProjectSnapshotManager(Workspace);
            Factory = new DefaultTemplateEngineFactoryService(ProjectManager, FallbackFactory, CustomFactories);
        }

        private ErrorReporter ErrorReporter { get; }

        private RazorTemplateEngineFactoryService Factory { get; }

        private Lazy<ITemplateEngineFactory, ICustomTemplateEngineFactoryMetadata>[] CustomFactories { get; }

        private IFallbackTemplateEngineFactory FallbackFactory { get; }

        private HostProject HostProject_For_2_0 { get; }

        private HostProject HostProject_For_NonSerializableConfiguration { get; }

        private ProjectSnapshotManagerBase ProjectManager { get; }

        private Project WorkspaceProject { get; }

        private Workspace Workspace { get; }

        [Fact]
        public async Task GetTagHelpersAsync_WithNonInitializedProject_Noops()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject_For_2_0);

            var project = ProjectManager.GetProjectWithFilePath("Test.csproj");

            var resolver = new TestTagHelperResolver(ErrorReporter, Workspace, Factory);

            var result = await resolver.GetTagHelpersAsync(project);

            // Assert
            Assert.Same(TagHelperResolutionResult.Empty, result);
        }

        [Fact]
        public async Task GetTagHelpersAsync_WithSerializableCustomFactory_GoesOutOfProcess()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject_For_2_0);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);

            var project = ProjectManager.GetProjectWithFilePath("Test.csproj");

            var resolver = new TestTagHelperResolver(ErrorReporter, Workspace, Factory)
            {
                OnResolveOutOfProcess = (f, p) =>
                {
                    Assert.Same(CustomFactories[0].Value, f);
                    Assert.Same(project, p);

                    return Task.FromResult(TagHelperResolutionResult.Empty);
                },
            };

            var result = await resolver.GetTagHelpersAsync(project);

            // Assert
            Assert.Same(TagHelperResolutionResult.Empty, result);      
        }

        [Fact]
        public async Task GetTagHelpersAsync_WithNonSerializableCustomFactory_StaysInProcess()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject_For_NonSerializableConfiguration);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);

            var project = ProjectManager.GetProjectWithFilePath("Test.csproj");

            var resolver = new TestTagHelperResolver(ErrorReporter, Workspace, Factory)
            {
                OnResolveInProcess = (p) =>
                {
                    Assert.Same(project, p);

                    return Task.FromResult(TagHelperResolutionResult.Empty);
                },
            };

            var result = await resolver.GetTagHelpersAsync(project);

            // Assert
            Assert.Same(TagHelperResolutionResult.Empty, result);

        }

        private class TestTagHelperResolver : DefaultTagHelperResolver
        {
            public TestTagHelperResolver(ErrorReporter errorReporter, Workspace workspace, RazorTemplateEngineFactoryService factory) 
                : base(errorReporter, workspace, factory)
            {
            }

            public Func<ITemplateEngineFactory, ProjectSnapshot, Task<TagHelperResolutionResult>> OnResolveOutOfProcess { get; set; }

            public Func<ProjectSnapshot, Task<TagHelperResolutionResult>> OnResolveInProcess { get; set; }

            protected override Task<TagHelperResolutionResult> ResolveTagHelpersOutOfProcessAsync(ITemplateEngineFactory factory, ProjectSnapshot project)
            {
                Assert.NotNull(OnResolveOutOfProcess);
                return OnResolveOutOfProcess(factory, project);
            }

            protected override Task<TagHelperResolutionResult> ResolveTagHelpersInProcessAsync(ProjectSnapshot project)
            {
                Assert.NotNull(OnResolveInProcess);
                return OnResolveInProcess(project);
            }
        }
        private class TestProjectSnapshotManager : DefaultProjectSnapshotManager
        {
            public TestProjectSnapshotManager(Workspace workspace)
                : base(
                      Mock.Of<ForegroundDispatcher>(),
                      Mock.Of<ErrorReporter>(),
                      Mock.Of<ProjectSnapshotWorker>(),
                      Enumerable.Empty<ProjectSnapshotChangeTrigger>(),
                      workspace)
            {
            }
        }

    }
}
