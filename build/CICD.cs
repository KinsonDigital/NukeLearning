using System;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using NukeLearningCICD.Services;
using Octokit;
using Octokit.Internal;
using Serilog;
using NukeParameter = Nuke.Common.ParameterAttribute;

namespace NukeLearningCICD;

// TODO: Add editorconfig to build project and tweak until it fits

public partial class CICD : NukeBuild
{
    const string ProjFileExt = "csproj";
    const string NugetOrgSource = "https://api.nuget.org/v3/index.json";
    const string ConsoleTab = "\t       ";
    private static BuildSettings buildSettings;

    public static int Main(string[] args)
    {
        // If the generate settings file command was invoked
        if (args.Length > 0 && args[0] == nameof(GenerateSettingsFile))
        {
            return Execute<CICD>(x => x.DebugTask);
        }

        var buildSettingsService = new BuildSettingsService();

        var loadResult = buildSettingsService.LoadBuildSettings();

        if (loadResult.loadSuccessful)
        {
            buildSettings = loadResult.settings ??
                    throw new Exception("The build settings are null.  Build canceled!!");

            Owner = buildSettings.Owner ?? string.Empty;
            MainProjName = buildSettings.MainProjectName ?? string.Empty;

            // Make sure mandatory settings are not null or empty
            if (string.IsNullOrEmpty(Owner) || string.IsNullOrEmpty(MainProjName))
            {
                const string mandatorySettingsErrorMsg = $"The '{nameof(BuildSettings.Owner)}' and '{nameof(BuildSettings.MainProjectName)}' settings must not be null or empty.";
                Log.Error(mandatorySettingsErrorMsg);
                return -1;
            }

            MainProjFileName = string.IsNullOrEmpty(buildSettings.MainProjectFileName)
                ? MainProjFileName
                : buildSettings.MainProjectFileName;
            DocumentationDirName = string.IsNullOrEmpty(buildSettings.DocumentationDirName)
                ? DocumentationDirName
                : buildSettings.DocumentationDirName;
            ReleaseNotesDirName = string.IsNullOrEmpty(buildSettings.ReleaseNotesDirName)
                ? ReleaseNotesDirName
                : buildSettings.ReleaseNotesDirName;
            AnnounceOnTwitter = buildSettings.AnnounceOnTwitter;

            GitHubClient = GetGitHubClient();
        }
        else
        {
            var loadErrorMsg = loadResult.errorMsg;
            loadErrorMsg += $"{Environment.NewLine}{ConsoleTab}To create an empty build settings file, run the '{nameof(GenerateSettingsFile)}' command";
            Log.Error(loadErrorMsg);
            return -1;
        }

        return Execute<CICD>(x => x.BuildAllProjects, x => x.RunAllUnitTests);
    }

    GitHubActions? GitHubActions => GitHubActions.Instance;
    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository Repo;

    [NukeParameter] static GitHubClient GitHubClient;

    [NukeParameter(List = false)] static readonly Configuration Configuration = GetBuildConfig();
    [NukeParameter] [Secret] readonly string NugetOrgApiKey;
    [NukeParameter] [Secret] string TwitterConsumerApiKey { get; set; }
    [NukeParameter] [Secret] string TwitterConsumerApiSecret { get; set; }
    [NukeParameter] [Secret] string TwitterAccessToken { get; set; }
    [NukeParameter] [Secret] string TwitterAccessTokenSecret { get; set; }

    static string Owner = string.Empty;
    static string MainProjName = string.Empty;
    static string MainProjFileName = $"{MainProjName}.{ProjFileExt}";
    static string DocumentationDirName = "Documentation";
    static string ReleaseNotesDirName = "ReleaseNotes";

    static AbsolutePath DocumentationPath => RootDirectory / DocumentationDirName;
    static AbsolutePath ReleaseNotesBaseDirPath => DocumentationPath / ReleaseNotesDirName;
    static AbsolutePath MainProjPath => RootDirectory / MainProjName / MainProjFileName;
    static AbsolutePath NugetOutputPath => RootDirectory / "Artifacts";
    static AbsolutePath PreviewReleaseNotesDirPath => ReleaseNotesBaseDirPath / "PreviewReleases";
    static AbsolutePath ProductionReleaseNotesDirPath => ReleaseNotesBaseDirPath / "ProductionReleases";

    static bool AnnounceOnTwitter
    {
        get => buildSettings.AnnounceOnTwitter;
        set => buildSettings.AnnounceOnTwitter = value;
    }

    static Configuration GetBuildConfig()
    {
        var repo = GitRepository.FromLocalDirectory(RootDirectory);

        if (IsLocalBuild || GitHubActions.Instance is null)
        {
            return (repo.Branch ?? string.Empty).IsMasterBranch()
                ? Configuration.Release
                : Configuration.Debug;
        }

        return (GitHubActions.Instance?.BaseRef  ?? string.Empty).IsMasterBranch()
            ? Configuration.Release
            : Configuration.Debug;
    }

    static string GetGitHubToken()
    {
        if (NukeBuild.IsServerBuild)
        {
            return GitHubActions.Instance.Token;
        }

        var localSecretService = new LoadSecretsService();

        const string tokenName = "GitHubApiToken";
        var token = localSecretService.LoadSecret(tokenName);

        if (string.IsNullOrEmpty(token))
        {
            throw new Exception($"The GitHub API token with the name '{tokenName}' could not be loaded.");
        }

        return token;
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
