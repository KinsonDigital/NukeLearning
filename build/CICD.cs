using System;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.GitHub;
using Octokit;
using Octokit.Internal;
using Serilog;
using NukeParameter = Nuke.Common.ParameterAttribute;

namespace NukeLearningCICD;

// TODO: Add editorconfig to build project and tweak until it fits

public partial class CICD : NukeBuild
{
    const string Owner = "KinsonDigital";
    const string ProjFileExt = "csproj";
    const string TestProjPostfix = "Tests";
    const string TestingDirName = "Testing";
    const string MainProjName = "NukeLearning";
    const string MainProjFileName = $"{MainProjName}.{ProjFileExt}";
    const string TestProjName = $"{MainProjName}{TestProjPostfix}";
    const string TestProjFileName = $"{TestProjName}.{ProjFileExt}";
    const string NugetOrgSource = "https://api.nuget.org/v3/index.json";

    public static int Main()
    {
        return Execute<CICD>(x => x.BuildAllProjects, x => x.RunAllUnitTests);
    }

    GitHubActions GitHubActions => GitHubActions.Instance;
    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository Repo;

    [NukeParameter] static GitHubClient GitHubClient = GetGitHubClient();

    [NukeParameter(List = false)] static readonly Configuration Configuration = GetBuildConfig();
    [NukeParameter] [Secret] readonly string GitHubToken = GetGitHubToken();
    [NukeParameter] [Secret] readonly string NuGetApiKey;
    [NukeParameter] [Secret] readonly string TwitterConsumerKey;
    [NukeParameter] [Secret] readonly string TwitterConsumerSecret;
    [NukeParameter] [Secret] readonly string TwitterAccessToken;
    [NukeParameter] [Secret] readonly string TwitterAccessTokenSecret;
    [NukeParameter] readonly string PullRequestType;

    // TODO: Setup the github client to use the github token.  If this is even required.  It might be built in

    static AbsolutePath TestingRootDir => RootDirectory / TestingDirName;
    static AbsolutePath MainProjPath => RootDirectory / MainProjName / MainProjFileName;
    static AbsolutePath TestProjPath => TestingRootDir / TestProjName / TestProjFileName;
    static AbsolutePath NugetOutputPath => RootDirectory / "Artifacts";
    static AbsolutePath PreviewReleaseNotesDirPath => RootDirectory / "Documentation" / "ReleaseNotes"  / "PreviewReleases";
    static AbsolutePath ProductionReleaseNotesDirPath => RootDirectory / "Documentation" / "ReleaseNotes"  / "ProductionReleases";

    static Configuration GetBuildConfig()
    {
        var repo = GitRepository.FromLocalDirectory(RootDirectory);

        return (repo.Branch ?? string.Empty).IsMasterBranch() ? Configuration.Release : Configuration.Debug;
    }

    static string GetGitHubToken()
    {
        if (NukeBuild.IsServerBuild)
        {
            return GitHubActions.Instance.Token;
        }

        return "local-fake-token";
    }

    static GitHubClient GetGitHubClient()
    {
        var token = string.Empty;
        GitHubClient client;

        if (NukeBuild.IsServerBuild)
        {
            client = new GitHubClient(new ProductHeaderValue(MainProjName),
                new InMemoryCredentialStore(new Credentials(GitHubActions.Instance.Token)));
        }
        else
        {
            client = new GitHubClient(new ProductHeaderValue(MainProjName));
        }

        return client;
    }
}
