/*
 * Arbiter/TPA demo switches. The CloudX and AdMob ad unit ids both come from
 * DemoConfig (the AdMob ones are Google's official test units).
 *
 * The AdMob banner and MREC units MUST have Automatic refresh set to Disabled
 * in the AdMob console. The arbiter cycle owns refresh here: it decides when a
 * new fill is requested and which network's view is on screen. An AdMob unit
 * that refreshes on its own would swap the creative behind the arbiter's back.
 */
public static class ArbiterConfig
{
    /*
     * Flip to true to watch the single-bid path: CloudX is asked to fill an
     * unknown ad unit and fails, so AdMob is the only bid in every round and the
     * SDK selects it without a service call.
     */
    public const bool ForceCloudXNoFill = false;

    /*
     * Banner and MREC re-arbitrate on this interval (docs recommend 20-30 s;
     * shorter intervals decrease CPM performance).
     */
    public const float InlineRefreshIntervalSeconds = 25f;

    private const string InvalidCloudXAdUnitId = "arbiter-invalid-unit";

    public static string CloudXAdUnitOrInvalid(string realAdUnitId) =>
        ForceCloudXNoFill ? InvalidCloudXAdUnitId : realAdUnitId;
}
