using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QualityDoc.Migrations.Audit
{
    /// <inheritdoc />
    public partial class InicialAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "access_logs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioEmail = table.Column<string>(type: "text", nullable: false),
                    DocumentoId = table.Column<int>(type: "integer", nullable: false),
                    VersionId = table.Column<int>(type: "integer", nullable: false),
                    VersionTag = table.Column<string>(type: "text", nullable: false),
                    Accion = table.Column<string>(type: "text", nullable: false),
                    Ip = table.Column<string>(type: "text", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: true),
                    UsuarioEmail = table.Column<string>(type: "text", nullable: true),
                    Rol = table.Column<string>(type: "text", nullable: true),
                    Accion = table.Column<string>(type: "text", nullable: false),
                    Entidad = table.Column<string>(type: "text", nullable: true),
                    EntidadId = table.Column<string>(type: "text", nullable: true),
                    Detalle = table.Column<string>(type: "jsonb", nullable: true),
                    Ip = table.Column<string>(type: "text", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "daily_stats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalDocumentos = table.Column<int>(type: "integer", nullable: false),
                    TotalVersiones = table.Column<int>(type: "integer", nullable: false),
                    DocsVigentes = table.Column<int>(type: "integer", nullable: false),
                    DocsEnRevision = table.Column<int>(type: "integer", nullable: false),
                    DocsRechazados = table.Column<int>(type: "integer", nullable: false),
                    DocsObsoletos = table.Column<int>(type: "integer", nullable: false),
                    TotalDescargas = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_stats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_logs_DocumentoId_VersionId",
                table: "access_logs",
                columns: new[] { "DocumentoId", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_access_logs_EmpresaId",
                table: "access_logs",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EmpresaId",
                table: "audit_logs",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EmpresaId_CreadoEn",
                table: "audit_logs",
                columns: new[] { "EmpresaId", "CreadoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_daily_stats_EmpresaId_Fecha",
                table: "daily_stats",
                columns: new[] { "EmpresaId", "Fecha" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_logs");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "daily_stats");
        }
    }
}
