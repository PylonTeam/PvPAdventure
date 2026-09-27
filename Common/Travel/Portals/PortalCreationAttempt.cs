namespace PvPAdventure.Common.Travel.Portals;

// Correlates local predictions with server replies, including cancel/retry during a round trip.
internal sealed class PortalCreationAttempt
{
    public int RequestId { get; private set; }
    public bool Pending { get; private set; }

    public int Begin()
    {
        Track(RequestId == int.MaxValue ? 1 : RequestId + 1);
        return RequestId;
    }

    public void Track(int requestId)
    {
        RequestId = requestId;
        Pending = true;
    }

    public bool IsPending(int requestId) => Pending && RequestId == requestId;

    public bool Finish(int requestId)
    {
        if (!IsPending(requestId))
            return false;

        Pending = false;
        return true;
    }
}
