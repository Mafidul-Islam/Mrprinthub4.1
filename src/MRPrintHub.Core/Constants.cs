namespace MRPrintHub.Core;

public static class Constants
{
    public const string AppName = "MRPrintHub";
    public const string ServiceName = "MRPrintHubService";
    public const string ProgramDataFolder = "MRPrintHub";
    public const int DefaultSessionTtlMinutes = 30;
    public const int DefaultMaxFileSizeBytes = 100 * 1024 * 1024;
    public const int DefaultLowDiskThresholdBytes = 500 * 1024 * 1024;
    public const int TokenLengthBytes = 32;
}
