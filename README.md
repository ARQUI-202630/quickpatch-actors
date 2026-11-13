# actors

**Tecnología:** ASP.NET Core

## Responsabilidad

Directorio de actores de negocio del tenant (proveedores con su equipo y clientes), armado con eventos. Los aliados siguen siendo evolutivos (SDD 6.14): no hay evento que los origine.

## Reglas

- Mantener el ownership definido en DD/SDD.
- No escribir directamente en tablas de otros servicios.
- Publicar/consumir eventos únicamente mediante contratos versionados.
- Mantener aislamiento multi-tenant cuando corresponda.

## Qué hace (SCRUM-640)

Actors arma el **directorio de actores de negocio de cada tenant** (Supplier y Client del SDD 6.14) solo con eventos que ya existen. No agrega REST (ADR-025), no publica eventos y no cambia ningún contrato. El equipo del proveedor sigue administrándose en Identity (DD, P-02): Actors lo proyecta, no lo reemplaza.

| Evento consumido | Qué hace | Tabla |
|---|---|---|
|`technician.registered`|Agrega el técnico con su proveedor (`providerId`), su especialidad y su estado, y recuenta el equipo del proveedor (y el del anterior si cambió)|`technician_actors`, `suppliers`|
|`technician.verification-changed`|Actualiza el estado del técnico y recuenta los aprobados del equipo. Un cambio con `updatedAt` anterior al guardado se descarta, porque los dos topics llegan por separado (mismo criterio que la réplica de Matching, ADR-017). Si llega antes que el registro, el técnico entra sin proveedor y el registro lo completa|`technician_actors`, `suppliers`|
|`service-request.created`|Suma la solicitud al cliente y guarda la primera y la última fecha. No guarda la descripción ni la dirección (RN-SR13)|`client_actors`|

Cada evento se aplica una vez por `eventId` (`processed_events`, ADR-007), bajo el tenant del evento y en una sola transacción. Un mensaje que no cumple el contrato se descarta; un error de base se reintenta sin confirmar el offset.

**Límite conocido.** Los topics guardan 7 días (`retentionHours: 168`). En el primer despliegue el directorio solo recibe los eventos que siguen en Kafka; los técnicos y clientes anteriores aparecen cuando vuelven a generar un evento.

**Datos (`db_actors`).** `technician_actors`, `suppliers`, `client_actors` y `processed_events`, todas con RLS forzado por `app.current_tenant`. Permisos en `db/roles.sql`: `actors_app` sin BYPASSRLS. Sin Outbox ni rol de publicador. Las migraciones van en la imagen como bundle de EF Core (`/app/efbundle`), para el init container con `actors_migrator`.

**Configuración.** `ConnectionStrings:Actors`, `Kafka:BootstrapServers` (vacío: no arrancan los consumidores) y `Kafka:ConsumerGroupId` (`actors-service`).

**Capas.** Los consumidores (`Infrastructure/Messaging`) solo deserializan y delegan; las reglas están en el dominio y los casos de uso en `Application`. Las pruebas de arquitectura (`tests/unit/.../Architecture`, NetArchTest) rompen el build si Domain o Application dependen de tecnología, si el API usa Infrastructure fuera de `Program` o si un consumidor toca los repositorios.

## Estructura

Cuatro capas, según el SDD (secciones 6.3 y 6.5):

```text
QuickPatch.Actors.slnx
src/
  QuickPatch.Actors.Api/              Endpoints, validación de entrada y raíz de composición
  QuickPatch.Actors.Application/      Casos de uso, comandos y consultas
  QuickPatch.Actors.Domain/           Entidades, value objects y reglas de negocio (sin dependencias externas)
  QuickPatch.Actors.Infrastructure/   PostgreSQL (EF Core) y consumidores de Kafka
tests/
  unit/QuickPatch.Actors.UnitTests/
  integration/QuickPatch.Actors.IntegrationTests/
```

Dependencias permitidas: `Api → Application → Domain`; `Infrastructure → Application, Domain`. `Api` referencia `Infrastructure` solo para registrar sus servicios.

## Desarrollo local

Requiere el SDK de .NET indicado en `global.json`.

```bash
dotnet restore
dotnet format --verify-no-changes   # lint, igual que el CI
dotnet build -c Release
dotnet test tests/unit/QuickPatch.Actors.UnitTests
dotnet test tests/integration/QuickPatch.Actors.IntegrationTests
dotnet run --project src/QuickPatch.Actors.Api
```

## Contenedor

- Imagen: `Dockerfile` en la raíz (multi-stage, usuario sin privilegios).
- Puerto: `8080`.
- Probes para k3s: `GET /health/live` (el proceso responde) y `GET /health/ready` (además, PostgreSQL responde).
- Despliegue en k3s: `deploy/k8s/` (manifiesto y pasos del primer despliegue).
