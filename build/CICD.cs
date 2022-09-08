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
        // GitHubClient = new GitHubClient(new ProductHeaderValue("NukeLearningApp"));
        //"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"
        // var credentials = new Credentials(GitHubActions.Instance.Token);
        // GitHubClient = new GitHubClient(
        //     new ProductHeaderValue(MainProjName),
        //     new InMemoryCredentialStore(credentials));

        return Execute<CICD>(x => x.BuildAllProjects, x => x.RunAllUnitTests);
    }

    GitHubActions GitHubActions => GitHubActions.Instance;
    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository Repo;

    [NukeParameter] static GitHubClient GitHubClient;
    [NukeParameter(List = false)] static readonly Configuration Configuration = GetBuildConfig();
    [NukeParameter] [Secret] readonly string GitHubToken;
    [NukeParameter] [Secret] readonly string NuGetApiKey;
    [NukeParameter] [Secret] readonly string TwitterConsumerKey;
    [NukeParameter] [Secret] readonly string TwitterConsumerSecret;
    [NukeParameter] [Secret] readonly string TwitterAccessToken;
    [NukeParameter] [Secret] readonly string TwitterAccessTokenSecret;

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
}
