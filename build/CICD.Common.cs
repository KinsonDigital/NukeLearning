using System;
using System.IO;
using System.Linq;
using System.Text;
using GlobExpressions;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
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
        .Requires(() => NugetApiKey)
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
                .SetApiKey(NugetApiKey));
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

    Target DebugTarget => _ => _
        .Requires(() => ProjVersionExists())
        .Requires(() => ProjAssemblyVersionExists())
        .Executes(() =>
        {
            ReleaseNotesExist();
        });



    bool ReleaseNotesExist()
    {


        return false;
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
            Log.Information("Is PR: {Value}", gitHub.IsPullRequest);
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

    string GetBranch()
    {
        if (IsServerBuild && GitHubActions.Instance is not null)
        {
            return GitHubActions.Instance.IsPullRequest
                ? GitHubActions.Instance.BaseRef
                : Repo.Branch;
        }

        if (IsLocalBuild || GitHubActions.Instance is null)
        {
            return Repo.Branch;
        }

        Assert.Fail("Could not get the correct branch.");
        return string.Empty;
    }


    // TODO: Create target to create GitHub release
        // This must add release notes, set title correctly, attach assets, etc.

    // TODO: Add target or requirement to check if a version tag already exists
}
