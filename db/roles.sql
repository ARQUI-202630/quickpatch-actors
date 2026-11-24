-- Roles y permisos de Actors Service (DD, sección 10.2; SCRUM-640).
-- Lo ejecuta un administrador de la base DESPUÉS de las migraciones (que crean las tablas y las políticas RLS).
-- Las contraseñas no van aquí: se asignan aparte con ALTER ROLE ... LOGIN PASSWORD, desde el vault.
-- Actors no publica eventos: no hay Outbox ni rol de publicador.
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'actors_app') THEN
        CREATE ROLE actors_app NOLOGIN NOBYPASSRLS;
    END IF;
END
$$;

GRANT USAGE ON SCHEMA public TO actors_app;

-- Rol del servicio: todo bajo RLS. El directorio se inserta y se actualiza; los eventos aplicados solo se insertan y se leen.
GRANT SELECT, INSERT, UPDATE ON technician_actors TO actors_app;
GRANT SELECT, INSERT, UPDATE ON suppliers TO actors_app;
GRANT SELECT, INSERT, UPDATE ON client_actors TO actors_app;
GRANT SELECT, INSERT ON processed_events TO actors_app;
