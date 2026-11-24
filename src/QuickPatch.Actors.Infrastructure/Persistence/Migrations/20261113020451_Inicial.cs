using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPatch.Actors.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Base de Actors (SCRUM-640): directorio de técnicos, proveedores y clientes de cada tenant, armado con eventos, y los
    /// eventos aplicados, todo con RLS por tenant (DD 10.2). Actors no publica eventos, así que no tiene Outbox.
    /// </summary>
    public partial class Inicial : Migration
    {
        private const string CurrentTenant = "NULLIF(current_setting('app.current_tenant', true), '')::uuid";

        private static readonly string SqlAdicional = string.Join(Environment.NewLine, new[] { "technician_actors", "suppliers", "client_actors", "processed_events" }
            .Select(t => $"""
                ALTER TABLE {t} ENABLE ROW LEVEL SECURITY;
                ALTER TABLE {t} FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON {t} USING (tenant_id = {CurrentTenant}) WITH CHECK (tenant_id = {CurrentTenant});
                """));

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_actors",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_count = table.Column<int>(type: "integer", nullable: false),
                    first_request_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_request_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_actors", x => new { x.tenant_id, x.client_id });
                    table.CheckConstraint("ck_client_actors_requests", "request_count >= 1 AND first_request_at <= last_request_at");
                });

            migrationBuilder.CreateTable(
                name: "processed_events",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_events", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technician_count = table.Column<int>(type: "integer", nullable: false),
                    approved_technician_count = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => new { x.tenant_id, x.supplier_id });
                    table.CheckConstraint("ck_suppliers_counts", "technician_count >= 0 AND approved_technician_count BETWEEN 0 AND technician_count");
                });

            migrationBuilder.CreateTable(
                name: "technician_actors",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technician_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    specialty_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verification_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    registered = table.Column<bool>(type: "boolean", nullable: false),
                    status_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_technician_actors", x => new { x.tenant_id, x.technician_id });
                    table.CheckConstraint("ck_technician_actors_status", "verification_status IN ('pendiente', 'aprobado', 'rechazado', 'suspendido')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_technician_actors_supplier",
                table: "technician_actors",
                columns: new[] { "tenant_id", "supplier_id" });

            migrationBuilder.Sql(SqlAdicional);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_actors");

            migrationBuilder.DropTable(
                name: "processed_events");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "technician_actors");
        }
    }
}
