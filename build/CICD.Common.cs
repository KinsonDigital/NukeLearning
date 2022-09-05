using Nuke.Common;
using Nuke.Common.Tools.DotNet;
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

    // TODO: Create target to announce release on twitter 👉🏼 https://nuke.build/docs/common/chats/
}
