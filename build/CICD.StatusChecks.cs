using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Tools.GitHub;
using Octokit;
using Serilog;
using Serilog.Events;
using static Nuke.Common.Tools.Git.GitTasks;

namespace NukeLearningCICD;

// TODO: Replace all owner and name string literals with variables

public partial class CICD // StatusChecks
{
    Target BuildStatusCheck => _ => _
        .Before(BuildAllProjects, BuildMainProject, BuildTestProject)
        .Triggers(BuildAllProjects)
        .Executes(async () =>
        {
            Log.Information("✅Starting Status Check . . .");

            PrintPullRequestInfo();
            await ValidateBranchForStatusCheck();

            Log.Information("Branch Is Valid!!");
        });


    Target UnitTestStatusCheck => _ => _
        .Before(RunAllUnitTests, RunUnitTests)
        .Triggers(RunAllUnitTests)
        .Executes(async () =>
        {
            var msg = "💡Purpose: Verifies that all unit tests for all of the solution projects pass.";
            Log.Information(msg);
            Console.WriteLine();
            Log.Information("✅Starting Status Check . . .");

            PrintPullRequestInfo();
            await ValidateBranchForStatusCheck();

            Log.Information("Branch Is Valid!!");
        });


    Target ValidVersionStatusCheck => _ => _
        .Requires(() => GetBranch().IsMasterBranch() || GetBranch().IsReleaseBranch())
        .Executes(() =>
        {
            var releaseType = GetBranch().IsMasterBranch()
                ? ReleaseType.Production
                : ReleaseType.Preview;

            var msg = "💡Purpose: Verifies that all of the versions exist in the csproj file";
            msg += $"{Environment.NewLine}\t       and that the version syntax is correct.";

            Log.Information(msg);
            Console.WriteLine();
            Log.Information("✅Starting Status Check . . .");
            Log.Information("Executing On Branch: {Value}", GetBranch());
            Log.Information("Type Of Release: {Value}", releaseType);

            var branch = GetBranch();

            if (branch.IsReleaseBranch())
            {
                ValidateVersions("#.#.#-preview.#");
                return;
            }

            if (branch.IsMasterBranch())
            {
                ValidateVersions("#.#.#");
                return;
            }

            Assert.Fail($"The branch must be a 'master' or 'release/v#.#.#' branch, but was executed on the '{GetBranch()}' branch.");
        });

    Target NoGitHubReleaseStatusCheck => _ => _
        .Requires(() => GetBranch().IsMasterBranch() || GetBranch().IsReleaseBranch())
        .Executes(async () =>
        {
            var version = $"v{Solution.GetProject(MainProjName).GetVersion()}";
            var releaseType = GetBranch().IsMasterBranch()
                ? ReleaseType.Production.ToString().ToLower()
                : ReleaseType.Preview.ToString().ToLower();

            var msg = $"💡Purpose: Verifies that no GitHub release already exists for the current version.";
            msg += $"{Environment.NewLine}\t       This status check is only intended to be executed for preview and production releases.";

            Log.Information(msg);
            Console.WriteLine();
            Log.Information("✅Starting Status Check . . .");
            Log.Information("Current Version: {Value}", version);
            Log.Information("Executing On Branch: {Value}", GetBranch());
            Log.Information("Type Of Release: {Value}", releaseType);

            var releaseExists = await GitHubClient.Repository.Release.ReleaseExists(Owner, MainProjName, version);

            if (releaseExists)
            {
                var errorMsg = "A release for version '{Value}' already exist.";
                errorMsg += $"{Environment.NewLine}\t       Did you forget to update the version values in the csproj file?";
                Log.Error(errorMsg, version);
                Assert.Fail($"A release for version '{version}' already exists.");
            }
        });


    Target ReleaseNotesExistStatusCheck => _ => _
        .Requires(() => GetBranch().IsMasterBranch() || GetBranch().IsReleaseBranch())
        .Executes(() =>
        {
            var version = $"v{Solution.GetProject(MainProjName).GetVersion()}";
            var releaseType = GetBranch().IsMasterBranch()
                ? ReleaseType.Production
                : ReleaseType.Preview;

            var releaseTypeStr = releaseType.ToString().ToLower();

            var msg = $"💡Purpose: Verifies that the {releaseTypeStr} release notes exist for the current version.";
            msg += $"{Environment.NewLine}\t       This status check is only meant to be intended for preview and production releases.";

            Log.Information(msg);
            Console.WriteLine();
            Log.Information("✅Starting Status Check . . .");
            Log.Information("Current Version: {Value}", version);
            Log.Information("Executing On Branch: {Value}", GetBranch());
            Log.Information("Type Of Release: {Value}", releaseTypeStr);

            if (ReleaseNotesExist(releaseType, version) is false)
            {
                var errorMsg = $"The {releaseTypeStr} release notes for version '{version}' could not be found.";
                Log.Error(errorMsg);
                Assert.Fail($"The {releaseTypeStr} release notes could not be found.");
            }
        });


