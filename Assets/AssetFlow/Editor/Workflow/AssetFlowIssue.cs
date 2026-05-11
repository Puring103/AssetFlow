namespace AssetFlow.Editor.Workflow
{
    public enum AssetFlowIssueSeverity
    {
        Info,
        Warning,
        Error,
    }

    public readonly struct AssetFlowIssue
    {
        public AssetFlowIssue(AssetFlowIssueSeverity severity, string message)
        {
            Severity = severity;
            Message = message ?? string.Empty;
        }

        public AssetFlowIssueSeverity Severity { get; }

        public string Message { get; }
    }
}
