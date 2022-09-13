using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Octokit;
using Serilog;
using static Nuke.Common.Tools.Git.GitTasks;
using NukeProject = Nuke.Common.ProjectModel.Project;

namespace NukeLearningCICD;

public static class ExtensionMethods
{
    private static readonly char[] Digits = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', };
    private const char MatchNumbers = '#';
    private const char MatchAnything = '*';

    public static bool IsNotNullOrEmpty(this string value) => !string.IsNullOrEmpty(value);

    public static bool ContainsNumbers(this string value) => Digits.Any(value.Contains);

    public static bool DoesNotContainNumbers(this string value)
        => !ContainsNumbers(value);

    public static bool IsOnCorrectBranch(this GitRepository repo, string branchPattern)
    {
        if (repo.Branch is null)
        {
            return false;
        }

        return EqualTo(repo.Branch, branchPattern);
    }

    public static bool IsCorrectBranch(this string branch, string branchPattern) => EqualTo(branch, branchPattern);

    public static bool IsMasterBranch(this string branch) => branch == "master";

    public static bool IsDevelopBranch(this string branch) => branch == "develop";

    public static bool IsOnFeatureBranch(this GitRepository repo)
    {
        const string namingSyntax = "feature/#-*";
        var errorMsg = "The feature branch '{Value}' does not follow the correct naming syntax.";
        errorMsg += $"{Environment.NewLine}Use the naming syntax '{namingSyntax}' for feature branches.";
        errorMsg += "Example: feature/123-my-feature-branch";

        var result = IsOnCorrectBranch(repo, namingSyntax);

        if (result is false)
        {
            Log.Error(errorMsg, repo.Branch);
        }

        return result;
    }

    public static bool IsFeatureBranch(this string branch) => IsCorrectBranch(branch, "feature/#-*");

    public static bool IsOnPreviewReleaseBranch(this GitRepository repo)
    {
        const string namingSyntax = "preview/v*.*.*-preview.#";
        var errorMsg = "The preview release branch '{Value}' does not follow the correct naming syntax.";
        errorMsg += $"{Environment.NewLine}Use the naming syntax '{namingSyntax}' for feature branches.";
        errorMsg += "Example: preview/v1.2.3-preview.4";

        var result = IsOnCorrectBranch(repo, namingSyntax);

        if (result is false)
        {
            Log.Error(errorMsg, repo.Branch);
        }

        return result;
    }

    public static bool IsPreviewBranch(this string branch) => IsCorrectBranch(branch, "preview/v*.*.*-preview.#");

    public static bool IsOnReleaseBranch(this GitRepository repo)
    {
        const string namingSyntax = "release/v*.*.*";
        var errorMsg = "The release branch '{Value}' does not follow the correct naming syntax.";
        errorMsg += $"{Environment.NewLine}Use the naming syntax '{namingSyntax}' for feature branches.";
        errorMsg += "Example: release/v1.2.3";

        var result = IsOnCorrectBranch(repo, namingSyntax);

        if (result is false)
        {
            Log.Error(errorMsg, repo.Branch);
        }

        return result;
    }

    public static bool IsReleaseBranch(this string branch) => IsCorrectBranch(branch, "release/v*.*.*");

    public static bool IsOnPreviewFeatureBranch(this GitRepository repo)
    {
        const string namingSyntax = "preview/feature/#-*";
        var errorMsg = "The preview feature branch '{Value}' does not follow the correct naming syntax.";
        errorMsg += $" {Environment.NewLine}Use the naming syntax '{namingSyntax}' for feature branches.";
        errorMsg += "Example: preview/feature/123-my-preview-branch";

        var result = IsOnCorrectBranch(repo, namingSyntax);

        if (result is false)
        {
            Log.Error(errorMsg, repo.Branch);
        }

        return result;
    }

    public static bool IsPreviewFeatureBranch(this string branch) => IsCorrectBranch(branch, "preview/feature/#-*");

