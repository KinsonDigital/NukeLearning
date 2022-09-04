using System;
using Nuke.Common;
using Serilog;

namespace NukeLearningCICD;

public partial class CICD
{
    readonly string WrongPreviewReleaseBranchMessage =
        "Preview releases can only be performed on preview release branches." +
        $"{Environment.NewLine}Preview Release Branch Syntax: preview/v#.#.#-preview.#";

    Target StartPreviewRelease => _ => _
        .Requires(() => Repo.IsOnPreviewReleaseBranch(WrongPreviewReleaseBranchMessage))
        .Requires(() => RepoClient.TagExists("v1.2.3-preview.4"))
        .DependsOn(BuildAllProjects, RunAllUnitTests)
        .After(BuildAllProjects, RunAllUnitTests)
        .Executes(() =>
        {
            Log.Information($"🚀Start Preview Release - TODO: Release number goes here");
        });
}
