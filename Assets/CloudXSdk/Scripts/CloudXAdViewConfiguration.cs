#nullable enable

namespace CloudX
{
    /// <summary>
    /// Position configuration for banner and MREC ad views. A configuration holds exactly one
    /// position: either a horizontal <see cref="AdViewPosition"/> or a vertical
    /// <see cref="AdViewVerticalPosition"/>, depending on which constructor was used.
    ///
    /// Vertical positions are supported only for banner ads. MRECs are square, so a vertical
    /// orientation does not apply to them - passing a vertical configuration to
    /// <see cref="CloudXSdk.CreateMrec"/> throws <see cref="System.ArgumentException"/>.
    /// </summary>
    public class CloudXAdViewConfiguration
    {
        /// <summary>
        /// The horizontal position, or null when this configuration was created with a
        /// vertical position.
        /// </summary>
        internal AdViewPosition? HorizontalPosition { get; }

        /// <summary>
        /// The vertical position, or null when this configuration was created with a
        /// horizontal position.
        /// </summary>
        internal AdViewVerticalPosition? VerticalPosition { get; }

        /// <summary>
        /// Creates a configuration for a horizontally positioned banner or MREC ad view.
        /// </summary>
        public CloudXAdViewConfiguration(AdViewPosition position)
        {
            HorizontalPosition = position;
        }

        /// <summary>
        /// Creates a configuration for a vertical banner ad view rotated against a screen
        /// edge. Supported only for banner ads, not MRECs.
        /// </summary>
        public CloudXAdViewConfiguration(AdViewVerticalPosition verticalPosition)
        {
            VerticalPosition = verticalPosition;
        }

        /// <summary>
        /// Position for horizontal banner and MREC ad views.
        ///
        /// WARNING: This enum MUST be kept in sync with Android's position enum!
        /// Enum names are passed as strings via JNI. Mismatched enums will cause runtime crashes.
        /// </summary>
        public enum AdViewPosition
        {
            TopLeft,
            TopCenter,
            TopRight,
            CenterLeft,
            Centered,
            CenterRight,
            BottomLeft,
            BottomCenter,
            BottomRight,
        }

        /// <summary>
        /// Vertical position for banner ad views rotated 90 degrees against a screen edge.
        /// Supported only for banner ads, not MRECs.
        ///
        /// Left pins the banner flush against the left screen edge, rotated clockwise; Right
        /// pins it against the right screen edge, rotated counter-clockwise. In both cases the
        /// top of the creative faces the screen center and the banner is vertically centered
        /// on its edge. When a display cutout (notch, camera hole, Dynamic Island) occupies
        /// that edge - the common case in landscape - the banner is inset past it so the
        /// creative is never obscured.
        ///
        /// WARNING: This enum MUST be kept in sync with Android's AdViewVerticalPosition enum!
        /// Enum names are passed as strings via JNI. Mismatched enums will cause runtime crashes.
        /// </summary>
        public enum AdViewVerticalPosition
        {
            Left,
            Right,
        }
    }
}