    public static bool IsOnHotFixBranch(this GitRepository repo)
    {
        const string namingSyntax = "hotfix/#-*";
        var errorMsg = "The hotfix branch '{Value}' does not follow the correct naming syntax.";
        errorMsg += $" {Environment.NewLine}Use the naming syntax '{namingSyntax}' for feature branches.";
        errorMsg += "Example: hotfix/123-my-hotfix-branch";

        var result = IsOnCorrectBranch(repo, namingSyntax);

        if (result is false)
        {
            Log.Error(errorMsg, repo.Branch);
        }

        return result;
    }

    public static void CreateTag(this GitRepository _, string tag)
    {
        Git($"tag {tag}");
        Git($"push origin {tag}");
    }

    public static bool IsHotFixBranch(this string branch) => IsCorrectBranch(branch, "hotfix/#-*");

    public static bool HasCorrectVersionSyntax(this NukeProject project, string versionPattern)
    {
        var currentVersion = project.GetVersion();

        return EqualTo(currentVersion, versionPattern);
    }

    public static bool HasCorrectFileVersionSyntax(this NukeProject project, string versionPattern)
    {
        var currentVersion = project.GetFileVersion();

        return EqualTo(currentVersion, versionPattern);
    }

    public static bool HasCorrectAssemblyVersionSyntax(this NukeProject project, string versionPattern)
    {
        var currentVersion = project.GetAssemblyVersion();

        return EqualTo(currentVersion, versionPattern);
    }

    public static bool AllVersionsExist(this NukeProject project)
    {
        return project.VersionExists() && project.FileVersionExists() && project.AssemblyVersionExists();
    }

    public static bool VersionExists(this NukeProject project)
    {
        return !string.IsNullOrEmpty(project.GetProperty("Version"));
    }

    public static bool FileVersionExists(this NukeProject project)
    {
        return !string.IsNullOrEmpty(project.GetProperty("FileVersion"));
    }

    public static bool AssemblyVersionExists(this NukeProject project)
    {
        return !string.IsNullOrEmpty(project.GetProperty("AssemblyVersion"));
    }

    public static string GetVersion(this NukeProject project)
    {
        var version = project.GetProperty("Version");

        if (string.IsNullOrEmpty(version))
        {
            // TODO: Create custom exception name MissingVersionException
            // TODO: In the exception, explain how to set the version
            throw new Exception($"The version for project '{project.Name}' is not set.");
        }

        return version;
    }

    public static string GetFileVersion(this NukeProject project)
    {
        var version = project.GetProperty("FileVersion");

        if (string.IsNullOrEmpty(version))
        {
            // TODO: Create custom exception name MissingFileVersionException
            // TODO: In the exception, explain how to set the version
            throw new Exception($"The file version for project '{project.Name}' is not set.");
        }

        return version;
    }

    public static string GetAssemblyVersion(this NukeProject project)
    {
        var version = project.GetProperty("AssemblyVersion");

        if (string.IsNullOrEmpty(version))
        {
            // TODO: Create custom exception name MissingAssemblyVersionException
            // TODO: In the exception, explain how to set the version
            throw new Exception($"The assembly version for project '{project.Name}' is not set.");
        }

        return version;
    }

    public static async Task<bool> TagExists(
        this IRepositoriesClient client,
        string repoOwner,
        string repoName,
        string tag)
    {
        var tags = await client.GetAllTags(repoOwner, repoName);

        var foundTag = (from t in tags
            where t.Name == tag
            select t).FirstOrDefault();

        return foundTag is not null;
    }

    public static async Task<bool> LabelExists(
        this IIssuesLabelsClient client,
        string repoOwner,
        string repoName,
        int issueNumber,
        string labelName)
    {
        var issueLabels = await client.GetAllForIssue(repoOwner, repoName, issueNumber);

        return issueLabels.Any(l => l.Name == labelName);
    }

    public static async Task<bool> LabelExists(
        this IPullRequestsClient client,
        string repoOwner,
        string repoName,
        int prNumber,
        string labelName)
    {
        var pr = await client.Get(repoOwner, repoName, prNumber);

        return pr.Labels.Any(l => l.Name == labelName);
    }

