namespace NukeLearningCICD;

public enum ReleaseType
{
    Production,
    Preview,
    HotFix,
}

public enum BranchType
{
    Master,
    Develop,
    Feature,
    PreviewFeature,
    Release,
    Preview,
    HotFix,
    Other,
}

public enum RunType
{
    StatusCheck,
    Release,
}
