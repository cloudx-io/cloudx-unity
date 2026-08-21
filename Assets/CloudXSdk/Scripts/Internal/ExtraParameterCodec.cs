#nullable enable

using System.Collections;
using System.Collections.Generic;

namespace CloudX
{
    /*
     * Marshals publisher extra-parameter values across the JNI / P-Invoke boundary.
     *
     * The interop layer can only carry primitives and strings, so a structured value
     * (number, list, map) is serialized to an enveloped JSON string {"value": <value>}
     * here on the C# side. The native bridge parses the envelope, rebuilds a real
     * Map / List / scalar, and hands it to native setExtraParameter, whose own coercion
     * (minFloor / minFloors / generic) then applies. The {"value": ...} envelope keeps
     * the top level a JSON object so scalars and arrays parse uniformly on both platforms.
     *
     * Reuses [Json.Write], which emits invariant-culture numbers. Only the scalar types it
     * preserves are accepted (see [IsSupported]); other numeric primitives are rejected rather
     * than silently stringified.
     */
    internal static class ExtraParameterCodec
    {
        /// <summary>
        /// Serializes [value] into an enveloped JSON string for transport. Returns null when
        /// the value type is unsupported, in which case the caller should skip the native call
        /// and leave any previously-stored value untouched (matching native "drop + log").
        /// </summary>
        public static string? SerializeEnvelope(object value)
        {
            if (!IsSupported(value))
            {
                CloudXSdk.Log.LogWarning(() =>
                    $"Ignoring extra parameter value of unsupported type {value.GetType().Name}; " +
                    "use bool, int, long, float, double, decimal, string, or an IList/IDictionary of those");
                return null;
            }

            if (IsNonFinite(value))
            {
                CloudXSdk.Log.LogWarning(() => $"Ignoring non-finite extra parameter value: {value}");
                return null;
            }

            var envelope = new Dictionary<string, object> { { "value", value } };
            return Json.Write(envelope);
        }

        /*
         * Only the scalar types [Json.Write] preserves as JSON numbers/bools are allowed. Other
         * numeric primitives (byte, short, uint, ulong, char, ...) would fall through Json.Write to
         * ToString() and be quoted as strings on the wire, silently changing their type — so they
         * are rejected here rather than accepted via GetType().IsPrimitive.
         */
        private static bool IsSupported(object value) =>
            value is bool || value is int || value is long || value is float || value is double
            || value is decimal || value is string || value is IList || value is IDictionary;

        /*
         * Guards top-level scalars only. NaN / +-Infinity are not valid JSON, so they are dropped
         * here rather than emitted as bare tokens. A non-finite value nested inside a list or map
         * still produces invalid JSON that the native SDK drops and logs.
         */
        private static bool IsNonFinite(object value) => value switch
        {
            double d => double.IsNaN(d) || double.IsInfinity(d),
            float f => float.IsNaN(f) || float.IsInfinity(f),
            _ => false,
        };
    }
}
