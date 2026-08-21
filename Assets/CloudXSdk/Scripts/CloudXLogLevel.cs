namespace CloudX
{
/// <summary>
/// Log level for CloudX SDK.
/// WARNING: Enum values must match Android/iOS SDK CloudXLogLevel names exactly.
/// Changes here require corresponding updates in native SDKs.
/// </summary>
public enum CloudXLogLevel
{
    Verbose,
    Debug,
    Info,
    Warn,
    Error,
    None,
}
}
