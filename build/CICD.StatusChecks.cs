using System;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Serilog;

namespace NukeLearningCICD;

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


    Target FeaturePRStatusCheck => _ => _
        .Requires(
            () => ThatThisIsPullRequestRun(nameof(FeaturePRStatusCheck), RunType.StatusCheck),
            () => ThatThePRSourceBranchIsValid(BranchType.Feature),
            () => ThatFeaturePRIssueNumberExists(),
            () => ThatFeaturePRIssueHasLabel(BranchType.Feature),
            () => ThatThePRTargetBranchIsValid(BranchType.Develop),
            () => ThatThePRHasBeenAssigned(),
            () => ThatPRHasLabels()
        );


    // TODO: Create HotFix PR status check

    Target PreviewFeaturePRStatusCheck => _ => _
        .Requires(
            () => ThatThisIsPullRequestRun(nameof(PreviewFeaturePRStatusCheck), RunType.StatusCheck),
            () => ThatThePRSourceBranchIsValid(BranchType.PreviewFeature),
            () => ThatPreviewFeaturePRIssueNumberExists(),
            () => ThatFeaturePRIssueHasLabel(BranchType.PreviewFeature),
            () => ThatThePRTargetBranchIsValid(BranchType.Preview),
            () => ThatThePRHasBeenAssigned(),
            () => ThatPRHasLabels()
        );


    Target HotFixPRStatusCheck => _ => _
        .Requires(
            () => ThatThisIsPullRequestRun(nameof(HotFixPRStatusCheck), RunType.StatusCheck),
            () => ThatThePRSourceBranchIsValid(BranchType.HotFix),
            () => ThatPreviewFeaturePRIssueNumberExists(),
            () => ThatFeaturePRIssueHasLabel(BranchType.HotFix),
            () => ThatThePRTargetBranchIsValid(BranchType.Master),
            () => ThatThePRHasBeenAssigned(),
            () => ThatPRHasLabels()
        );


    Target PrevReleasePRStatusCheck => _ => _
        .Requires(
            () => ThatThisIsPullRequestRun(nameof(PrevReleasePRStatusCheck), RunType.StatusCheck),
            () => ThatThePRSourceBranchIsValid(BranchType.Preview),
            () => ThatThePRTargetBranchIsValid(BranchType.Release),
            () => ThatThePRHasBeenAssigned(),
            () => ThatThePRHasTheLabel("🚀Preview Release"),
            () => ThatTheProjectVersionsAreValid(ReleaseType.Preview),
            () => ThatThePreviewPRBranchVersionsMatch(ReleaseType.Preview),
            () => ThatThePRSourceBranchVersionSectionMatchesProjectVersion(ReleaseType.Preview),
            () => ThatTheReleaseMilestoneExists(),
            () => ThatTheReleaseMilestoneContainsIssues(),
            () => ThatTheReleaseTagDoesNotAlreadyExist(ReleaseType.Preview),
            () => ThatAllMilestoneIssuesHaveLabels(),
            () => ThatAllOfTheReleaseMilestoneIssuesAreClosed(ReleaseType.Preview, true),
            () => ThatTheReleaseMilestoneOnlyContainsSingleReleaseToDoIssue(ReleaseType.Preview),
            () => ThatTheReleaseMilestoneOnlyContainsSingleReleasePR(ReleaseType.Preview),
            () => ThatTheReleaseNotesExist(ReleaseType.Preview),
            () => ThatMilestoneIssuesExistInReleaseNotes(ReleaseType.Preview),
            () => ThatGitHubReleaseDoesNotExist(ReleaseType.Preview),
            () => NugetPackageDoesNotExist()
        );


    Target ProdReleasePRStatusCheck => _ => _
        .Requires(
            () => ThatThisIsPullRequestRun(nameof(PrevReleasePRStatusCheck), RunType.StatusCheck),
            () => ThatThePRSourceBranchIsValid(BranchType.Release),
            () => ThatThePRTargetBranchIsValid(BranchType.Master),
            () => ThatThePRHasBeenAssigned(),
            () => ThatThePRHasTheLabel("🚀Production Release"),
            () => ThatTheProjectVersionsAreValid(ReleaseType.Production),
            () => ThatThePreviewPRBranchVersionsMatch(ReleaseType.Production),
            () => ThatThePRSourceBranchVersionSectionMatchesProjectVersion(ReleaseType.Production),
            () => ThatTheReleaseMilestoneExists(),
            () => ThatTheReleaseMilestoneContainsIssues(),
            () => ThatTheReleaseTagDoesNotAlreadyExist(ReleaseType.Production),
            () => ThatAllMilestoneIssuesHaveLabels(),
            () => ThatAllOfTheReleaseMilestoneIssuesAreClosed(ReleaseType.Production, true),
            () => ThatTheReleaseMilestoneOnlyContainsSingleReleaseToDoIssue(ReleaseType.Production),
            () => ThatTheReleaseMilestoneOnlyContainsSingleReleasePR(ReleaseType.Production),
            () => ThatTheReleaseNotesExist(ReleaseType.Production),
            () => ThatMilestoneIssuesExistInReleaseNotes(ReleaseType.Production),
            () => ThatGitHubReleaseDoesNotExist(ReleaseType.Production),
            () => NugetPackageDoesNotExist()
        );


    Target ValidVersionStatusCheck => _ => _
        .Requires(() => GetTargetBranch().IsMasterBranch() || GetTargetBranch().IsReleaseBranch())
        .Executes(() =>
        {
            var releaseType = GetTargetBranch().IsMasterBranch()
                ? ReleaseType.Production
                : ReleaseType.Preview;

            var msg = "💡Purpose: Verifies that all of the versions exist in the csproj file";
            msg += $"{ConsoleTab}and that the version syntax is correct.";

            Log.Information(msg);
            Console.WriteLine();
            Log.Information("✅Starting Status Check . . .");
            Log.Information("Executing On Branch: {Value}", GetTargetBranch());
            Log.Information("Type Of Release: {Value}", releaseType);

            var branch = GetTargetBranch();

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

            Assert.Fail($"The branch must be a 'master' or 'release/v#.#.#' branch, but was executed on the '{GetTargetBranch()}' branch.");
        });


    Target NoGitHubReleaseStatusCheck => _ => _
        .Requires(() => GetTargetBranch().IsMasterBranch() || GetTargetBranch().IsReleaseBranch())
        .Executes(async () =>
        {
            var project = Solution.GetProject(MainProjName);
            var version = $"v{(project is null ? string.Empty : project.GetVersion())}";
            var releaseType = GetTargetBranch().IsMasterBranch()
                ? ReleaseType.Production.ToString().ToLower()
                : ReleaseType.Preview.ToString().ToLower();

            var msg = $"💡Purpose: Verifies that no GitHub release already exists for the current version.";
            msg += $"{ConsoleTab}This status check is only intended to be executed for preview and production releases.";

            Log.Information(msg);
            Console.WriteLine();
            Log.Information("✅Starting Status Check . . .");
            Log.Information("Current Version: {Value}", version);
            Log.Information("Executing On Branch: {Value}", GetTargetBranch());
            Log.Information("Type Of Release: {Value}", releaseType);

            var releaseExists = await GitHubClient.Repository.Release.ReleaseExists(Owner, MainProjName, version);

            if (releaseExists)
            {
                var errorMsg = "A release for version '{Value}' already exist.";
                errorMsg += $"{ConsoleTab}Did you forget to update the version values in the csproj file?";
                Log.Error(errorMsg, version);
                Assert.Fail($"A release for version '{version}' already exists.");
            }
        });


    Target ReleaseNotesExistStatusCheck => _ => _
        .Requires(() => GetTargetBranch().IsMasterBranch() || GetTargetBranch().IsReleaseBranch())
        .Executes(() =>
        {
            var project = Solution.GetProject(MainProjName);
            var version = $"v{(string.IsNullOrEmpty(project) ? string.Empty : project.GetVersion())}";
            var releaseType = GetTargetBranch().IsMasterBranch()
                ? ReleaseType.Production
                : ReleaseType.Preview;

            var releaseTypeStr = releaseType.ToString().ToLower();

            var msg = $"💡Purpose: Verifies that the {releaseTypeStr} release notes exist for the current version.";
            msg += $"{ConsoleTab}This status check is only meant to be intended for preview and production releases.";

            Log.Information(msg);
            Console.WriteLine();
            Log.Information("✅Starting Status Check . . .");
            Log.Information("Current Version: {Value}", version);
            Log.Information("Executing On Branch: {Value}", GetTargetBranch());
            Log.Information("Type Of Release: {Value}", releaseTypeStr);

            if (ReleaseNotesExist(releaseType, version) is false)
            {
                var errorMsg = $"The {releaseTypeStr} release notes for version '{version}' could not be found.";
                Log.Error(errorMsg);
                Assert.Fail($"The {releaseTypeStr} release notes could not be found.");
            }
        });


    Target MilestoneExistsStatusCheck => _ => _
        .Requires(() => GetTargetBranch().IsMasterBranch() || GetTargetBranch().IsReleaseBranch())
        .Executes(async () =>
        {
            var project = Solution.GetProject(MainProjName);
            var version = $"v{(string.IsNullOrEmpty(project) ? string.Empty : project.GetVersion())}";
            var releaseType = GetTargetBranch().IsMasterBranch()
                ? ReleaseType.Production.ToString().ToLower()
                : ReleaseType.Preview.ToString().ToLower();

            var msg = $"💡Purpose: Verifies that the GitHub {releaseType} milestone exists.";
            msg += $"{ConsoleTab}A milestone must exist and contain issues before a release can be performed.";

            Log.Information(msg);
            Console.WriteLine();
            Log.Information("✅Starting Status Check . . .");
            Log.Information("Current Version: {Value}", version);
            Log.Information("Executing On Branch: {Value}", GetTargetBranch());
            Log.Information("Type Of Release: {Value}", releaseType);

            var milestoneClient = GitHubClient.Issue.Milestone;

            if (await milestoneClient.MilestoneExists(Owner, MainProjName, version) is false)
            {
                const string newMilestoneUrl = $"https://github.com/{Owner}/{MainProjName}/milestones/new";
                var errorMsg = "The milestone '{Value1}' does not exist.";
                errorMsg += $"{ConsoleTab}To create a milestone, go to this URL here 👉🏼 {{Value2}}";
                Log.Error(errorMsg, version, newMilestoneUrl);
                Assert.Fail($"Milestone {version} does not exist.");
            }
        });


    Target MilestoneStateStatusCheck => _ => _
        .Requires(() => GetTargetBranch().IsMasterBranch() || GetTargetBranch().IsReleaseBranch())
        .Executes(async () =>
        {
            var project = Solution.GetProject(MainProjName);
            var version = $"v{(string.IsNullOrEmpty(project) ? string.Empty : project.GetVersion())}";
            var releaseType = GetTargetBranch().IsMasterBranch()
                ? ReleaseType.Production.ToString().ToLower()
                : ReleaseType.Preview.ToString().ToLower();

            var msg = $"💡Purpose: Verifies that the GitHub {releaseType} milestone is in the correct state.";
            msg += $"{ConsoleTab}This correct state means that all of the issues are closed and pull requests are merged.";

            Log.Information(msg);
            Console.WriteLine();
            Log.Information("✅Starting Status Check . . .");
            Log.Information("Current Version: {Value}", version);
            Log.Information("Executing On Branch: {Value}", GetTargetBranch());
            Log.Information("Type Of Release: {Value}", releaseType);

            var milestoneClient = GitHubClient.Issue.Milestone;
            var milestone = await milestoneClient.GetByTitle(Owner, MainProjName, version);

            if (milestone is null)
            {
                const string newMilestoneUrl = $"https://github.com/{Owner}/{MainProjName}/milestones/new";
                const string errorMsg = "The milestone '{{Value1}}' does not exist.  To create a new milestone, go here 👉🏼 {Value2}";
                Log.Error(errorMsg, version, newMilestoneUrl);
                Assert.Fail($"Could not find the milestone '{version}' to analyze its state.");
            }
            else
            {
                // If all of the issues are not closed
                if (milestone.OpenIssues > 0)
                {
                    var errorMsg = $"Some issues are still open for milestone '{version}'.";
                    errorMsg += $"{ConsoleTab}Please close all open issues before attempting a release.";
                    errorMsg += $"{ConsoleTab}Goto the milestone here 👉🏼 {milestone.HtmlUrl}";
                    Log.Error(errorMsg);
                    Assert.Fail($"Milestone {version} still contains open issues.");
                }
                else
                {
                    var errorMsg = $"No issues and pull requests have been added to the milestone '{version}'.";
                    errorMsg += $"{ConsoleTab}Please add all issues and pull requests to the milestone.";
                    Log.Error(errorMsg);
                    Assert.Fail($"Milestone {version} does not contain any issues.");
                }
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
                errorMsg += $"{ConsoleTab}The tag is auto created upon release.";
                Log.Error(errorMsg);
                Assert.Fail($"The tag '{version}' already exists.");
            }
        });


    Target ValidPreviewBranchVersionStatusCheck => _ => _
        .Requires(() => GetTargetBranch().IsReleaseBranch())
        .Executes(async () =>
        {
            // TODO: Empty.  Delete or do something with this
        });


    Target DebugTask => _ => _
        .Requires(
            () => ThatTheReleaseMilestoneOnlyContainsSingleReleasePR(ReleaseType.Preview)
        )
        .Executes(async () =>
        {
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
            validBranch = IsPullRequest()
                ? github.BaseRef.IsPreviewBranch() || github.BaseRef.IsReleaseBranch() ||
                  github.BaseRef.IsDevelopBranch() || github.BaseRef.IsMasterBranch()
                : ValidBranchForManualExecution();

            branch = IsPullRequest() ? github.BaseRef : Repo.Branch;
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

    // TODO: Check to see if the 'tasks' in an issue can be seen in the returned JSON data, or with the Octokit issue object
    // If so, we can check to make sure that all checkboxes (tasks) are complete in all of the issues in a milestone
}
