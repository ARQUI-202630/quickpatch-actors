namespace QuickPatch.Actors.Application.Events;

/// <summary>Topics que consume Actors (<c>quickpatch-kafka/topics/topics.yaml</c>). Actors no publica eventos.</summary>
public static class EventTypes
{
    public const string TechnicianRegistered = "technician.registered";
    public const string TechnicianVerificationChanged = "technician.verification-changed";
    public const string ServiceRequestCreated = "service-request.created";
}