/*
 * First Look demo switches. The CloudX and AdMob ad unit ids both come from
 * DemoConfig (the AdMob ones are Google's official test units).
 */
public static class FirstLookConfig
{
    /*
     * Flip to true to exercise the AdMob fallback path: CloudX is asked to fill
     * an unknown ad unit, fails to load, and the controllers fall back to AdMob.
     */
    public const bool ForceCloudXNoFill = false;

    private const string InvalidCloudXAdUnitId = "first-look-invalid-unit";

    public static string CloudXAdUnitOrInvalid(string realAdUnitId) =>
        ForceCloudXNoFill ? InvalidCloudXAdUnitId : realAdUnitId;
}
