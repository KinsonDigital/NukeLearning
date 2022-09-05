using System;
using System.Text;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.GitHub;
using Serilog;

namespace NukeLearningCICD;

public partial class CICD // Release.Preview
{
    Target RunPreviewRelease => _ => _
        .Requires(() => Repo.IsOnPreviewReleaseBranch())
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

            ValidateVersions();
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
        });

    // TODO: Create target to pack nuget package to preview for release to nuget.org
    // TODO: Create target to release the nuget package to nuget.org

    void ValidateVersions()
    {
        var project = Solution.GetProject(MainProjName);

        Log.Information("✔️ Validating all csproj version exist . . .");
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
        var correctVersionSyntax = project.HasCorrectVersionSyntax("#.#.#-preview.#");

        if (correctVersionSyntax is false)
        {
            var failMsg = $"The syntax for the '<Version/>' value '{project.GetVersion()}' is incorrect.";
            failMsg += $"{Environment.NewLine}Expected Syntax: '#.#.#-preview.#'";

            Log.Error(failMsg);
            Assert.Fail("The csproj file '<Version/>' value syntax is incorrect.");
        }

        Log.Information("✔️ Validating that the file version syntax is valid . . .");
        var correctFileVersionSyntax = project.HasCorrectFileVersionSyntax("#.#.#-preview.#");
        if (correctFileVersionSyntax is false)
        {
            var failMsg = $"The syntax for the '<FileVersion/>' value '{project.GetFileVersion()}' is incorrect.";
            failMsg += $"{Environment.NewLine}Expected Syntax: '#.#.#-preview.#'";

            Log.Error(failMsg);
            Assert.Fail("The csproj file '<FileVersion/>' value syntax is incorrect.");
        }

        Log.Information("✔️ Validating that the assembly version syntax is valid . . .");
        var correctAssemblyVersionSyntax = project.HasCorrectAssemblyVersionSyntax("#.#.#");
        if (correctAssemblyVersionSyntax is false)
        {
            var failMsg = $"The syntax for the '<AssemblyVersion/>' value '{project.GetAssemblyVersion()}' is incorrect.";
            failMsg += $"{Environment.NewLine}Expected Syntax: '#.#.#-preview.#'";

            Log.Error(failMsg);
            Assert.Fail("The csproj file '<AssemblyVersion/>' value syntax is incorrect.");
        }
    }
}
