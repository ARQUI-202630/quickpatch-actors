namespace QuickPatch.Actors.Application.Abstractions;

/// <summary>
/// Ejecuta un trabajo en una transacción que primero fija el tenant de la sesión (<c>app.current_tenant</c>, DD 10.2), para
/// que RLS aísle los datos. Al terminar guarda y confirma.
/// </summary>
public interface ITenantUnitOfWork
{
    Task<T> ExecuteAsync<T>(Guid tenantId, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}