    public static async Task<bool> TagDoesNotExist(
        this IRepositoriesClient client,
        string repoOwner,
        string repoName,
        string tag)
    {
        return !await TagExists(client, repoOwner, repoName, tag);
    }

    public static bool IsManuallyExecuted(this GitHubActions gitHubActions)
        => gitHubActions.IsPullRequest is false && gitHubActions.EventName == "workflow_dispatch";

    public static async Task<bool> IssueExists(
        this IIssuesClient client,
        string owner,
        string name,
        int issueNumber)
    {
        try
        {
            var result = await client.Get(owner, name, issueNumber);

            return result.PullRequest is null;
        }
        catch (NotFoundException e)
        {
            return false;
        }

        return true;
    }

    public static async Task<bool> HasReviewers(
        this IPullRequestsClient client,
        string owner,
        string name,
        int prNumber)
    {
        try
        {
            var pr = await client.Get(owner, name, prNumber);

            return pr is not null && pr.RequestedReviewers.Count >= 1;
        }
        catch (NotFoundException e)
        {
            return false;
        }
    }

    public static async Task<bool> HasAssignees(
        this IPullRequestsClient client,
        string owner,
        string name,
        int prNumber)
    {
        try
        {
            var pr = await client.Get(owner, name, prNumber);

            return pr is not null && pr.Assignees.Count >= 1;
        }
        catch (NotFoundException e)
        {
            return false;
        }
    }

    public static async Task<bool> HasLabels(
        this IPullRequestsClient client,
        string owner,
        string name,
        int prNumber)
    {
        try
        {
            var pr = await client.Get(owner, name, prNumber);

            return pr is not null && pr.Labels.Count >= 1;
        }
        catch (NotFoundException e)
        {
            return false;
        }
    }

    public static async Task<bool> ReleaseExists(
        this IReleasesClient client,
        string owner,
        string name,
        string tag)
    {
        return (from r in await client.GetAll(owner, name)
            where r.TagName == tag
            select r).ToArray().Any();
    }

    public static async Task UploadTextFileAsset(this IReleasesClient client, Release release, string filePath)
    {
        if (File.Exists(filePath) is false)
        {
            var errorMsg = "Could not upload text file asset to release '{Value1}'.";
            errorMsg += $"{Environment.NewLine}The path to the asset file '{{Value2}} does not exist.";
            Log.Error(errorMsg, release.Name, filePath);
            Assert.Fail($"The asset file '{filePath}' does not exist.");
        }

        await using var fileAsset = File.OpenRead(filePath);

        var assetUpload = new ReleaseAssetUpload()
        {
            FileName = Path.GetFileName(filePath),
            ContentType = "application/zip",
            RawData = fileAsset
        };

        try
        {
            var result = await client.UploadAsset(release, assetUpload);

            Log.Information("Id: {Value}", result.Id);
            Log.Information("Label: {Value}", result.Label);
            Log.Information("Name: {Value}", result.Name);
        }
        catch (Exception e)
        {
            var errorMsg = "Uploading the text file asset '{Value1}' for release '{Value2}' failed.";
            errorMsg += $"{Environment.NewLine}{e.Message}";
            Log.Error(errorMsg);
            Assert.Fail("There was an issue uploading a release asset.");
        }
    }

    public static async Task<bool> MilestoneExists(
        this IMilestonesClient client,
        string owner,
        string name,
        string version)
    {
        return (from m in await client.GetAllForRepository(owner, name)
            where m.Title == version
            select m).Any();
    }

    public static async Task<Milestone?> GetByTitle(
        this IMilestonesClient client,
        string owner,
        string name,
        string title)
    {
        var milestones = (from m in await client.GetAllForRepository(owner, name)
            where m.Title == title
            select m).ToArray();

        if (milestones.Length <= 0)
        {
            return null;
        }

        return milestones[0];
    }

