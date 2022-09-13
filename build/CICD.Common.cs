using System;
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

    bool ThatRunIsForPullRequest(string runName, RunType runType)
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

        var notesExist = (from f in Glob.Files(releaseNotesDirPath, "*.md")
            where f.Contains(version)
            select f).Any();

        if (notesExist)
        {
            Log.Information($"{ConsoleTab}✅The release notes for the '{releaseType}' release exist in the directory '{releaseNotesDirPath}'");
        }
        else
        {
            var errorMsg = $"The '{releaseType}' release notes could not be found in the directory '{releaseNotesDirPath}'.";
            Log.Error(errorMsg);
            Assert.Fail("Release notes could not be found.");
        }

        return notesExist;
    }

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

    bool ThatPRHasBeenAssigned()
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
            errorMsg += $"{ConsoleTab}To set an assignee, go to 👉🏼 '{{Value2}}'.";
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
            errorMsg += $"{ConsoleTab}To add a label, go to 👉🏼 '{{Value2}}'.";
            Log.Error(errorMsg, prNumber, prLink);
            Assert.Fail("The pull request does not have one or more labels.");
        }

        return true;
    }

    bool ThatPRTargetBranchIsValid(BranchType branchType)
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
                    errorMsg += $"{ConsoleTab}The syntax for the develop branch is 'develop'.";
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
                    errorMsg += $"{ConsoleTab}The syntax for the production branch is 'master'.";
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
                    errorMsg += $"{ConsoleTab}The syntax for feature branches is 'feature/#-*'.";
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
                    errorMsg += $"{ConsoleTab}The syntax for feature branches is 'preview/feature/#-*'.";
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
                    errorMsg += $"{ConsoleTab}The syntax for release branches is 'release/v#.#.#'.";
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
                    errorMsg += $"{ConsoleTab}The syntax for preview branches is 'preview/v#.#.#-preview.#'.";
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
                    errorMsg += $"{ConsoleTab}The syntax for hotfix branches is 'hotfix/#-*'.";
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

    bool ThatPRSourceBranchIsValid(BranchType branchType)
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
                        errorMsg += $"{ConsoleTab}The source branch '{{Value2}}' must be recreated with the correct issue number.";
                        errorMsg += $"{ConsoleTab}The syntax requirements for feature branches is '{FeatureBranchSyntax}.";
                        Log.Error(errorMsg, validIssueNumResult.issueNum, sourceBranch);
                    }
                }
                else
                {
                    errorMsg = "The feature branch '{Value}' is invalid.";
                    errorMsg += $"{ConsoleTab}The syntax for feature branches is 'feature/#-*'.";
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
                        errorMsg += $"{ConsoleTab}The source branch '{{Value2}}' must be recreated with the correct issue number.";
                        errorMsg += $"{ConsoleTab}The syntax requirements for feature branches is '{FeatureBranchSyntax}.";
                        Log.Error(errorMsg, validIssueNumResult.issueNum, sourceBranch);
                    }
                }
                else
                {
                    errorMsg = "The preview feature branch '{Value}' is invalid.";
                    errorMsg += $"{ConsoleTab}The syntax for feature branches is 'preview/feature/#-*'.";
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
                    errorMsg += $"{ConsoleTab}The syntax for release branches is 'release/v#.#.#'.";
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
                    errorMsg += $"{ConsoleTab}The syntax for preview branches is 'preview/v#.#.#-preview.#'.";
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
                    errorMsg += $"{ConsoleTab}The syntax for hotfix branches is 'hotfix/#-*'.";
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
}
