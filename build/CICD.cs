using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Octokit;
using Octokit.Internal;
using NukeParameter = Nuke.Common.ParameterAttribute;

namespace NukeLearningCICD;

// TODO: Add editorconfig to build project and tweak until it fits

public partial class CICD : NukeBuild
{
    const string Owner = "KinsonDigital";
    const string ProjFileExt = "csproj";
    const string MainProjName = "NukeLearning";
    const string MainProjFileName = $"{MainProjName}.{ProjFileExt}";
    const string NugetOrgSource = "https://api.nuget.org/v3/index.json";
    const string ConsoleTab = "\t       ";

    public static int Main(string[] args)
    {
        return Execute<CICD>(x => x.BuildAllProjects, x => x.RunAllUnitTests);
    }

    GitHubActions? GitHubActions => GitHubActions.Instance;
    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository Repo;

    [NukeParameter] static GitHubClient GitHubClient = GetGitHubClient();

    [NukeParameter(List = false)] static readonly Configuration Configuration = GetBuildConfig();
    [NukeParameter] [Secret] readonly string NugetOrgApiKey;
    [NukeParameter] [Secret] readonly string TwitterConsumerApiKey;
    [NukeParameter] [Secret] readonly string TwitterConsumerApiSecret;
    [NukeParameter] [Secret] readonly string TwitterAccessToken;
    [NukeParameter] [Secret] readonly string TwitterAccessTokenSecret;

    static AbsolutePath MainProjPath => RootDirectory / MainProjName / MainProjFileName;
    static AbsolutePath NugetOutputPath => RootDirectory / "Artifacts";
    static AbsolutePath PreviewReleaseNotesDirPath => RootDirectory / "Documentation" / "ReleaseNotes"  / "PreviewReleases";
    static AbsolutePath ProductionReleaseNotesDirPath => RootDirectory / "Documentation" / "ReleaseNotes"  / "ProductionReleases";

    static Configuration GetBuildConfig()
    {
        var repo = GitRepository.FromLocalDirectory(RootDirectory);

        if (IsLocalBuild)
        {
            return (repo.Branch ?? string.Empty).IsMasterBranch()
                ? Configuration.Release
                : Configuration.Debug;
        }

        if (GitHubActions.Instance is null)
        {
            return (repo.Branch ?? string.Empty).IsMasterBranch()
                ? Configuration.Release
                : Configuration.Debug;
        }

        if (GitHubActions.Instance.IsPullRequest)
        {
            return (GitHubActions.Instance?.BaseRef  ?? string.Empty).IsMasterBranch()
                ? Configuration.Release
                : Configuration.Debug;
        }

        return (repo.Branch ?? string.Empty).IsMasterBranch()
            ? Configuration.Release
            : Configuration.Debug;
    }

    static string GetGitHubToken()
    {
        if (NukeBuild.IsServerBuild)
        {
            return GitHubActions.Instance.Token;
        }

        return "";
    }

    static GitHubClient GetGitHubClient()
    {
        var token = GetGitHubToken();
        GitHubClient client;

        if (NukeBuild.IsServerBuild)
        {
            client = new GitHubClient(new ProductHeaderValue(MainProjName),
                new InMemoryCredentialStore(new Credentials(token)));
        }
        else
        {
            if (string.IsNullOrEmpty(token))
            {
                // TODO: This needs to possibly utilize the SWCM app/lib project to get the token from windows credentials.
                // Or something similar
                client = new GitHubClient(new ProductHeaderValue(MainProjName));
            }
            else
            {
                client = new GitHubClient(new ProductHeaderValue(MainProjName),
                new InMemoryCredentialStore(new Credentials(token)));
            }
        }

        return client;
    }
}
