using Nuke.Common;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace NukeLearningCICD;

public partial class CICD // Common.Build
{
    Target BuildAllProjects => _ => _
        .DependsOn(RestoreSolution)
        .Before(BuildMainProject, BuildTestProject)
        .Triggers(BuildMainProject, BuildTestProject)
        .Executes(() =>
        {
            Log.Information($"⚙️Building All Projects");
        });

    Target BuildMainProject => _ => _
        .DependsOn(RestoreSolution)
        .After(BuildAllProjects)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(MainProjPath)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target BuildTestProject => _ => _
        .DependsOn(RestoreSolution)
        .Before(RunUnitTests)
        .After(BuildAllProjects)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(TestProjPath)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });
}
