using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
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
            ValidateBranch();

            Log.Information("Branch Is Valid!!");
        });


    Target UnitTestStatusCheck => _ => _
        .Before(RunAllUnitTests, RunUnitTests)
        .Triggers(RunAllUnitTests)
        .Executes(() =>
        {
            Log.Information("✅Starting Unit Test Status Check - Executing {Value} Target", nameof(RunAllUnitTests));

            PrintPullRequestInfo();
            ValidateBranch();

            Log.Information("Branch Is Valid!!");
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

    void ValidateBranch()
    {
        // If the build is on the server and the GitHubActions object exists
        if (IsServerBuild && GitHubActions.Instance is not null)
        {
            var github = GitHubActions.Instance;

            if (github.IsPullRequest)
            {
                var isValidDestinationBranch =
                    github.BaseRef.IsPreviewReleaseBranch() ||
                    github.BaseRef.IsReleaseBranch() ||
                    github.BaseRef.IsDevelopBranch() ||
                    github.BaseRef.IsMasterBranch();

                if (isValidDestinationBranch)
                {
                    return;
                }

                const string errorMsg = "The branch '{Value}' is not a valid branch to run a build status check on for a pull request.";
                Log.Error(errorMsg, github.BaseRef);
                Assert.Fail("The destination branch for the pull request does not have the correct syntax.");
            }
            else if(github.IsManuallyExecuted())
            {
                ValidateBranchForManualExecution();
            }
        }
        else if (IsLocalBuild || GitHubActions.Instance is null)
        {
            ValidateBranchForManualExecution();
        }
    }

    void ValidateBranchForManualExecution()
    {
        PrintValidBranchesForManuallyExecution();

        if (string.IsNullOrEmpty(Repo.Branch))
        {
            Assert.Fail("Branch null or empty.  Possible detached HEAD?");
            return;
        }

        var isValidDestinationBranch =
            Repo.Branch.IsMasterBranch() ||
            Repo.Branch.IsDevelopBranch() ||
            Repo.Branch.IsFeatureBranch() ||
            Repo.Branch.IsPreviewFeatureBranch() ||
            Repo.Branch.IsPreviewReleaseBranch() ||
            Repo.Branch.IsReleaseBranch() ||
            Repo.Branch.IsHotFixBranch();

        if (isValidDestinationBranch)
        {
            return;
        }

        const string errorMsg = "The branch '{Value}' is not a valid branch to run a build status check on for manual execution.";
        Log.Error(errorMsg, Repo.Branch);
        Assert.Fail("The destination branch does not have the correct syntax.");
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
