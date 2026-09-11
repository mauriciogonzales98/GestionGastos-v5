using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionGastos.Api.Migrations
{
    /// <inheritdoc />
    public partial class NotaDelMovimientoYCodigoDeTresLetras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "nota",
                table: "movimiento",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddCheckConstraint(
                name: "ck_moneda_codigo_tres_letras",
                table: "moneda",
                sql: "codigo REGEXP '^[A-Za-z]{3}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_moneda_codigo_tres_letras",
                table: "moneda");

            migrationBuilder.DropColumn(
                name: "nota",
                table: "movimiento");
        }
    }
}
