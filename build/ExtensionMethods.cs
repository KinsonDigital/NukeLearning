using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GlobExpressions;
using Nuke.Common.Git;

namespace DefaultNamespace;

public static class ExtensionMethods
{
    private static int[] digits = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
    private const char MatchNumbers = '#';
    private const char MatchAnything = '*';

    public static bool IsCorrectBranch(this GitRepository repository, string branchPattern)
    {
        if (repository.Branch is null)
        {
            return false;
        }

        // var branch = "release/v11.22.33-prview.44";
        // var pattern = "release/v#.#.#-prev*.#";
        //
        // var result = EqualTo(branch, pattern);


        var otherResult= EqualTo(repository.Branch, branchPattern);
        return EqualTo(repository.Branch, branchPattern);

        // return Glob.IsMatch(currentBranch, branchPattern);
    }

    public static bool IsReleaseBranch(this GitRepository repo)
    {
        return IsCorrectBranch(repo, "release/v*.*.*");
    }

    private static bool ValidateNumbers(string value, string pattern)
    {
        if (string.IsNullOrEmpty(value) || value.Contains('#') is false)
        {
            return true;
        }

        var sectionsToRemove = value.Split('#');

        var leftovers = string.Empty;

        foreach (var section in sectionsToRemove)
        {
            leftovers += value.Replace(section, string.Empty);
        }

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
    public static bool EqualTo(string branch, string value)
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
