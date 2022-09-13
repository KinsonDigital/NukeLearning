using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlobExpressions;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Octokit;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Twitter.TwitterTasks;

namespace NukeLearningCICD;

public partial class CICD // Common
{
    Target RestoreSolution => _ => _
        .After(BuildStatusCheck, UnitTestStatusCheck)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });


    Target CreateNugetPackage => _ => _
        .DependsOn(RestoreSolution)
        .After(RestoreSolution, BuildTestProject, RunUnitTests)
        .Executes(() =>
        {
            DeleteNugetPackageIfExists();

            DotNetPack(s => s
                .SetConfiguration(Configuration)
                .SetProject(MainProjPath)
                .SetOutputDirectory(NugetOutputPath)
                .EnableNoRestore());
        });


    Target PublishNugetPackage => _ => _
        .DependsOn(RunAllUnitTests, CreateNugetPackage)
        .Requires(() => NuGetApiKey)
        .Requires(() => IsServerBuild) // Prevent accidental push attempt from local machine
        .Executes(() =>
        {
            var packages = Glob.Files(NugetOutputPath, "*.nupkg").ToArray();

            if (packages.Length <= 0)
            {
                Assert.Fail($"Could not find a nuget package in path '{NugetOutputPath}' to publish to nuget.org");
            }

            var fullPackagePath = $"{NugetOutputPath}/{packages[0]}";

            DotNetNuGetPush(s => s
                .SetTargetPath(fullPackagePath)
                .SetSource(NugetOrgSource)
                .SetApiKey(NuGetApiKey));
        });


    Target SendTweetAnnouncement => _ => _
        .Requires(() => IsServerBuild)
        .Requires(() => TwitterConsumerKey)
        .Requires(() => TwitterConsumerSecret)
        .Requires(() => TwitterAccessToken)
        .Requires(() => TwitterAccessTokenSecret)
        .Executes(async () =>
        {
            // Validate that the keys, tokens, and secrets are not null or empty
            await SendTweetAsync(
                message: "Hello from NUKE",
                TwitterConsumerKey,
                TwitterConsumerSecret,
                TwitterAccessToken,
                TwitterAccessTokenSecret);
        });


    Target CreateGitHubRelease => _ => _
        .DependsOn(RunAllUnitTests)
        .After(RunAllUnitTests, RunUnitTests)
        .Executes(() =>
        {

        });

    bool IsPullRequest()
    {
        return GitHubActions.Instance is not null && GitHubActions.Instance.IsPullRequest;
    }

    bool ThatTheWorkflowRunIsForAPullRequest(string runName, RunType runType)
    {
        var isPullRequest = IsPullRequest();

        Log.Information("Checking if run is a pull request run.");
        if (isPullRequest)
        {
            Log.Information($"{ConsoleTab}✅Valid run executed for '{runType}'");
        }
        else
        {
            var errorMsg = runType switch
            {
                RunType.StatusCheck => $"Running '{runName}' can only be done with status checks.",
                RunType.Release => $"Running '{runName}' can only be done with releases.",
                _ => throw new ArgumentOutOfRangeException("")
            };

            Log.Error(errorMsg);
            Assert.Fail($"{ConsoleTab}Not executed from a pull request.");
        }

        return true;
    }

    bool ReleaseNotesExist(ReleaseType releaseType, string version)
    {
        var releaseNotesDirPath = releaseType switch
        {
            ReleaseType.Production => ProductionReleaseNotesDirPath,
            ReleaseType.Preview => PreviewReleaseNotesDirPath,
            _ => throw new ArgumentOutOfRangeException(nameof(releaseType), releaseType, null)
        };

        Log.Information($"Checking if the '{releaseType}' release notes exist.");

        return (from f in Glob.Files(releaseNotesDirPath, "*.md")
            where f.Contains(version)
            select f).Any();
    }

    bool ReleaseNotesDoNotExist(ReleaseType releaseType, string version)
        => !ReleaseNotesExist(releaseType, version);

    void PrintPullRequestInfo()
    {
        // If the build is on the server and the GitHubActions object exists
        if (IsServerBuild && GitHubActions.Instance is not null)
        {
            var gitHub = GitHubActions.Instance;

            Log.Information("Is Server Build: {Value}", IsServerBuild);
            Log.Information("Repository Owner: {Value}", gitHub.RepositoryOwner);
            Log.Information("Status Check Invoked By: {Value}", gitHub.Actor);
            Log.Information("Is Local Build: {Value}", IsLocalBuild);
            Log.Information("Is PR: {Value}", IsPullRequest());
            Log.Information("Ref: {Value}", gitHub.Ref);
            Log.Information("Source Branch: {Value}", gitHub.HeadRef);
            Log.Information("Destination Branch: {Value}", gitHub.BaseRef);
        }
        else
        {
            Log.Information("Local Build");
        }
    }

    bool ProjVersionExists()
    {
        var project = Solution.GetProject(MainProjName);

        return !string.IsNullOrEmpty(project.GetProperty("Version"));
    }

    bool ProjFileVersionExists()
    {
        var project = Solution.GetProject(MainProjName);

        return !string.IsNullOrEmpty(project.GetProperty("FileVersion"));
    }

    bool ProjAssemblyVersionExists()
    {
        var project = Solution.GetProject(MainProjName);

        return !string.IsNullOrEmpty(project.GetProperty("AssemblyVersion"));
    }

    void ValidateVersions(string versionPattern)
    {
        var project = Solution.GetProject(MainProjName);

        Log.Information("✔️ Validating that version exists in csproj file . . .");
        if (project.AllVersionsExist() is false)
        {
            var failMsg = new StringBuilder();
            failMsg.AppendLine("All of the following versions must exist in the csproj file.");
            failMsg.AppendLine("\t- <Version/>");
            failMsg.AppendLine("\t- <FileVersion/>");
            failMsg.AppendLine("\t- <AssemblyVersion/>");

            Log.Error(failMsg.ToString());

            Assert.Fail("Project file missing version.");
        }

        Log.Information("✔️ Validating that the version syntax is valid . . .");
        var correctVersionSyntax = project.HasCorrectVersionSyntax(versionPattern);

        if (correctVersionSyntax is false)
        {
            var failMsg = $"The syntax for the '<Version/>' value '{project.GetVersion()}' is incorrect.";
            failMsg += $"{Environment.NewLine}Expected Syntax: '{versionPattern}'";

            Log.Error(failMsg);
            Assert.Fail("The csproj file '<Version/>' value syntax is incorrect.");
        }

        Log.Information("✔️ Validating that the file version syntax is valid . . .");
        var correctFileVersionSyntax = project.HasCorrectFileVersionSyntax(versionPattern);
        if (correctFileVersionSyntax is false)
        {
            var failMsg = $"The syntax for the '<FileVersion/>' value '{project.GetFileVersion()}' is incorrect.";
            failMsg += $"{Environment.NewLine}Expected Syntax: '{versionPattern}'";

            Log.Error(failMsg);
            Assert.Fail("The csproj file '<FileVersion/>' value syntax is incorrect.");
        }

        Log.Information("✔️ Validating that the assembly version syntax is valid . . .");
        var correctAssemblyVersionSyntax = project.HasCorrectAssemblyVersionSyntax("#.#.#");
        if (correctAssemblyVersionSyntax is false)
        {
            var failMsg = $"The syntax for the '<AssemblyVersion/>' value '{project.GetAssemblyVersion()}' is incorrect.";
            failMsg += $"{Environment.NewLine}Expected Syntax: '{versionPattern}'";

            Log.Error(failMsg);
            Assert.Fail("The csproj file '<AssemblyVersion/>' value syntax is incorrect.");
        }
    }

    void DeleteNugetPackageIfExists()
    {
        // If the build is local, find and delete the package first if it exists.
        // This is to essentially overwrite the package so it is "updated".
        // Without doing this, the package already exists but does not get overwritten to be "updated"
        if (IsLocalBuild)
        {
            var project = Solution.GetProject(MainProjName);
            var version = project.GetVersion();

            var packageFileName = $"{MainProjName}.{version}.nupkg";
            var packageFilePath = $"{NugetOutputPath}/{packageFileName}";

            if (File.Exists(packageFilePath))
            {
                File.Delete(packageFilePath);
            }
        }
    }

    string GetTargetBranch()
    {
        if (IsServerBuild && GitHubActions.Instance is not null)
        {
            if (IsPullRequest())
            {
                return GitHubActions.Instance.BaseRef;
            }

            if (string.IsNullOrEmpty(Repo.Branch))
            {
                Assert.Fail("Cannot get branch.  Branch name is null or empty.  Maybe GIT is in the state of a detached head?");
            }
        }

        if ((IsLocalBuild || GitHubActions.Instance is null) && (Repo.Branch ?? string.Empty).IsNotNullOrEmpty())
        {
            return Repo.Branch!;
        }

        Assert.Fail("Could not get the correct branch.");
        throw new Exception();
    }

    async Task CreateNewGitHubRelease(ReleaseType releaseType)
    {
        try
        {
            var project = Solution.GetProject(MainProjName);
            var releaseClient = GitHubClient.Repository.Release;

            var version = $"v{project.GetVersion()}";
            var releaseNotesFilePath = Solution.GetReleaseNotesFilePath(releaseType, version);
            var releaseNotes = Solution.GetReleaseNotes(releaseType, version);

            var newRelease = new NewRelease(version)
            {
                Name = $"🚀{releaseType} Release - {version}",
                Body = releaseNotes,
                Prerelease = releaseType == ReleaseType.Preview,
                TargetCommitish = "8242f49b45ed246d99fa4fc74c661e7b0c9ebdae",
            };

            var releaseResult = await releaseClient.Create(Owner, MainProjName, newRelease);

            await releaseClient.UploadTextFileAsset(releaseResult, releaseNotesFilePath);
        }
        catch (Exception e)
        {
            Assert.Fail(e.Message);
        }
    }

    int ExtractIssueNumber(BranchType branchType, string branch)
    {
        var isNotIssueBranch = branchType != BranchType.Feature &&
                               branchType != BranchType.PreviewFeature &&
                               branchType != BranchType.HotFix;

        if (isNotIssueBranch || branch.DoesNotContainNumbers())
        {
            return -1; // All of the other branches do not contain an issue number
        }

        var separator = branchType switch
        {
            BranchType.Feature => "feature/",
            BranchType.PreviewFeature => "preview/feature/",
            BranchType.HotFix => "hotfix/",
            _ => throw new ArgumentOutOfRangeException(nameof(branchType), "Not a valid issue type branch")
        };

        var sections = branch.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        var issueNumStr = sections[0].Split('-')[0];

        var parseResult = int.TryParse(issueNumStr, out var issueNum);

        if (parseResult)
        {
            return issueNum;
        }

        return -1;
    }

    async Task<(bool isValid, int issueNum)> BranchIssueNumberValid(BranchType branchType)
    {
        var sourceBranch = GitHubActions.Instance?.HeadRef ?? string.Empty;
        var issueClient = GitHubClient.Issue;
        var issueNumber = ExtractIssueNumber(branchType, sourceBranch);

        return (await issueClient.IssueExists(Owner, MainProjName, issueNumber), issueNumber);
    }

    bool ThatThePRHasBeenAssigned()
    {
        var prClient = GitHubClient.PullRequest;

        Log.Information("Checking if the pull request as been assigned to someone.");

        var prNumber = GitHubActions.Instance is null || GitHubActions.Instance.PullRequestNumber is null
            ? -1
            : (int)(GitHubActions.Instance.PullRequestNumber);

        if (prClient.HasAssignees(Owner, MainProjName, prNumber).Result)
        {
            Log.Information($"{ConsoleTab}✅The pull request '{prNumber}' is properly assigned.");
        }
        else
        {
            var prLink = $"https://github.com/{Owner}/{MainProjName}/pull/{prNumber}";
            var errorMsg = "The pull request '{Value1}' is not assigned to anyone.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}To set an assignee, go to 👉🏼 '{{Value2}}'.";
            Log.Error(errorMsg, prNumber, prLink);
            Assert.Fail("The pull request is not assigned to anybody.");
        }

        return true;
    }

    bool ThatPRHasLabels()
    {
        var prClient = GitHubClient.PullRequest;

        Log.Information("Checking if the pull request has labels.");

        var prNumber = GitHubActions.Instance is null || GitHubActions.Instance.PullRequestNumber is null
            ? -1
            : (int)(GitHubActions.Instance.PullRequestNumber);

        if (prClient.HasLabels(Owner, MainProjName, prNumber).Result)
        {
            Log.Information($"{ConsoleTab}✅The pull request '{prNumber}' has labels.");
        }
        else
        {
            var prLink = $"https://github.com/{Owner}/{MainProjName}/pull/{prNumber}";
            var errorMsg = "The pull request '{Value1}' does not have any labels.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}To add a label, go to 👉🏼 '{{Value2}}'.";
            Log.Error(errorMsg, prNumber, prLink);
            Assert.Fail("The pull request does not have one or more labels.");
        }

        return true;
    }

    bool ThatThePRHasTheLabel(string labelName)
    {
        var prNumber = GitHubActions.Instance?.PullRequestNumber ?? -1;
        var labelExists = false;

        Log.Information("Checking if the pull request has a preview release label.");

        if (prNumber is -1)
        {
            const string errorMsg = "The pr number could not be found.  This must only run as a pull request in GitHub, not locally.";
            Log.Error(errorMsg);
            Assert.Fail("The workflow is not being executed as a pull request in the GitHub environment.");
        }

        labelExists = GitHubClient.PullRequest.LabelExists(Owner, MainProjName, prNumber, labelName).Result;

        if (labelExists)
        {
            Log.Information($"{ConsoleTab}✅The pull request '{prNumber}' has a preview label.");
        }
        else
        {
            var prLink = $"https://github.com/{Owner}/{MainProjName}/pull/{prNumber}";
            var errorMsg = $"The pull request '{{Value1}}' does not have the preview release label '{labelName}'.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}To add the label, go to 👉🏼 '{{Value2}}'.";
            Log.Error(errorMsg, prNumber, prLink);
            Assert.Fail("The pull request does not have a preview release label.");
        }

        return true;
    }

    bool ThatThePRTargetBranchIsValid(BranchType branchType)
    {
        var targetBranch = GitHubActions.Instance?.BaseRef ?? string.Empty;
        var errorMsg = string.Empty;
        var isValidBranch = false;

        Log.Information($"Checking if pull request target branch '{targetBranch}' is valid.");

        switch (branchType)
        {
            case BranchType.Develop:
                isValidBranch = targetBranch.IsDevelopBranch();

                if (isValidBranch)
                {
                    Log.Information($"{ConsoleTab}✅The '{branchType}' branch '{targetBranch}' valid.");
                }
                else
                {
                    errorMsg = "The development branch '{Value}' is invalid.";
                    errorMsg += $"{Environment.NewLine}{ConsoleTab}The syntax for the develop branch is 'develop'.";
                }
                break;
            case BranchType.Master:
                isValidBranch = targetBranch.IsDevelopBranch();

                if (isValidBranch)
                {
                    Log.Information($"{ConsoleTab}✅The '{branchType}' branch '{targetBranch}' valid.");
                }
                else
                {
                    errorMsg = "The production branch '{Value}' is invalid.";
                    errorMsg += $"{Environment.NewLine}{ConsoleTab}The syntax for the production branch is 'master'.";
                }
                break;
            case BranchType.Feature:
                isValidBranch = targetBranch.IsFeatureBranch();

                if (isValidBranch)
                {
                    Log.Information($"{ConsoleTab}✅The '{branchType}' branch '{targetBranch}' valid.");
                }
                else
                {
                    errorMsg = "The feature branch '{Value}' is invalid.";
                    errorMsg += $"{Environment.NewLine}{ConsoleTab}The syntax for feature branches is 'feature/#-*'.";
                }
                break;
            case BranchType.PreviewFeature:
                isValidBranch = targetBranch.IsPreviewFeatureBranch();

                if (isValidBranch)
                {
                    Log.Information($"{ConsoleTab}✅The '{branchType}' branch '{targetBranch}' valid.");
                }
                else
                {
                    errorMsg = "The preview feature branch '{Value}' is invalid.";
                    errorMsg += $"{Environment.NewLine}{ConsoleTab}The syntax for feature branches is 'preview/feature/#-*'.";
                }
                break;
            case BranchType.Release:
                isValidBranch = targetBranch.IsReleaseBranch();

                if (isValidBranch)
                {
                    Log.Information($"{ConsoleTab}✅The '{branchType}' branch '{targetBranch}' valid.");
                }
                else
                {
                    errorMsg = "The release branch '{Value}' is invalid.";
                    errorMsg += $"{Environment.NewLine}{ConsoleTab}The syntax for release branches is 'release/v#.#.#'.";
                }
                break;
            case BranchType.Preview:
                isValidBranch = targetBranch.IsPreviewBranch();

                if (isValidBranch)
                {
                    Log.Information($"{ConsoleTab}✅The '{branchType}' branch '{targetBranch}' valid.");
                }
                else
                {
                    errorMsg = "The preview branch '{Value}' is invalid.";
                    errorMsg += $"{Environment.NewLine}{ConsoleTab}The syntax for preview branches is 'preview/v#.#.#-preview.#'.";
                }
                break;
            case BranchType.HotFix:
                isValidBranch = targetBranch.IsHotFixBranch();

                if (isValidBranch)
                {
                    Log.Information($"{ConsoleTab}✅The '{branchType}' branch '{targetBranch}' valid.");
                }
                else
                {
                    errorMsg = "The hotfix branch '{Value}' is invalid.";
                    errorMsg += $"{Environment.NewLine}{ConsoleTab}The syntax for hotfix branches is 'hotfix/#-*'.";
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(branchType), branchType, null);
        }

        if (isValidBranch)
        {
            return true;
        }

        Log.Error(errorMsg, targetBranch);
        var runType = IsPullRequest() ? "pull request" : "manual";
        Assert.Fail($"Invalid target branch for the {runType} run.");
        return false;
    }

    bool ThatThePRSourceBranchIsValid(BranchType branchType)
    {
        var sourceBranch = GitHubActions.Instance?.HeadRef ?? string.Empty;
        var errorMsg = string.Empty;
        var isValidBranch = false;

        Log.Information("Validating PR Source Branch:");

        switch (branchType)
        {
            case BranchType.Develop:
            case BranchType.Master:
                errorMsg = "Invalid source branch.  'master' and 'develop' branches are not aloud to be merged into another branch.";
                Log.Error(errorMsg);
                break;
            case BranchType.Feature:
                isValidBranch = sourceBranch.IsFeatureBranch();

                if (isValidBranch)
                {
                    var validIssueNumResult = BranchIssueNumberValid(branchType).Result;

                    if (validIssueNumResult.isValid)
                    {
                        Log.Information($"{ConsoleTab}✅The '{branchType}' branch '{sourceBranch}' is valid.");
                    }
                    else
                    {
                        errorMsg = "The issue '{Value1}' does not exist for feature branch '{Value2}'.";
                        errorMsg += $"{Environment.NewLine}{ConsoleTab}The source branch '{{Value2}}' must be recreated with the correct issue number.";
                        errorMsg += $"{Environment.NewLine}{ConsoleTab}The syntax requirements for feature branches is '{FeatureBranchSyntax}.";
                        Log.Error(errorMsg, validIssueNumResult.issueNum, sourceBranch);
                    }
                }
                else
                {
                    errorMsg = "The feature branch '{Value}' is invalid.";
                    errorMsg += $"{Environment.NewLine}{ConsoleTab}The syntax for feature branches is 'feature/#-*'.";
                }
                break;
            case BranchType.PreviewFeature:
                isValidBranch = sourceBranch.IsPreviewFeatureBranch();

                if (isValidBranch)
                {
                    var validIssueNumResult = BranchIssueNumberValid(branchType).Result;

                    if (validIssueNumResult.isValid)
                    {
                        Log.Information($"{ConsoleTab}✅The '{branchType}' branch '{sourceBranch}' is valid.");
                    }
                    else
                    {
                        errorMsg = "The issue '{Value1}' does not exist for feature branch '{Value2}'.";
                        errorMsg += $"{Environment.NewLine}{ConsoleTab}The source branch '{{Value2}}' must be recreated with the correct issue number.";
                        errorMsg += $"{Environment.NewLine}{ConsoleTab}The syntax requirements for feature branches is '{FeatureBranchSyntax}.";
                        Log.Error(errorMsg, validIssueNumResult.issueNum, sourceBranch);
                    }
                }
                else
                {
                    errorMsg = "The preview feature branch '{Value}' is invalid.";
                    errorMsg += $"{Environment.NewLine}{ConsoleTab}The syntax for feature branches is 'preview/feature/#-*'.";
                }
                break;
            case BranchType.Release:
                isValidBranch = sourceBranch.IsReleaseBranch();

                if (isValidBranch)
                {
                    Log.Information($"{ConsoleTab}✅The '{branchType}' branch '{sourceBranch}' is valid.");
                }
                else
                {
                    errorMsg = "The release branch '{Value}' is invalid.";
                    errorMsg += $"{Environment.NewLine}{ConsoleTab}The syntax for release branches is 'release/v#.#.#'.";
                }
                break;
            case BranchType.Preview:
                isValidBranch = sourceBranch.IsPreviewBranch();

                if (isValidBranch)
                {
                    Log.Information($"{ConsoleTab}✅The '{branchType}' branch '{sourceBranch}' is valid.");
                }
                else
                {
                    errorMsg = "The preview branch '{Value}' is invalid.";
                    errorMsg += $"{Environment.NewLine}{ConsoleTab}The syntax for preview branches is 'preview/v#.#.#-preview.#'.";
                }
                break;
            case BranchType.HotFix:
                isValidBranch = sourceBranch.IsHotFixBranch();

                if (isValidBranch)
                {
                    Log.Information($"{ConsoleTab}✅The '{branchType}' branch '{sourceBranch}' is valid.");
                }
                else
                {
                    errorMsg = "The hotfix branch '{Value}' is invalid.";
                    errorMsg += $"{Environment.NewLine}{ConsoleTab}The syntax for hotfix branches is 'hotfix/#-*'.";
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(branchType), branchType, null);
        }

        if (isValidBranch)
        {
            return true;
        }

        Log.Error(errorMsg, sourceBranch);
        Assert.Fail("Invalid pull request source branch.");
        return false;
    }

    bool ThatThePreviewPRBranchVersionsMatch()
    {
        var sourceBranch = GitHubActions.Instance?.HeadRef ?? string.Empty;
        var targetBranch = GitHubActions.Instance?.BaseRef ?? string.Empty;
        var errors = new List<string>();

        Log.Information("Checking that the version section for the preview release PR source and target branches match.");

        if (string.IsNullOrEmpty(sourceBranch) || string.IsNullOrEmpty(targetBranch))
        {
            errorMsg = "The workflow must be executed from a pull request in the GitHub environment.";
            Log.Error(errorMsg);
            Assert.Fail("The workflow was executed from the wrong environment and context.");
            return false;
        }

        if (sourceBranch.IsPreviewBranch() is false)
        {
            var errorMsg = $"The pull request source branch '{sourceBranch}' must be a preview branch.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}Preview branch syntax is 'preview/v#.#.#-preview.#'";
            errors.Add(errorMsg);
        }

        if (targetBranch.IsReleaseBranch() is false)
        {
            var errorMsg = $"The pull request target branch '{targetBranch}' must be a release branch.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}Release branch syntax is 'release/v#.#.#'";
            errors.Add(errorMsg);
        }

        var srcBranchVersionSection = sourceBranch.Contains('/') && sourceBranch.Contains('-') && sourceBranch.Length > 3
            ? sourceBranch.Split('/')[1].Split('-')[0]
            : string.Empty;
        var targetBranchVersionSection = sourceBranch.Contains('/') && sourceBranch.Length > 3
            ? targetBranch.Split('/')[1]
            : string.Empty;

        if (srcBranchVersionSection != targetBranchVersionSection ||
            (string.IsNullOrEmpty(srcBranchVersionSection) && string.IsNullOrEmpty(targetBranchVersionSection)))
        {
            var errorMsg = $"The main version sections of the source branch '{sourceBranch}' and the target branch '{targetBranch}' do not match.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}Source Branch Syntax: 'preview/v#.#.#-preview.#'";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}Target Branch Syntax: 'release/v#.#.#'";
            errors.Add(errorMsg);
        }

        if (errors.Count <= 0)
        {
            return true;
        }

        errors.PrintErrors("There is an issue with the version in the csproj file.");

        return false;
    }

    bool ThatTheProjectVersionsAreValid(ReleaseType releaseType)
    {
        var project = Solution.GetProject(MainProjName);
        var errors = new List<string>();

        Log.Information("Checking that all of the versions in the csproj file are valid.");

        if (project is null)
        {
            Log.Error($"Could not find the project '{MainProjName}'");
            Assert.Fail("There was an issue getting the project.");
            return false;
        }

        var versionExists = project.VersionExists();
        var fileVersionExists = project.FileVersionExists();
        var assemblyVersionExists = project.AssemblyVersionExists();

        // Check if the regular version value exists
        if (versionExists is false)
        {
            errors.Add("The version '<Version/>' value in the csproj file does not exist.");
        }

        // Check if the file version value exists
        if (fileVersionExists is false)
        {
            errors.Add("The version '<FileVersion/>' value in the csproj file does not exist.");
        }

        // Check if the assembly version value exists
        if (assemblyVersionExists is false)
        {
            errors.Add("The version '<AssemblyVersion/>' value in the csproj file does not exist.");
        }

        const string previewBranchSyntax = "#.#.#-preview.#";
        const string productionBranchSyntax = "#.#.#";

        if (versionExists)
        {
            var validVersionSyntax = releaseType switch
            {
                ReleaseType.Preview => project.HasCorrectVersionSyntax(previewBranchSyntax),
                ReleaseType.Production => project.HasCorrectVersionSyntax(productionBranchSyntax),
                _ => throw new ArgumentOutOfRangeException(nameof(releaseType), releaseType, null)
            };

            if (validVersionSyntax is false)
            {
                var msg = "The syntax for the '<Version/>' value in the csproj file is invalid.";
                msg += $"{Environment.NewLine}{ConsoleTab}Valid syntax is '{previewBranchSyntax}'";
                errors.Add(msg);
            }
        }

        if (fileVersionExists)
        {
            var validFileVersionSyntax = releaseType switch
            {
                ReleaseType.Preview => project.HasCorrectFileVersionSyntax(previewBranchSyntax),
                ReleaseType.Production => project.HasCorrectFileVersionSyntax(productionBranchSyntax),
                _ => throw new ArgumentOutOfRangeException(nameof(releaseType), releaseType, null)
            };

            if (validFileVersionSyntax is false)
            {
                var msg = "The syntax for the '<FileVersion/>' value in the csproj file is invalid.";
                msg += $"{Environment.NewLine}{ConsoleTab}Valid syntax is '{previewBranchSyntax}'";
                errors.Add(msg);
            }
        }

        if (assemblyVersionExists)
        {
            var validAssemblyVersionSyntax = releaseType switch
            {
                ReleaseType.Preview => project.HasCorrectAssemblyVersionSyntax(productionBranchSyntax),
                ReleaseType.Production => project.HasCorrectAssemblyVersionSyntax(productionBranchSyntax),
                _ => throw new ArgumentOutOfRangeException(nameof(releaseType), releaseType, null)
            };

            if (validAssemblyVersionSyntax is false)
            {
                var msg = "The syntax for the '<AssemblyVersion/>' value in the csproj file is invalid.";
                msg += $"{Environment.NewLine}{ConsoleTab}Valid syntax is '{productionBranchSyntax}'";
                errors.Add(msg);
            }
        }

        if (errors.Count <= 0)
        {
            return true;
        }

        errors.PrintErrors("Project versions are invalid.");

        return errors.Count <= 0;
    }

    bool ThatThePRSourceBranchVersionSectionMatchesProjectVersion(ReleaseType releaseType)
    {
        var project = Solution.GetProject(MainProjName);
        var sourceBranch = GitHubActions.Instance?.HeadRef ?? string.Empty;
        var errors = new List<string>();

        Log.Information(
    $"Checking that the project version matches the version section of the PR source {releaseType.ToString().ToLower()} branch.");

        if (string.IsNullOrEmpty(sourceBranch))
        {
            errors.Add("The workflow must be executed from a pull request in the GitHub environment.");
        }

        if (project is null)
        {
            errors.Add($"Could not find the project '{MainProjName}'");
        }

        var setProjectVersion = project?.GetVersion() ?? string.Empty;

        var branchType = sourceBranch.GetBranchType();

        if (branchType is BranchType.Preview or BranchType.Release)
        {
            var branchVersionSection = branchType == BranchType.Release
                ? sourceBranch.Split('/')[0].TrimStart('v')
                : sourceBranch.Split('/')[1].TrimStart('v');

            if (setProjectVersion != branchVersionSection)
            {
                errors.Add($"The set project version '{setProjectVersion}' does not match the version branch section '{branchVersionSection}' of the source branch.");
            }
        }
        else
        {
            errors.Add($"The branch '{sourceBranch}' is not a preview or release branch.");
        }

        if (errors.Count <= 0)
        {
            return true;
        }

        errors.PrintErrors("The pull request source branch version section does not match the set project version.");

        return false;
    }

    bool ThatTheReleaseMilestoneExists()
    {
        var project = Solution.GetProject(MainProjName);
        var errors = new List<string>();

        Log.Information($"Checking that the release milestone exists for the current version.");

        if (project is null)
        {
            errors.Add($"Could not find the project '{MainProjName}'");
        }

        var projectVersion = project?.GetVersion() ?? string.Empty;
        var milestoneClient = GitHubClient.Issue.Milestone;

        var milestoneExists = milestoneClient.MilestoneExists(Owner, MainProjName, $"v{projectVersion}").Result;

        if (milestoneExists is false)
        {
            const string milestoneUrl = $"https://github.com/{Owner}/{MainProjName}/milestones/new";
            var errorMsg = $"The milestone for version '{projectVersion}' does not exist.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab} To create a milestone, go here 👉🏼 {milestoneUrl}";
            errors.Add(errorMsg);
        }

        if (errors.Count <= 0)
        {
            return true;
        }

        errors.PrintErrors("The release milestone does not exist.");

        return false;
    }

    bool ThatTheReleaseMilestoneContainsIssues()
    {
        var project = Solution.GetProject(MainProjName);
        var errors = new List<string>();

        Log.Information($"Checking that the release milestone current version contains issues.");

        if (project is null)
        {
            errors.Add($"Could not find the project '{MainProjName}'");
        }

        var projectVersion = project?.GetVersion() ?? string.Empty;
        var milestoneClient = GitHubClient.Issue.Milestone;

        var milestone = milestoneClient.GetByTitle(Owner, MainProjName, $"v{projectVersion}").Result;

        if (milestone is null)
        {
            const string milestoneUrl = $"https://github.com/{Owner}/{MainProjName}/milestones/new";
            var errorMsg = $"The milestone for version '{projectVersion}' does not exist.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab} To create a milestone, go here 👉🏼 {milestoneUrl}";
            errors.Add(errorMsg);
        }

        var totalMilestoneIssues = milestone?.OpenIssues ?? 0 + milestone?.ClosedIssues ?? 0;

        if (totalMilestoneIssues == 0)
        {
            const string milestoneUrl = $"https://github.com/{Owner}/{MainProjName}/milestones/new";
            var errorMsg = $"The milestone for version '{projectVersion}' does not contain any issues or pull requests.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}Add some issues to the milestone";
            errors.Add(errorMsg);
        }

        if (errors.Count <= 0)
        {
            return true;
        }

        errors.PrintErrors("The release milestone does not contain any issues.");

        return false;
    }

    bool ThatTheReleaseMilestoneOnlyContainsSingleReleaseToDoIssue(ReleaseType releaseType)
    {
        var project = Solution.GetProject(MainProjName);
        var errors = new List<string>();
        var releaseTypeStr = releaseType.ToString().ToLower();

        Log.Information($"Checking that the release milestone only contains a single release todo issue item.");

        if (project is null)
        {
            errors.Add($"Could not find the project '{MainProjName}'");
        }

        var projectVersion = project?.GetVersion() ?? string.Empty;
        var mileStoneTitle = $"v{projectVersion}";
        var issueClient = GitHubClient.Issue;
        var mileStoneClient = GitHubClient.Issue.Milestone;
        var milestone = mileStoneClient.GetByTitle(Owner, MainProjName, mileStoneTitle).Result;

        if (milestone is null)
        {
            const string milestoneUrl = $"https://github.com/{Owner}/{MainProjName}/milestones/new";
            var errorMsg = $"Cannot check a milestone that does not exist.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}To create a milestone, go here 👉🏼 {milestoneUrl}";
            errors.Add(errorMsg);
        }

        var issues = issueClient.IssuesForMilestone(Owner, MainProjName, mileStoneTitle).Result;

        if (issues.Length <= 0)
        {
            var errorMsg = $"The milestone does not contain any issues.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab} To view the milestone, go here 👉🏼 {milestone?.HtmlUrl}";
            errors.Add(errorMsg);
        }

        var issueTitleAndLabel = releaseType switch
        {
            ReleaseType.Preview => "🚀Preview Release",
            ReleaseType.Production => "🚀Production Release",
            _ => throw new ArgumentOutOfRangeException(nameof(releaseType), releaseType, null)
        };

        var totalReleaseToDoIssues =
            issues.Count(i => i.Title == issueTitleAndLabel &&
                                  i.Labels.Count == 1 &&
                                  i.PullRequest is null &&
                                  i.Labels[0].Name == issueTitleAndLabel);

        if (totalReleaseToDoIssues == 0 || totalReleaseToDoIssues > 1)
        {
            var errorMsg = $"The {releaseTypeStr} release milestone '{mileStoneTitle}' does not contain, or has too many release todo issues.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}Release ToDo Issue Requirements:";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}  - Title must be equal to '{issueTitleAndLabel}'";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}  - Contain only a single '{issueTitleAndLabel}' label";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}  - Only contain 1 release todo issue.";

            errors.Add(errorMsg);
        }

        if (errors.Count <= 0)
        {
            return true;
        }

        errors.PrintErrors("The release milestone does not contain a release todo issue.");

        return false;
    }

    bool ThatTheReleaseMilestoneOnlyContainsSingleReleasePR(ReleaseType releaseType)
    {
        var project = Solution.GetProject(MainProjName);
        var errors = new List<string>();
        var releaseTypeStr = releaseType.ToString().ToLower();

        Log.Information($"Checking that the release milestone only contains a single release PR item.");

        if (project is null)
        {
            errors.Add($"Could not find the project '{MainProjName}'");
        }

        var projectVersion = project?.GetVersion() ?? string.Empty;
        var mileStoneTitle = $"v{projectVersion}";
        var issueClient = GitHubClient.Issue;
        var mileStoneClient = GitHubClient.Issue.Milestone;
        var milestone = mileStoneClient.GetByTitle(Owner, MainProjName, mileStoneTitle).Result;

        if (milestone is null)
        {
            const string milestoneUrl = $"https://github.com/{Owner}/{MainProjName}/milestones/new";
            var errorMsg = $"Cannot check a milestone that does not exist.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}To create a milestone, go here 👉🏼 {milestoneUrl}";
            errors.Add(errorMsg);
        }

        var issues = issueClient.IssuesForMilestone(Owner, MainProjName, mileStoneTitle).Result;

        if (issues.Length <= 0)
        {
            var errorMsg = $"The milestone does not contain any pull requests.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab} To view the milestone, go here 👉🏼 {milestone?.HtmlUrl}";
            errors.Add(errorMsg);
        }

        var prTitle = releaseType switch
        {
            ReleaseType.Preview => "Preview Release",
            ReleaseType.Production => "Production Release",
            _ => throw new ArgumentOutOfRangeException(nameof(releaseType), releaseType, null)
        };

        var releaseLabel = releaseType switch
        {
            ReleaseType.Preview => "🚀Preview Release",
            ReleaseType.Production => "🚀Production Release",
            _ => throw new ArgumentOutOfRangeException(nameof(releaseType), releaseType, null)
        };

        var totalReleasePullRequests =
            issues.Count(i => i.Title == prTitle &&
                                  i.Labels.Count == 1 &&
                                  i.PullRequest is not null &&
                                  i.Labels[0].Name == releaseLabel);

        if (totalReleasePullRequests == 0 || totalReleasePullRequests > 1)
        {
            var errorMsg = $"The {releaseTypeStr} release milestone '{mileStoneTitle}' does not contain, or has too many release pull requests.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}Release Pull Request Requirements:";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}  - Title must be equal to '{releaseLabel}'";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}  - Contain only a single '{releaseLabel}' label";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}  - Only contain 1 release pull request.";

            errors.Add(errorMsg);
        }

        if (errors.Count <= 0)
        {
            return true;
        }

        errors.PrintErrors("The release milestone does not contain a release pull request.");

        return false;
    }

    bool ThatAllOfTheReleaseMilestoneIssuesAreClosed()
    {
        var project = Solution.GetProject(MainProjName);
        var errors = new List<string>();

        Log.Information($"Checking that all of the release milestone issues and pull requests are closed.");

        if (project is null)
        {
            errors.Add($"Could not find the project '{MainProjName}'");
        }

        var projectVersion = project?.GetVersion() ?? string.Empty;
        var milestoneClient = GitHubClient.Issue.Milestone;

        var milestone = milestoneClient.GetByTitle(Owner, MainProjName, $"v{projectVersion}").Result;

        if (milestone is null)
        {
            const string milestoneUrl = $"https://github.com/{Owner}/{MainProjName}/milestones/new";
            var errorMsg = $"The milestone for version '{projectVersion}' does not exist.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}To create a milestone, go here 👉🏼 {milestoneUrl}";
            errors.Add(errorMsg);
        }

        var totalOpenIssues = milestone?.OpenIssues ?? 0;

        if (totalOpenIssues > 0)
        {
            var errorMsg = $"The milestone for version '{projectVersion}' contains opened issues.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab} To view the opened issues for the milestone, go here 👉🏼 {milestone?.HtmlUrl}";
            errors.Add(errorMsg);
        }

        if (errors.Count <= 0)
        {
            return true;
        }

        errors.PrintErrors("The release milestone does not contain any issues.");

        return false;
    }

    bool ThatTheReleaseTagDoesNotAlreadyExist(ReleaseType releaseType)
    {
        var project = Solution.GetProject(MainProjName);
        var errors = new List<string>();

        var releaseTypeStr = releaseType.ToString().ToLower();

        Log.Information($"Checking that a {releaseTypeStr} release tag that matches the set project version does not already exist.");

        if (project is null)
        {
            errors.Add($"Could not find the project '{MainProjName}'");
        }

        var projectVersion = project?.GetVersion() ?? string.Empty;

        var repoClient = GitHubClient.Repository;
        var tagExists = repoClient.TagExists(Owner, MainProjName, $"v{projectVersion}").Result;

        if (tagExists)
        {
            var tagUrl = $"https://github.com/{Owner}/{MainProjName}/tree/{projectVersion}";
            var errorMsg = $"The {releaseTypeStr} release tag '{projectVersion}' already exists.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab} To view the tag, go here 👉🏼 {tagUrl}";
            errors.Add(errorMsg);
        }

        if (errors.Count <= 0)
        {
            return true;
        }

        errors.PrintErrors($"The {releaseTypeStr} release tag already exists.");

        return false;
    }

    bool ThatTheReleaseNotesExist(ReleaseType releaseType)
    {
        var project = Solution.GetProject(MainProjName);
        var errors = new List<string>();

        var releaseTypeStr = releaseType.ToString().ToLower();

        Log.Information($"Checking that the release notes for the {releaseTypeStr} release exist.");

        if (project is null)
        {
            errors.Add($"Could not find the project '{MainProjName}'");
        }

        var projectVersion = project?.GetVersion() ?? string.Empty;

        var releaseNotesDoNotExist = ReleaseNotesDoNotExist(releaseType, projectVersion);

        if (releaseNotesDoNotExist)
        {
            var notesDirPath = $"~/Documentation/ReleaseNotes/{releaseType.ToString()}Releases";
            var errorMsg = $"The {releaseTypeStr} release notes do not exist for version {projectVersion}";
            var notesFileName = $"Release-Notes-{projectVersion}.md";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}The {releaseTypeStr} release notes go in the directory '{notesDirPath}'";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}The {releaseTypeStr} release notes file name should be '{notesFileName}'.";
            errors.Add(errorMsg);
        }

        if (errors.Count <= 0)
        {
            return true;
        }

        errors.PrintErrors($"The {releaseTypeStr} release notes do not exist.");

        return false;
    }

    bool ThatMilestoneIssuesExistInReleaseNotes(ReleaseType releaseType)
    {
        var project = Solution.GetProject(MainProjName);
        var errors = new List<string>();

        var releaseTypeStr = releaseType.ToString().ToLower();

        Log.Information($"Checking that the {releaseTypeStr} release notes contain the release issues.");

        if (project is null)
        {
            errors.Add($"Could not find the project '{MainProjName}'");
        }

        bool IsNotReleaseToDoIssue(Issue issue)
        {
            var issueAndLabel = $"🚀{releaseType} Release";

            var isIssue = issue.PullRequest is null &&
                   issue.Title != issueAndLabel &&
                   issue.Labels.Any(l => l.Name == issueAndLabel) is false;

            return issue.PullRequest is null &&
                   issue.Title != issueAndLabel &&
                   issue.Labels.Any(l => l.Name == issueAndLabel) is false;
        }

        var projectVersion = project?.GetVersion() ?? string.Empty;
        var milestoneTitle = $"v{projectVersion}";

        var milestoneIssues = GitHubClient.Issue.IssuesForMilestone(Owner, MainProjName, milestoneTitle).Result;
        var issuesForNotes = milestoneIssues.Where(IsNotReleaseToDoIssue).ToArray();

        var releaseNotes = Solution.GetReleaseNotes(releaseType, projectVersion);

        if (string.IsNullOrEmpty(releaseNotes))
        {
            errors.Add($"No {releaseTypeStr} release notes exist to check for issue numbers.");
        }

        if (releaseNotes.IsNotNullOrEmpty())
        {
            foreach (var issue in issuesForNotes)
            {
                const string baseUrl = "https://github.com";
                const string issueNoteSyntax = $"[#<issue-num>]({baseUrl}/<repo-owner>/<repo-name>/issues/<issue-num>) - <notes>";
                var issueNote = $"[#{issue.Number}]({baseUrl}/{Owner}/{MainProjName}/issues/{issue.Number})";

                if (releaseNotes.Contains(issueNote) is false)
                {
                    var errorMsg = $"The {releaseTypeStr} release notes does not contain any notes for issue '{issue.Number}'";
                    errorMsg += $"{Environment.NewLine}{ConsoleTab}Issue Note Syntax: {issueNoteSyntax}";
                    errors.Add(errorMsg);
                }
            }
        }

        if (errors.Count <= 0)
        {
            return true;
        }

        errors.PrintErrors($"The {releaseTypeStr} release notes is missing notes for 1 ore more issues.");

        return false;
    }

    bool ThatGitHubReleaseDoesNotExist(ReleaseType releaseType)
    {
        var project = Solution.GetProject(MainProjName);
        var errors = new List<string>();

        var releaseTypeStr = releaseType.ToString().ToLower();

        Log.Information($"Checking that the {releaseTypeStr} GitHub release does not already exist.");

        if (project is null)
        {
            errors.Add($"Could not find the project '{MainProjName}'");
        }

        var projectVersion = project?.GetVersion() ?? string.Empty;

        var versionSyntax = releaseType switch
        {
            ReleaseType.Preview => "#.#.#-preview.#",
            ReleaseType.Production => "#.#.#",
            _ => throw new ArgumentOutOfRangeException(nameof(releaseType), releaseType, null)
        };

        var versionSyntaxValid = project?.HasCorrectVersionSyntax(versionSyntax);

        if (versionSyntaxValid is false)
        {
            var errorMsg = $"The set project version '{projectVersion}' has invalid syntax.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}Required Version Syntax: '{versionSyntax}'";
            errors.Add(errorMsg);
        }

        var releaseTag = $"v{projectVersion}";

        var releaseClient = GitHubClient.Repository.Release;

        var releaseExists = releaseClient.ReleaseExists(Owner, MainProjName, releaseTag).Result;

        if (releaseExists)
        {
            var errorMsg = $"The {releaseTypeStr} release for version '{releaseTag}' already exists.";
            errorMsg += $"{Environment.NewLine}{ConsoleTab}Verify that the project versions have been correctly updated.";
            errors.Add(errorMsg);
        }

        if (errors.Count <= 0)
        {
            return true;
        }

        errors.PrintErrors("A GitHub release already exists for the currently set version.");

        return false;
    }

    bool NugetPackageDoesNotExist()
    {
        var project = Solution.GetProject(MainProjName);
        var errors = new List<string>();

        Log.Information($"Checking that the nuget package does not already exist.");

        if (project is null)
        {
            errors.Add($"Could not find the project '{MainProjName}'");
        }

        var projectVersion = project?.GetVersion() ?? string.Empty;
        projectVersion = "1.2.4-preview.1";

        // TODO: This package name might be the owner.reponame.  It could be something different entirely
        const string packageName = MainProjName;
        var nugetService = new NugetDataService();

        var packageVersions = nugetService.GetNugetVersions(packageName).Result;

        var nugetPackageExists = packageVersions.Any(i => i == projectVersion);

        if (nugetPackageExists)
        {
            errors.Add($"The nuget package '{packageName}' version 'v{projectVersion}' already exists.");
        }

        if (errors.Count <= 0)
        {
            return true;
        }

        errors.PrintErrors("The nuget package already exists.");

        return false;
    }
}
