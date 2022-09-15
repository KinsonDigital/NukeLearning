using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.Tools.GitHub;
using Serilog;

namespace NukeLearningCICD;

public partial class CICD // Release.Preview
{
    Target RunPreviewRelease => _ => _
        // .Requires(
        //     () => ThatTheReleaseIsNotFromPullRequest(ReleaseType.Preview)
        // )
        .Before(BuildAllProjects, RunAllUnitTests)
        .Triggers(BuildAllProjects, RunAllUnitTests)
        .Executes(async () =>
        {
            Log.Information($"🚀Starting preview release process for version 'stuff'");

            return await Task.FromResult(4);
        });
}
