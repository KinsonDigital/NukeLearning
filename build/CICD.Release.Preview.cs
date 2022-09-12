using Nuke.Common;
using Nuke.Common.Tools.GitHub;
using Serilog;

namespace NukeLearningCICD;

public partial class CICD // Release.Preview
{
    Target RunPreviewRelease => _ => _
        .Requires(() => Repo.IsOnPreviewReleaseBranch())
        .Requires(() => ProjVersionExists())
        .Requires(() => ProjFileVersionExists())
        .Requires(() => ProjAssemblyVersionExists())
        .Before(BuildAllProjects, RunAllUnitTests)
        .Triggers(BuildAllProjects, RunAllUnitTests)
        .Executes(async () =>
        {
            /* TODO: Create method and invoke here to print out all kinds of repo and release info
                1. Use Group and EndGroup to group the information

                2. Add the info below:
                    GitHubActions.Instance.Action
                    GitHubActions.Instance.Actor
                    GitHubActions.Instance.Sha
                    GitHubActions.Instance.Workflow
            */

            ValidateVersions("#.#.#-preview.#");
            var project = Solution.GetProject(MainProjName);

            var version = project.GetVersion();
            Log.Information($"🚀Starting preview release process for version 'v{version}'");

            var repoClient = GitHubTasks.GitHubClient.Repository;

            var tagDoesNotExistResult = await repoClient.TagDoesNotExist(
                Repo.GetGitHubOwner(),
                Repo.GetGitHubName(),
                $"v{version}");
            if (tagDoesNotExistResult is false)
            {
                Assert.Fail($"A tag with the value 'v{version}' already exists.");
            }

            // TODO: Close the milestone for the preview release after everything has been successful
        });
}
