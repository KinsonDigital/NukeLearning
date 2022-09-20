using System;
using Nuke.Common;
using Serilog;

namespace NukeLearningCICD;

public partial class CICD // Release.Production
{
    Target RunProductionRelease => _ => _
        .Requires(
            () => ThatThisIsExecutedManually(BranchType.Master),
            () => ThatTheCurrentBranchIsCorrect(BranchType.Master),
            () => ThatTheProjectVersionsAreValid(ReleaseType.Production),
            () => ThatTheReleaseTagDoesNotAlreadyExist(ReleaseType.Production),
            () => ThatTheReleaseMilestoneExists(),
            () => ThatTheReleaseMilestoneContainsIssues(),
            () => ThatAllMilestoneIssuesHaveLabels(),
            () => ThatAllMilestonePullRequestsHaveLabels(),
            () => ThatAllOfTheReleaseMilestoneIssuesAreClosed(ReleaseType.Production, false),
            () => ThatAllOfTheReleaseMilestonePullRequestsAreClosed(ReleaseType.Production, false),
            () => ThatTheReleaseMilestoneOnlyContainsSingle(ReleaseType.Production, ItemType.Issue),
            () => ThatTheReleaseMilestoneOnlyContainsSingle(ReleaseType.Production, ItemType.PullRequest),
            () => ThatTheReleaseNotesExist(ReleaseType.Production),
            () => ThatTheReleaseNotesTitleIsCorrect(ReleaseType.Production),
            () => ThatTheProdReleaseNotesContainsPreviewReleaseSection(),
            () => ThatTheProdReleaseNotesContainsPreviewReleaseItems(),
            () => ThatMilestoneIssuesExistInReleaseNotes(ReleaseType.Production),
            () => ThatGitHubReleaseDoesNotExist(ReleaseType.Production),
            () => NugetPackageDoesNotExist()
        )
        .After(BuildAllProjects, RunAllUnitTests)
        .DependsOn(BuildAllProjects, RunAllUnitTests)
        .Executes(async () =>
        {
            var tweetTemplatePath = RootDirectory / ".github" / "ReleaseTweetTemplate.txt";
            var version = Solution.GetProject(MainProjName)?.GetVersion() ?? string.Empty;

            version = version.StartsWith("v")
                ? version
                : $"v{version}";

            if (string.IsNullOrEmpty(version))
            {
                Assert.Fail("Release failed.  Could not get version information.");
            }

            Log.Information($"🚀 Starting production release process for version '{version}' 🚀");

            try
            {
                // Create a GitHub release
                Log.Information("✅Creating new GitHub release . . .");
                var releaseUrl = await CreateNewGitHubRelease(ReleaseType.Production, version);
                var githubReleaseLog = $"The GitHub production release for version '{version}' was successful!!🚀";
                githubReleaseLog += $"{Environment.NewLine}{ConsoleTab}To view the release, go here 👉🏼 {releaseUrl}{Environment.NewLine}";
                Log.Information(githubReleaseLog);

                // Close the milestone
                Log.Information($"✅Closing GitHub milestone '{version}' . . .");
                var milestoneClient = GitHubClient.Issue.Milestone;
                var milestoneResult = await milestoneClient.CloseMilestone(Owner, MainProjName, version);
                var milestoneMsg = $"The GitHub milestone '{version}' as been closed.";
                milestoneMsg += $"{Environment.NewLine}{ConsoleTab}To view the milestone, go here 👉🏼 {milestoneResult.HtmlUrl}{Environment.NewLine}";
                Log.Information(milestoneMsg);

                // Update the milestone description
                Log.Information($"✅Updating description for milestone '{version}' . . .");
                var description = await GetProdMilestoneDescription(version);
                var updatedMilestone = await milestoneClient.UpdateMilestoneDescription(Owner, MainProjName, version, description);
                var updateMsg = $"The GitHub Milestone '{version}' description has been updated.";
                updateMsg += $"{Environment.NewLine}{ConsoleTab}To view the milestone, go here 👉🏼 {updatedMilestone.HtmlUrl}{Environment.NewLine}";
                Log.Information(updateMsg);

                // Create the nuget package to deploy
                var fileName = $"{MainProjName}.{version.TrimStart('v')}.nupkg";
                var nugetPath = $"{NugetOutputPath}/{fileName}"
                    .Replace(RootDirectory, "~")
                    .Replace(@"\", "/");
                Log.Information("✅Creating a nuget package . . .");
                CreateNugetPackage();
                Log.Information($"Nuget package created at location '{nugetPath}'{Environment.NewLine}");

                // // Publish nuget package to nuget.org
                // Log.Information("✅Publishing nuget package to nuget.org . . .");
                // var nugetUrl = $"https://www.nuget.org/packages/{Owner}.{MainProjName}/{version.TrimStart('v')}";
                // PublishNugetPackage();
                // var nugetReleaseLog = "Nuget package published!!🚀";
                // nugetReleaseLog += $"To view the nuget package, go here 👉🏼 {nugetUrl}";
                // Log.Information(nugetReleaseLog);
                //
                // // Tweet about release
                // Log.Information("✅Announcing release on twitter . . .");
                // SendReleaseTweet(tweetTemplatePath, version);
                // Log.Information($"Twitter announcement complete!!{Environment.NewLine}");
                //
                // // Merge the master branch into the develop branch
                // Log.Information("✅Merging 'master' branch into the 'develop' branch . . .");
                // var mergeResultUrl = await MergeBranch("master", "develop");
                // var mergeLog = $"The 'master' branch has been merged into the 'develop' branch.";
                // mergeLog += $"{Environment.NewLine}{ConsoleTab}To view the merge result, go here 👉🏼 {mergeResultUrl}";
                // Log.Information(mergeLog);
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }
        });
}
