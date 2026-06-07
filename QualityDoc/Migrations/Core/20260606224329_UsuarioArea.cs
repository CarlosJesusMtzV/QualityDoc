using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QualityDoc.Migrations.Core
{
    /// <inheritdoc />
    public partial class UsuarioArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AreaId",
                table: "Usuarios",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_AreaId",
                table: "Usuarios",
                column: "AreaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Areas_AreaId",
                table: "Usuarios",
                column: "AreaId",
                principalTable: "Areas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Areas_AreaId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_AreaId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "AreaId",
                table: "Usuarios");
        }
    }
}
