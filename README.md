# actors

**Tecnología:** ASP.NET Core

## Responsabilidad

Actores de negocio distintos de la identidad técnica: clientes, aliados y proveedores según evolucione el alcance.

## Reglas

- Mantener el ownership definido en DD/SDD.
- No escribir directamente en tablas de otros servicios.
- Publicar/consumir eventos únicamente mediante contratos versionados.
- Mantener aislamiento multi-tenant cuando corresponda.
