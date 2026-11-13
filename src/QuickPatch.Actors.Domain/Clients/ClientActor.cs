namespace QuickPatch.Actors.Domain.Clients;

/// <summary>
/// Cliente (Client, SDD 6.14) como actor de negocio del tenant: cuántas solicitudes ha creado y cuándo fue la primera y la
/// última. Sale de <c>service-request.created</c>; no guarda datos personales ni la dirección (RN-SR13).
/// </summary>
public sealed class ClientActor
{
    private ClientActor()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid ClientId { get; private set; }

    public int RequestCount { get; private set; }

    public DateTimeOffset FirstRequestAt { get; private set; }

    public DateTimeOffset LastRequestAt { get; private set; }

    public static ClientActor Create(Guid tenantId, Guid clientId, DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty || clientId == Guid.Empty)
        {
            throw new ArgumentException("El tenant y el cliente son obligatorios.");
        }

        return new ClientActor { TenantId = tenantId, ClientId = clientId, FirstRequestAt = createdAt, LastRequestAt = createdAt };
    }

    /// <summary>Suma una solicitud; las fechas aguantan que los eventos lleguen en otro orden entre particiones.</summary>
    public void RecordRequest(DateTimeOffset createdAt)
    {
        RequestCount++;
        if (createdAt < FirstRequestAt)
        {
            FirstRequestAt = createdAt;
        }

        if (createdAt > LastRequestAt)
        {
            LastRequestAt = createdAt;
        }
    }
}