namespace QuickPatch.Actors.Domain.Technicians;

/// <summary>Estados de verificación del técnico, con los valores de los eventos de Identity (DD 8.4.2).</summary>
public static class VerificationStatus
{
    public const string Pending = "pendiente";
    public const string Approved = "aprobado";
    public const string Rejected = "rechazado";
    public const string Suspended = "suspendido";

    private static readonly HashSet<string> Known = [Pending, Approved, Rejected, Suspended];

    public static bool IsKnown(string? status) => status is not null && Known.Contains(status);
}