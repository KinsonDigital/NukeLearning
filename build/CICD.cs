using System;
using System.IO;
using System.Reflection;
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

namespace NukeLearningCICD;

// TODO: Add editorconfig to build project and tweak until it fits

// [GitHubActions(
//     "Build And Test",
//     GitHubActionsImage.UbuntuLatest,
//     On = new [] { GitHubActionsTrigger.Push },
//     InvokedTargets = new[] { nameof(Start)},
//     EnableGitHubToken = true)]
public partial class CICD : NukeBuild
{
    const string ProjFileExt = "csproj";
    const string TestProjPostfix = "Tests";
    const string TestingDirName = "Testing";
    const string MainProjName = "NukeLearning";
    const string MainProjFileName = $"{MainProjName}.{ProjFileExt}";
    const string TestProjName = $"{MainProjName}{TestProjPostfix}";
    const string TestProjFileName = $"{TestProjName}.{ProjFileExt}";

    public static int Main()
    {
        var credentials = new Credentials("fake-token");
        GitHubTasks.GitHubClient = new GitHubClient(
            new ProductHeaderValue(nameof(NukeBuild)),
            new InMemoryCredentialStore(credentials));

        return Execute<CICD>(x => x.Start);
    }

    /* TODO: This cannot always be release if it is not a local build
        The only time a release build will exist is if it is being ran on the server and it is the production branch.
     */
    [Nuke.Common.Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    readonly IRepositoriesClient RepoClient = GitHubTasks.GitHubClient.Repository;
    [GitRepository] readonly GitRepository Repo;
    [Solution] readonly Solution Solution;

    static AbsolutePath SolutionRootDir => RootDirectory / MainProjName;
    static AbsolutePath TestingRootDir => RootDirectory / TestingDirName;
    static AbsolutePath MainProjPath => SolutionRootDir / MainProjFileName;
    static AbsolutePath TestProjPath => TestingRootDir / TestProjName / TestProjFileName;

    Target Start => _ => _
        .OnlyWhenStatic(() => !string.IsNullOrEmpty(Repo.Branch))
        .Executes(() =>
        {
            Log.Information("GitHub Owner: {Value}", Repo.GetGitHubOwner());
            Log.Information("GitHub Name: {Value}", Repo.GetGitHubName());
            Log.Information("Is Preview Feature Branch: {Value}", Repo.IsOnPreviewFeatureBranch());
            Log.Information("Is Release Branch: {Value}", Repo.IsOnReleaseBranch());
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