    Target MilestoneStateStatusCheck => _ => _
        .Requires(() => GetBranch().IsMasterBranch() || GetBranch().IsReleaseBranch())
        .Executes(async () =>
        {
            var version = $"v{Solution.GetProject(MainProjName).GetVersion()}";
            var releaseType = GetBranch().IsMasterBranch()
                ? ReleaseType.Production.ToString().ToLower()
                : ReleaseType.Preview.ToString().ToLower();

            var msg = $"💡Purpose: Verifies that the GitHub {releaseType} milestone is in the correct state.";
            msg += $"{Environment.NewLine}\t       This correct state means that all of the issues are closed and pull requests are merged.";

            Log.Information(msg);
            Console.WriteLine();
            Log.Information("✅Starting Status Check . . .");
            Log.Information("Current Version: {Value}", version);
            Log.Information("Executing On Branch: {Value}", GetBranch());
            Log.Information("Type Of Release: {Value}", releaseType);

            var milestoneClient = GitHubClient.Issue.Milestone;
            var milestone = await milestoneClient.GetByTitle(Owner, MainProjName, version);

            if (milestone is not null)
            {
                // If all of the issues are not closed
                if (milestone.OpenIssues > 0)
                {
                    var errorMsg = $"Some issues are still open for milestone '{version}'.";
                    errorMsg += $"{Environment.NewLine}\t       Please close all open issues before attempting a release.";
                    errorMsg += $"{Environment.NewLine}\t       Goto the milestone here 👉🏼 {milestone.HtmlUrl}";
                    Log.Error(errorMsg);
                    Assert.Fail($"Milestone {version} still contains open issues.");
                }
            }
            else
            {
                Log.Error("The milestone '{Value}' does not exist.", version);
                Assert.Fail($"Could not find the milestone '{version}' to analyze its state.");
            }
        });

    Target TagDoesNotExistStatusCheck => _ => _
        .Executes(async () =>
        {
            var repoClient = GitHubClient.Repository;

            var project = Solution.GetProject(MainProjName);
            var version = project is null ? string.Empty : project.GetVersion();

            var tagExists = await repoClient.TagExists(Owner, MainProjName, version);

            if (tagExists)
            {
                var errorMsg = "The tag '{Value}' already exists.  If doing a production or preview release, the tag must not already exist.";
                errorMsg += $"{Environment.NewLine}\t       The tag is auto created upon release.";
                Log.Error(errorMsg);
                Assert.Fail($"The tag '{version}' already exists.");
            }
        });

    Target DebugTask => _ => _
        .Executes(async () =>
        {
            Log.Information($"GitHubToken Is Not Null/Empty: {GitHubToken.IsNotNullOrEmpty()}");

            Log.Information($"Token In GitHubActions Exists: {GitHubActions.Instance.Token.IsNotNullOrEmpty()}");
            // await CreateNewGitHubRelease(ReleaseType.Preview);
        });

    async Task ValidateBranchForStatusCheck()
    {
        var github = GitHubActions.Instance;
        var validBranch = false;
        var branch = string.Empty;

        // This is if the workflow is execution locally or manually in GitHub using workflow_dispatch
        bool ValidBranchForManualExecution()
        {
            return Repo.Branch.IsMasterBranch() ||
                   Repo.Branch.IsDevelopBranch() ||
                   Repo.Branch.IsFeatureBranch() ||
                   Repo.Branch.IsPreviewFeatureBranch() ||
                   Repo.Branch.IsPreviewBranch() ||
                   Repo.Branch.IsReleaseBranch() ||
                   Repo.Branch.IsHotFixBranch();
        }

        // If the build is on the server and the GitHubActions object exists
        if (IsServerBuild && github is not null)
        {
            validBranch = github.IsPullRequest
                ? github.BaseRef.IsPreviewBranch() || github.BaseRef.IsReleaseBranch() ||
                  github.BaseRef.IsDevelopBranch() || github.BaseRef.IsMasterBranch()
                : ValidBranchForManualExecution();

            branch = github.IsPullRequest ? github.BaseRef : Repo.Branch;
        }
        else if (IsLocalBuild || github is null)
        {
            validBranch = ValidBranchForManualExecution();
            branch = Repo.Branch;
        }

        if (validBranch)
        {
            var validIssueNumber = await ValidBranchIssueNumber(branch);

            validBranch = validIssueNumber;

            if (validIssueNumber is false)
            {
                Log.Error($"The issue number '{ParseIssueNumber(branch)}' in branch '{branch}' does not exist.");
            }
        }

        if (validBranch is false)
        {
            Assert.Fail($"The branch '{branch}' is invalid.");
        }
    }

    async Task<bool> ValidBranchIssueNumber(string branch)
    {
        // If the branch is not a branch with an issue number, return as valid
        if (!branch.IsFeatureBranch() && !branch.IsPreviewFeatureBranch() && !branch.IsHotFixBranch())
        {
            return true;
        }

        var issueNumber = ParseIssueNumber(branch);

        var issueClient = GitHubClient.Issue;

        return await issueClient.IssueExists(Owner, MainProjName, issueNumber);
    }

    int ParseIssueNumber(string branch)
    {
        if (string.IsNullOrEmpty(branch))
        {
            return 0;
        }

        if (branch.IsFeatureBranch())
        {
            // feature/123-my-branch
            var mainSections = branch.Split("/");
            var number = mainSections[1].Split('-')[0];
            return int.Parse(number);
        }

        if (branch.IsPreviewFeatureBranch())
        {
            // preview/feature/123-my-preview-branch
            var mainSections = branch.Split("/");
            var number = mainSections[2].Split('-')[0];
            return int.Parse(number);
        }

        if (branch.IsHotFixBranch())
        {
            // hotfix/123-my-hotfix
            var mainSections = branch.Split("/");
            var number = mainSections[1].Split('-')[0];
            return int.Parse(number);
        }

        return 0;
    }

    // TODO: Create release status check to verify that a tag does not exist already.
        // Used by prev and prod releases.  An already created tag could interupt the release process.  Creating a release process

    // TODO: Add validation to release and preview release branches that a deployment of that version does not exist already
        // Example: the release branch contains the version 'v1.2.3'.  Verify that a
}


