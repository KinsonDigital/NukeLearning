using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Tools.GitHub;
using Serilog;

namespace NukeLearningCICD;

public partial class CICD // StatusChecks
{
    Target BuildStatusCheck => _ => _
        .Before(BuildAllProjects, BuildMainProject, BuildTestProject)
        .Triggers(BuildAllProjects)
        .Executes(() =>
        {
            Log.Information("✅Starting Build Status Check - Executing {Value} Target", nameof(BuildAllProjects));

            PrintPullRequestInfo();
            ValidateBranchForStatusCheck();

            Log.Information("Branch Is Valid!!");
        });


    Target UnitTestStatusCheck => _ => _
        .Before(RunAllUnitTests, RunUnitTests)
        .Triggers(RunAllUnitTests)
        .Executes(async () =>
        {
            Log.Information("✅Starting Unit Test Status Check - Executing {Value} Target", nameof(RunAllUnitTests));

            PrintPullRequestInfo();
            await ValidateBranchForStatusCheck();

            Log.Information("Branch Is Valid!!");
        });

    Target DebugTask => _ => _
        .Executes(async () =>
        {
            var result = await ValidBranchIssueNumber("feature/3-test-branch");
        });

    void PrintValidBranchesForManuallyExecution()
    {
        var validBranches = new[]
        {
            "master",
            "develop",
            "feature/#-*",
            "preview/feature/#-*",
            "preview/v#.#.#-preview.#",
            "release/v#.#.#",
            "hotfix/#-*",
        };
        Log.Information("✔️Validating correct branch . . .");
        Log.Information("Valid Branches:");

        foreach (var branch in validBranches)
        {
            Log.Information($"\t  {branch}");
        }
    }

    public async Task ValidateBranchForStatusCheck()
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
                   Repo.Branch.IsPreviewReleaseBranch() ||
                   Repo.Branch.IsReleaseBranch() ||
                   Repo.Branch.IsHotFixBranch();
        }

        // If the build is on the server and the GitHubActions object exists
        if (IsServerBuild && github is not null)
        {
            validBranch = github.IsPullRequest
                ? github.BaseRef.IsPreviewReleaseBranch() || github.BaseRef.IsReleaseBranch() ||
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

            if (validIssueNumber)
            {
                return;
            }
        }

        var statusCheckType = GitHubActions.Instance is not null && GitHubActions.Instance.IsPullRequest
            ? "a pull request"
            : "manual execution";
        var errorMsg = $"The branch '{{Value}}' is not a valid branch to run a build status check on for {statusCheckType}.";
        Log.Error(errorMsg, branch);
        Assert.Fail("The destination branch for the pull request does not have the correct syntax.");
    }

    async Task<bool> ValidBranchIssueNumber(string branch)
    {
        // If the branch is a branch that contains an issue number
        if (branch.IsFeatureBranch() || branch.IsPreviewFeatureBranch() || branch.IsHotFixBranch())
        {
            var issueNumber = -1;

            if (branch.IsFeatureBranch())
            {
                // feature/123-my-branch
                var mainSections = branch.Split("/");
                var number = mainSections[1].Split('-')[0];
                issueNumber = int.Parse(number);
            }
            else if (branch.IsPreviewFeatureBranch())
            {
                // preview/feature/123-my-preview-branch
                var mainSections = branch.Split("/");
                var number = mainSections[2].Split('-')[0];
                issueNumber = int.Parse(number);
            }
            else if (branch.IsHotFixBranch())
            {
                // hotfix/123-my-hotfix
                var mainSections = branch.Split("/");
                var number = mainSections[1].Split('-')[0];
                issueNumber = int.Parse(number);
            }

            var issueClient = GitHubClient.Issue;

            return await issueClient.IssueExists("KinsonDigital", "NukeLearning", issueNumber);
        }

        return true;
    }





    // TODO: Create status check to verify that the number in a branch is a real issue number
        // The issue number must be linked to a pull request
        // All pull requests linked must not be merged.  (Some open PR are ok.  Just not all closed)

    // TODO: Create status check to verify that an open milestone exists with a name that matches the current version pulled from the csproj

    // TODO: Create status check for release branches only where the milestone must have all of its issues closed and PR's merged/closed
        // 👉🏼https://github.com/nuke-build/nuke/blob/develop/build/Build.GitFlow.cs

    // TODO: Add status check to check that a nuget package of a particular version does not already exist
        // This is for preview and production releases

    // TODO: Create target status check to check that the release notes exist
        // This will be for preview and production releases
}
