using Nuke.Common;

namespace NukeLearningCICD;

public partial class CICD // Release.Production
{
    Target RunProductionRelease => _ => _
        .Executes(() =>
        {

        });
}
