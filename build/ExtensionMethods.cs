using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GlobExpressions;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.Tools.GitHub;
using Octokit;
using Serilog;

namespace NukeLearningCICD;

public static class ExtensionMethods
{
    private static int[] digits = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
    private const char MatchNumbers = '#';
    private const char MatchAnything = '*';

    public static bool IsOnCorrectBranch(this GitRepository repo, string branchPattern)
    {
        if (repo.Branch is null)
        {
            return false;
        }

        return EqualTo(repo.Branch, branchPattern);
    }

    public static bool IsOnFeatureBranch(this GitRepository repo)
    {
        return IsOnCorrectBranch(repo, "feature/#-*");
    }

    public static bool IsOnPreviewReleaseBranch(this GitRepository repo)
    {
        return IsOnCorrectBranch(repo, "preview/v*.*.*");
    }

    public static bool IsOnPreviewReleaseBranch(this GitRepository repo, string messageOnFalse)
    {
        if (string.IsNullOrEmpty(messageOnFalse))
        {
            Assert.Fail($"The parameter '{nameof(messageOnFalse)}' cannot be null or empty when checking if currently on a preview release branch.");
        }

        var result = IsOnCorrectBranch(repo, "preview/v#.#.#-preview.#");

        if (string.IsNullOrEmpty(messageOnFalse) is false && result is false)
        {
            Log.Error(messageOnFalse);
        }

        return result;
    }

    public static bool IsOnReleaseBranch(this GitRepository repo)
    {
        return IsOnCorrectBranch(repo, "release/v*.*.*");
    }

    public static bool IsOnPreviewFeatureBranch(this GitRepository repo)
    {
        return IsOnCorrectBranch(repo, "preview/feature/#-*");
    }

    public static bool IsOnHotFixBranch(this GitRepository repo)
    {
        return IsOnCorrectBranch(repo, "feature/#-*");
    }

    public static bool IsOnHotFixBranch(this GitRepository repo, string messageOnFalse)
    {
        if (string.IsNullOrEmpty(messageOnFalse))
        {
            Assert.Fail($"The parameter '{nameof(messageOnFalse)}' cannot be null or empty when checking if currently on a hot fix branch.");
        }

        var result = IsOnCorrectBranch(repo, "feature/#-*");

        if (string.IsNullOrEmpty(messageOnFalse) is false && result is false)
        {
            Log.Error(messageOnFalse);
        }

        return result;
    }

    // public static ITargetDefinition IsCorrectGitHubOrg(this ITargetDefinition targetDefinition)
    // {
    //     if (CICD.GitHubOrganization != "KinsonDigital")
    //     {
    //         var failMsg = "The github organization must be 'KinsonDigital'.";
    //         failMsg += $"{Environment.NewLine}Verify that the '{nameof(CICD)}.{nameof(CICD.GitHubOrganization)}' static property is set correctly.";
    //
    //         Assert.Fail(failMsg);
    //     }
    //
    //     return targetDefinition;
    // }

    public static async Task<bool> TagExists(this IRepositoriesClient repoClient, string tag)
    {

        // var tags = await repoClient.GetAllTags(GitHubTasks.GetGitHubOwner(), GitHubTasks.GetGitHubName());

        return false;
    }

    /// <summary>
    /// Returns a value indicating whether or not a branch with the given branch name
    /// matches the given <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value to check against the branch name.</param>
    /// <returns><c>true</c> if the <paramref name="value"/> is equal to the branch name.</returns>
    /// <remarks>
    ///     The comparison is case sensitive.
    /// </remarks>
    private static bool EqualTo(string branch, string value)
    {
        value = string.IsNullOrEmpty(value) ? string.Empty : value;

        var hasGlobbingSyntax = value.Contains(MatchNumbers) || value.Contains(MatchAnything);
        var isEqual = hasGlobbingSyntax
            ? Match(branch, value)
            : (string.IsNullOrEmpty(value) && string.IsNullOrEmpty(branch)) || value == branch;

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
