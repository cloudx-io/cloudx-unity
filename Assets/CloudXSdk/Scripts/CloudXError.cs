// CloudXError.cs

#nullable enable

namespace CloudX
{
    public record CloudXError(
        string ErrorCodeName,  // Android enum name (e.g., "NO_FILL", "NETWORK_ERROR")
        int ErrorCode,         // Android enum code value (e.g., 300, 200)
        string? Message        // Custom message from Android (optional)
    )
    {
        public override string ToString() =>
            $"CloudXError(code={ErrorCodeName}[{ErrorCode}], message={Message ?? "null"})";
    }
}
