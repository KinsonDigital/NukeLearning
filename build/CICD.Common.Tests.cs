using System;
using Nuke.Common;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace NukeLearningCICD;

public partial class CICD // Common.Tests
{
    Target RunAllUnitTests => _ => _
        .DependsOn(RestoreSolution)
        .Triggers(RunUnitTests)
        .Executes(() =>
        {
            Log.Information($"🧪Executing All Tests");
        });

    Target RunUnitTests => _ => _
        .DependsOn(RestoreSolution)
        .After(RunAllUnitTests)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(TestProjPath)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });
}
