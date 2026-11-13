namespace QuickPatch.Actors.Application;

/// <summary>Resultado de aplicar un evento consumido.</summary>
public enum HandlingOutcome
{
    /// <summary>El directorio cambió.</summary>
    Applied,

    /// <summary>El evento ya se había aplicado (mismo <c>eventId</c>, RN-EV1).</summary>
    Duplicate,

    /// <summary>El evento es más viejo que el estado guardado; se registra y no cambia nada.</summary>
    Stale,
}