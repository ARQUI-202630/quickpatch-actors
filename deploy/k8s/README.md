# Despliegue de Actors en k3s

`actors.yaml` crea el ConfigMap, el Deployment (con las migraciones como init container) y el Service en el namespace `quickpatch`. Es igual en QA y en producción; solo cambian los secretos y la dirección de Kafka. Actors no tiene `IngressRoute`: no publica endpoints de negocio (ADR-025) ni eventos; solo consume `technician.registered`, `technician.verification-changed` y `service-request.created` (SCRUM-640).

## Secreto `actors-secretos`

Lo crea DevOps desde Ansible Vault (`primer-despliegue-servicios.yml`, plantilla `secreto-servicio.yaml.j2`). Nunca se guarda en el repositorio. La plantilla agrega también `jwt-public-key`, pero Actors no valida tokens y no la lee.

|Clave|Contenido|
|---|---|
|`migrator-connection`|Cadena de conexión con `actors_migrator` (dueño de las tablas; solo para las migraciones)|
|`app-connection`|Cadena de conexión con `actors_app` (sin `BYPASSRLS`), con el tope de pool del inventario de Ansible (`pool_conexiones.actors: 8`)|

## Primer despliegue

1. Base: `db_actors` con dueño `actors_migrator` (Ansible ya la crea: `postgres_databases`). Los tres topics ya existen (quickpatch-kafka v0.10.0).
2. Secreto y manifiesto: agregar `actors` a la lista `servicios` de `primer-despliegue-servicios.yml` (o crear el Secret a mano) y aplicar `deploy/k8s/actors.yaml` con la IP de Kafka del ambiente en `<vm-mensajeria>`. El pod queda esperando la imagen (`:pendiente`).
3. Push a `release/*`: el pipeline publica la imagen y la fija en el Deployment; el init container aplica las migraciones.
4. Solo la primera vez, un administrador de la base ejecuta `db/roles.sql` (crea `actors_app` con sus permisos) y reinicia el servicio con `kubectl -n quickpatch rollout restart deployment/actors`.

## Recursos

`requests` 128Mi y `limits` 320Mi de memoria, como Ranking, hasta medirlos en QA con carga (AC2-E5, SCRUM-640; Documento de Infraestructura 5.6).
