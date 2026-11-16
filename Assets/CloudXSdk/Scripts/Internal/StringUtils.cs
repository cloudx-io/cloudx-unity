#nullable enable

namespace CloudX
{
    internal static class StringUtils
    {
        internal static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
