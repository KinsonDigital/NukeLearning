using Nuke.Common;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace NukeLearningCICD;

public partial class CICD // StatusChecks
{
    Target BuildStatusCheck => _ => _
        .Triggers(BuildAllProjects)
        .Executes(() =>
        {
            Log.Information("✅Build Status Check - Executing {Value} Target", nameof(BuildAllProjects));
        });

    Target UnitTestStatusCheck => _ => _
        .Triggers(RunAllUnitTests)
        .Executes(() =>
        {
            Log.Information("✅Unit Test Status Check - Executing {Value} Target", nameof(RunAllUnitTests));
        });
}
