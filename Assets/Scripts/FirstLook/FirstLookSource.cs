/*
 * Which SDK served an ad in the First Look flow. Shared by both controllers so
 * a single handler can take events from either format. Copy this file with
 * whichever controller you take.
 *
 * https://docs.cloudx.io/en/unity/integrations/first-look
 */
public enum FirstLookSource
{
    CloudX,
    AdMob,
}
