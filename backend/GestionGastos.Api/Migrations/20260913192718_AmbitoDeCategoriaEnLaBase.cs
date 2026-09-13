using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionGastos.Api.Migrations
{
    /// <summary>
    /// **La invariante de D7-07 pasa a la base** (FR-006): un movimiento no puede quedar
    /// clasificado con la categoría de otra cuenta, ni siquiera escribiendo directo en MySQL.
    ///
    /// Va en su **propia** migración, separada de la de datos, y no por prolijidad: es la
    /// verificación de que aquélla salió bien. Si algún movimiento hubiera quedado fuera de su
    /// ámbito, esta migración no entra — y separada en su archivo, el fallo señala el paso correcto
    /// en vez de dejar "la migración falló" a secas (research D-04).
    ///
    /// **El índice viejo se va y no se duplica.** `IX_movimiento_categoria_id` empezaba por la misma
    /// columna que el que la foránea compuesta necesita, así que dejarlo sería mantener dos índices
    /// para lo mismo: el compuesto sirve igual a toda consulta que acote por `categoria_id` sola.
    /// Lo resolvió el scaffolding por su cuenta y se revisó antes de aceptarlo (data-model).
    /// </summary>
    public partial class AmbitoDeCategoriaEnLaBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_movimiento_categoria_categoria_id",
                table: "movimiento");

            migrationBuilder.DropIndex(
                name: "IX_movimiento_categoria_id",
                table: "movimiento");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_categoria_id_usuario",
                table: "categoria",
                columns: new[] { "id", "usuario_id" });

            migrationBuilder.CreateIndex(
                name: "IX_movimiento_categoria_id_usuario_id",
                table: "movimiento",
                columns: new[] { "categoria_id", "usuario_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_movimiento_categoria_del_ambito",
                table: "movimiento",
                columns: new[] { "categoria_id", "usuario_id" },
                principalTable: "categoria",
                principalColumns: new[] { "id", "usuario_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_movimiento_categoria_del_ambito",
                table: "movimiento");

            migrationBuilder.DropIndex(
                name: "IX_movimiento_categoria_id_usuario_id",
                table: "movimiento");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_categoria_id_usuario",
                table: "categoria");

            migrationBuilder.CreateIndex(
                name: "IX_movimiento_categoria_id",
                table: "movimiento",
                column: "categoria_id");

            migrationBuilder.AddForeignKey(
                name: "FK_movimiento_categoria_categoria_id",
                table: "movimiento",
                column: "categoria_id",
                principalTable: "categoria",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
