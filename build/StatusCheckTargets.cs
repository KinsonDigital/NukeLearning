using Nuke.Common;
using Serilog;

namespace DefaultNamespace;

public static class StatusCheckTargets
{
    public static Target TestTarget => _ => _
        .Executes(() => Log.Debug("HELLO WORLD!!"));
}
