using System.Linq;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Tools.GitHub;
using Octokit;
using Serilog;

namespace NukeLearningCICD;

public partial class CICD // StatusChecks
{
    Target BuildStatusCheck => _ => _
        .Before(BuildAllProjects, BuildMainProject, BuildTestProject)
        .Triggers(BuildAllProjects)
        .Executes(async () =>
        {
            Log.Information("✅Starting Build Status Check - Executing {Value} Target", nameof(BuildAllProjects));

            PrintPullRequestInfo();
            await ValidateBranchForStatusCheck();

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


    // TODO: YAML file will filter master, hotfix, and release branches
    Target ValidVersionStatusCheck => _ => _
        .Requires(() => Repo.Branch.IsMasterBranch() || Repo.Branch.IsReleaseBranch())
        .Executes(() =>
        {
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

            Assert.Fail("The branch must be a 'master' or 'release/v#.#.#' branch.");
        });


    Target DebugTask => _ => _
        .Executes(async () =>
        {
            var milestoneClient = GitHubClient.Issue.Milestone;

            // var milestone = (from m in await milestoneClient.GetAllForRepository("KinsonDigital", "NukeLearning")
            //     where m.State == ItemState.Open && m.Title == "v.1.2.3"
            //     select m).ToArray();


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

        return await issueClient.IssueExists("KinsonDigital", "NukeLearning", issueNumber);
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


    // TODO: Add validation to release and preview release branches that a deployment of that version does not exist already

    // TODO: Create release status check where the milestone must exist, have all of its issues closed, and PR's merged/closed
        // Messages will exist for each incorrect milestone state

    // TODO: Create release status check that pulls the version from the csproj and validates its syntax
        // Release branches only
        // Account for manual and PR executions

    // TODO: Create release status check that pulls the version from the csproj and checks if the nuget package does not already exist
        // Release branches only
        // Account for manual and PR executions

    // TODO: Add status check to check that a nuget package of a particular version does not already exist
        // This is for preview and production releases

    // TODO: Create release status check to check that the release notes file exists
        // This will be for preview and production releases
        // Account for manual and PR executions
}


