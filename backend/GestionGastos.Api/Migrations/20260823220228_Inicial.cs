using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GestionGastos.Api.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "moneda",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    codigo = table.Column<string>(type: "char(3)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    nombre = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    simbolo = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    decimales = table.Column<byte>(type: "tinyint unsigned", nullable: false, defaultValue: (byte)2),
                    es_predeterminada = table.Column<ulong>(type: "bit(1)", nullable: false, defaultValue: 0ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moneda", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "usuario",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "categoria",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    nombre = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tipo = table.Column<sbyte>(type: "tinyint", nullable: false),
                    usuario_id = table.Column<long>(type: "bigint", nullable: true),
                    activa = table.Column<ulong>(type: "bit(1)", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categoria", x => x.id);
                    table.ForeignKey(
                        name: "FK_categoria_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "movimiento",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    usuario_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo = table.Column<sbyte>(type: "tinyint", nullable: false),
                    monto = table.Column<decimal>(type: "decimal(11,2)", nullable: false),
                    moneda_id = table.Column<short>(type: "smallint", nullable: false),
                    categoria_id = table.Column<int>(type: "int", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimiento", x => x.id);
                    table.CheckConstraint("ck_movimiento_monto_positivo", "monto > 0");
                    table.ForeignKey(
                        name: "FK_movimiento_categoria_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categoria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimiento_moneda_moneda_id",
                        column: x => x.moneda_id,
                        principalTable: "moneda",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimiento_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "categoria",
                columns: new[] { "id", "activa", "nombre", "tipo", "usuario_id" },
                values: new object[,]
                {
                    { 1, 1ul, "Comida", (sbyte)0, null },
                    { 2, 1ul, "Transporte", (sbyte)0, null },
                    { 3, 1ul, "Vivienda", (sbyte)0, null },
                    { 4, 1ul, "Servicios", (sbyte)0, null },
                    { 5, 1ul, "Salud", (sbyte)0, null },
                    { 6, 1ul, "Ocio", (sbyte)0, null },
                    { 7, 1ul, "Otros", (sbyte)0, null },
                    { 8, 1ul, "Sueldo", (sbyte)1, null },
                    { 9, 1ul, "Ingreso extra", (sbyte)1, null },
                    { 10, 1ul, "Otros", (sbyte)1, null }
                });

            migrationBuilder.InsertData(
                table: "moneda",
                columns: new[] { "id", "codigo", "decimales", "es_predeterminada", "nombre", "simbolo" },
                values: new object[] { (short)1, "ARS", (byte)2, 1ul, "Peso argentino", "$" });

            migrationBuilder.InsertData(
                table: "moneda",
                columns: new[] { "id", "codigo", "decimales", "nombre", "simbolo" },
                values: new object[] { (short)2, "USD", (byte)2, "Dólar estadounidense", "US$" });

            migrationBuilder.InsertData(
                table: "usuario",
                columns: new[] { "id", "email" },
                values: new object[] { 1L, "semilla@gestiongastos.local" });

            migrationBuilder.CreateIndex(
                name: "ux_categoria_ambito_nombre_tipo",
                table: "categoria",
                columns: new[] { "usuario_id", "nombre", "tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_moneda_codigo",
                table: "moneda",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_movimiento_categoria_id",
                table: "movimiento",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimiento_moneda_id",
                table: "movimiento",
                column: "moneda_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_usuario_fecha",
                table: "movimiento",
                columns: new[] { "usuario_id", "fecha", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_usuario_email",
                table: "usuario",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "movimiento");

            migrationBuilder.DropTable(
                name: "categoria");

            migrationBuilder.DropTable(
                name: "moneda");

            migrationBuilder.DropTable(
                name: "usuario");
        }
    }
}