    public static string GetReleaseNotesFilePath(this Solution solution, ReleaseType releaseType, string version)
    {
        const string relativeDir = "Documentation/ReleaseNotes";

        var notesDirPath = releaseType switch
        {
            ReleaseType.Preview => $"{solution.Directory}/{relativeDir}/PreviewReleases",
            ReleaseType.Production => $"{solution.Directory}/{relativeDir}/ProductionReleases",
            _ => throw new ArgumentOutOfRangeException(nameof(releaseType), releaseType, null)
        };

        var fileName = $"Release-Notes-{version}.md";

        return $"{notesDirPath}/{fileName}";
    }

    public static string GetReleaseNotes(this Solution solution, ReleaseType releaseType, string version)
    {
        var fullFilePath = solution.GetReleaseNotesFilePath(releaseType, version);

        if (File.Exists(fullFilePath) is false)
        {
            var errorMsg = "The {Value1} release notes for version '{Value2}' at file path '{Value3}'";
            errorMsg += " could not be found.";
            Log.Error(errorMsg,
                releaseType.ToString().ToLower(),
                version,
                fullFilePath.Replace(solution.Directory, "~"));
            Assert.Fail("The release notes file could not be found.");
        }

        return File.ReadAllText(fullFilePath);
    }

    /// <summary>
    /// Returns a value indicating whether or not a branch with the given branch name
    /// matches the given <paramref name="pattern"/>.
    /// </summary>
    /// <param name="pattern">The value to check against the branch name.</param>
    /// <returns><c>true</c> if the <paramref name="pattern"/> is equal to the branch name.</returns>
    /// <remarks>
    ///     The comparison is case sensitive.
    /// </remarks>
    private static bool EqualTo(string value, string pattern)
    {
        pattern = string.IsNullOrEmpty(pattern) ? string.Empty : pattern;

        var hasGlobbingSyntax = pattern.Contains(MatchNumbers) || pattern.Contains(MatchAnything);
        var isEqual = hasGlobbingSyntax
            ? Match(value, pattern)
            : (string.IsNullOrEmpty(pattern) && string.IsNullOrEmpty(value)) || pattern == value;

        return isEqual;
    }

    /// <summary>
    /// Returns a value indicating whether or not the given <paramref name="globbingPattern"/> contains a match
    /// to the given <c>string</c> <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The <c>string</c> to match.</param>
    /// <param name="globbingPattern">The globbing pattern and text to search.</param>
    /// <returns>
    ///     <c>true</c> if the globbing pattern finds a match in the given <c>string</c> <paramref name="value"/>.
    /// </returns>
    private static bool Match(string value, string globbingPattern)
    {
        // NOTE: Refer to this website for more regex information -> https://regex101.com/
        const char regexMatchStart = '^';
        const char regexMatchEnd = '$';
        const string regexMatchNumbers = @"\d+";
        const string regexMatchAnything = ".+";

        // Remove any consecutive '#' and '*' symbols until no more consecutive symbols exists anymore
        globbingPattern = RemoveConsecutiveCharacters(new[] { MatchNumbers, MatchAnything }, globbingPattern);

        // Replace the '#' symbol with
        globbingPattern = globbingPattern.Replace(MatchNumbers.ToString(), regexMatchNumbers);

        // Prefix all '.' symbols with '\' to match the '.' literally in regex
        globbingPattern = globbingPattern.Replace(".", @"\.");

        // Replace all '*' character with '.+'
        globbingPattern = globbingPattern.Replace(MatchAnything.ToString(), regexMatchAnything);

        globbingPattern = $"{regexMatchStart}{globbingPattern}{regexMatchEnd}";

        return Regex.Matches(value, globbingPattern).Count > 0;
    }

    /// <summary>
    /// Removes any consecutive occurrences of the given <paramref name="characters"/> from the given <c>string</c> <paramref name="value"/>.
    /// </summary>
    /// <param name="characters">The <c>char</c> to check.</param>
    /// <param name="value">The value that contains the consecutive characters to remove.</param>
    /// <returns>The original <c>string</c> value with the consecutive characters removed.</returns>
    private static string RemoveConsecutiveCharacters(IEnumerable<char> characters, string value)
    {
        foreach (var c in characters)
        {
            while (value.Contains($"{c}{c}"))
            {
                value = value.Replace($"{c}{c}", c.ToString());
            }
        }

        return value;
    }
}
