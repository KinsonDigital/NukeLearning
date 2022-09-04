using Nuke.Common;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitHub;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

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

    Target ValidateGitHubRepo => _ => _
        .Executes(() =>
        {
        });
}
