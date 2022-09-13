namespace NukeLearningCICD;

public enum ReleaseType
{
    Production,
    Preview
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
