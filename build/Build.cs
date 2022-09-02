using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[GitHubActions(
    "Build And Test",
    GitHubActionsImage.UbuntuLatest,
    On = new [] { GitHubActionsTrigger.Push },
    InvokedTargets = new[] { nameof(BuildStart)},
    EnableGitHubToken = true)]
public class Build : NukeBuild
{
    const string ProjFileExt = "csproj";
    const string TestProjPostfix = "Tests";
    const string RootDirName = "NukeLearning";
    const string TestingDirName = "Testing";
    const string MainProjName = "NukeLearning";
    const string MainProjFileName = $"{MainProjName}.{ProjFileExt}";
    const string TestProjName = $"{MainProjName}{TestProjPostfix}";
    const string TestProjFileName = $"{TestProjName}.{ProjFileExt}";

    public static int Main()
    {
        return Execute<Build>(x => x.BuildStart);
    }

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;

    static AbsolutePath SolutionRootDir => RootDirectory / MainProjName;

    static AbsolutePath TestingRootDir => RootDirectory / TestingDirName;

    static AbsolutePath MainProjPath => SolutionRootDir / MainProjFileName;

    static AbsolutePath TestProjPath => TestingRootDir / TestProjName / TestProjFileName;

    Target BuildStart => _ => _
        .Triggers(RestoreSolution)
        .Unlisted();

    Target RestoreSolution => _ => _
        .Before(CompileAllProjects)
        .Triggers(CompileAllProjects)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target CompileAllProjects => _ => _
        .DependsOn(RestoreSolution)
        .Triggers(CompileMainProject, CompileTestProject)
        .Unlisted();

    Target CompileMainProject => _ => _
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(MainProjPath)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target CompileTestProject => _ => _
        .Before(RunUnitTests)
        .Triggers(RunUnitTests)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(TestProjPath)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target RunUnitTests => _ => _
        .DependsOn(CompileTestProject)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(TestProjPath)
                .EnableNoRestore());
        });
}
