using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionGastos.Api.Migrations
{
    /// <inheritdoc />
    public partial class LimiteDeIntentosFallidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "intento_de_acceso",
                columns: table => new
                {
                    email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fallos_consecutivos = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    ultimo_fallo = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intento_de_acceso", x => x.email);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_intento_de_acceso_ultimo_fallo",
                table: "intento_de_acceso",
                column: "ultimo_fallo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "intento_de_acceso");
        }
    }
}
