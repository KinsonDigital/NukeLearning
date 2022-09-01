using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NuGet.Configuration;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Nuke.Common.Utilities.Collections;
using Serilog;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;

[GitHubActions(
    "Build And Test",
    GitHubActionsImage.UbuntuLatest,
    On = new [] { GitHubActionsTrigger.Push },
    InvokedTargets = new[] { nameof(CompileMainProject)},
    EnableGitHubToken = true)]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main()
    {
        return Execute<Build>(x => x.CompileMainProject);
    }

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;
    [PathExecutable] readonly Tool Git;

    Target SetUbuntuPermissions => _ => _
        .Executes(() =>
        {
            Git("update-index --chmod=+x build.cmd");
        });

    Target RestoreMainProject => _ => _
        .DependsOn(SetUbuntuPermissions)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target RestoreTestProject => _ => _
        .DependsOn(SetUbuntuPermissions)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target CompileMainProject => _ => _
        .DependsOn(RestoreMainProject)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration));
        });

    Target CompileTestProject => _ => _
        .DependsOn(RestoreTestProject)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution.Directory / "Testing" / "NukeLearningTests")
                .SetConfiguration(Configuration));
        });

    Target RunUnitTests => _ => _
        .DependsOn(CompileTestProject)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(Solution.Directory / "Testing" / "NukeLearningTests"));
        });
}
