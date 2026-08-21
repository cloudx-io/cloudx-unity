// IsExternalInit.cs
//
// This type is required to enable C# 9.0 init-only properties and records in projects
// targeting .NET Standard 2.1 or earlier frameworks (like Unity).
//
// Background:
// - C# 9.0 introduced records and init-only setters (init accessors)
// - These features require the System.Runtime.CompilerServices.IsExternalInit type
// - This type is included in .NET 5+ but NOT in .NET Standard 2.1 or .NET Framework
// - Unity projects typically target .NET Standard 2.1, so this type is missing
//
// Solution:
// - The C# compiler doesn't care WHERE this type comes from
// - We can define it ourselves in our project and the compiler will accept it
// - This is the officially recommended workaround from Microsoft
// - No conditional compilation needed: the 'internal' modifier prevents conflicts
//
// Why no #if directive?
// - The compiler prefers the framework's public type when available (.NET 5.0+)
// - Our internal version is invisible outside this assembly, so no conflict is possible
// - This is Microsoft's official recommendation: keep it simple
//
// References:
// - https://github.com/dotnet/runtime/issues/34978
// - https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-9.0/init

using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Reserved for use by the compiler for tracking metadata.
    /// This class allows the use of init-only setters and records in C# 9.0+
    /// when targeting frameworks that don't include this type (.NET Standard 2.1, etc.).
    /// </summary>
    /// <remarks>
    /// This class is marked as internal and will not conflict with the framework's
    /// built-in version in .NET 5.0+. The compiler automatically uses the framework
    /// version when available.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IsExternalInit
    {
    }
}
