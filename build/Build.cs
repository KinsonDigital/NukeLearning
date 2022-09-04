using DefaultNamespace;
using GlobExpressions;
using Microsoft.Build.Tasks;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitHub;
using Octokit;
using Octokit.Internal;
using Serilog;
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
        var credentials = new Credentials("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");
        GitHubTasks.GitHubClient = new GitHubClient(
            new ProductHeaderValue(nameof(NukeBuild)),
            new InMemoryCredentialStore(credentials));

        return Execute<Build>(x => x.BuildStart);
    }

    [Nuke.Common.Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;

    static AbsolutePath SolutionRootDir => RootDirectory / MainProjName;
    static AbsolutePath TestingRootDir => RootDirectory / TestingDirName;
    static AbsolutePath MainProjPath => SolutionRootDir / MainProjFileName;
    static AbsolutePath TestProjPath => TestingRootDir / TestProjName / TestProjFileName;

    Target BuildStart => _ => _
        .Triggers(RestoreSolution)
        .Unlisted()
        .Executes(() =>
        {

        });

    Target RestoreSolution => _ => _
        .After(BuildStatusCheck, UnitTestStatusCheck)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target BuildAllProjects => _ => _
        .DependsOn(RestoreSolution)
        .Triggers(BuildMainProject, BuildTestProject);

    Target RunAllUnitTests => _ => _
        .DependsOn(RestoreSolution)
        .Triggers(RunUnitTests);

    Target BuildMainProject => _ => _
        .Before(RunUnitTests)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(MainProjPath)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target BuildTestProject => _ => _
        .Before(RunUnitTests)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(TestProjPath)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target RunUnitTests => _ => _
        .DependsOn(RestoreSolution)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(TestProjPath)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target BuildStatusCheck => _ => _
        .Triggers(BuildAllProjects)
        .Executes(() =>
        {
            Log.Information("✅Build Status Check - Executing {Value} Target", nameof(BuildAllProjects));
        });

    Target UnitTestStatusCheck => _ => _
        .Triggers(RunAllUnitTests)
        .Executes(() =>
        {
            Log.Information("✅Unit Test Status Check - Executing {Value} Target", nameof(RunAllUnitTests));
        });

    Target CheckTag => _ => _
        .Executes(async () =>
        {

            var repoClient = GitHubTasks.GitHubClient.Repository;
            var tags = await repoClient.GetAllTags("KinsonDigital", "NukeLearning");

            foreach (var tag in tags)
            {
                Log.Information("Tags: {Value}", tag.Name);
            }
        });
}
