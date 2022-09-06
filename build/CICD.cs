using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Octokit;
using NukeParameter = Nuke.Common.ParameterAttribute;

namespace NukeLearningCICD;

// TODO: Add editorconfig to build project and tweak until it fits

public partial class CICD : NukeBuild
{
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
        GitHubClient = new GitHubClient(new ProductHeaderValue("NukeLearningApp"));

        return Execute<CICD>(x => x.BuildAllProjects, x => x.RunAllUnitTests);
    }

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository Repo;

    [Parameter] static GitHubClient GitHubClient;
    [Parameter(List = false)]
    static readonly Configuration Configuration = GetBuildConfig();
    [Parameter] [Secret] readonly string NugetApiKey;
    [Parameter] [Secret] readonly string TwitterConsumerKey;
    [Parameter] [Secret] readonly string TwitterConsumerSecret;
    [Parameter] [Secret] readonly string TwitterAccessToken;
    [Parameter] [Secret] readonly string TwitterAccessTokenSecret;

    // TODO: Setup the github client to use the github token.  If this is even required.  It might be built in

    static AbsolutePath TestingRootDir => RootDirectory / TestingDirName;
    static AbsolutePath MainProjPath => RootDirectory / MainProjName / MainProjFileName;
    static AbsolutePath TestProjPath => TestingRootDir / TestProjName / TestProjFileName;
    static AbsolutePath NugetOutputPath => RootDirectory / "Artifacts";

    static Configuration GetBuildConfig()
    {
        var repo = GitRepository.FromLocalDirectory(RootDirectory);

        return repo.Branch.IsMasterBranch() ? Configuration.Release : Configuration.Debug;
    }
}
