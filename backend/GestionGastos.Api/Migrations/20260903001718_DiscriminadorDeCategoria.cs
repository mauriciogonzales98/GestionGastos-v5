using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionGastos.Api.Migrations
{
    /// <summary>
    /// La columna `discriminador` y el índice único rehecho con ella (D-01).
    ///
    /// **Las diez categorías sembradas no se tocan.** El `DEFAULT 0` del `ADD COLUMN` ya las deja
    /// donde tienen que estar —activas, con el casillero 0 compartido— y ésa es la comprobación de
    /// SC-005: mismos ids, mismos nombres, mismos tipos.
    ///
    /// El andamiaje de EF había generado diez `UpdateData` con las listas de columnas y de valores
    /// VACÍAS, uno por fila sembrada. No eran inocuos: se traducían a `UPDATE categoria SET WHERE
    /// id = 1`, que no es SQL válido y hacía fallar la migración entera. Se quitaron a mano, que es
    /// lo que D-10 pedía mirar.
    /// </summary>
    public partial class DiscriminadorDeCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La clave foránea se suelta primero y se vuelve a poner al final.
            //
            // No es ceremonia: InnoDB respalda `FK_categoria_usuario_usuario_id` con el índice que
            // empiece por `usuario_id`, y el único que lo hace es justamente el que hay que rehacer.
            // Sin este paso, el DROP INDEX falla con "needed in a foreign key constraint" y la
            // migración no corre. El andamiaje de EF no lo previó.
            migrationBuilder.DropForeignKey(
                name: "FK_categoria_usuario_usuario_id",
                table: "categoria");

            migrationBuilder.DropIndex(
                name: "ux_categoria_ambito_nombre_tipo",
                table: "categoria");

            migrationBuilder.AddColumn<long>(
                name: "discriminador",
                table: "categoria",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "ux_categoria_ambito_nombre_tipo",
                table: "categoria",
                columns: new[] { "usuario_id", "nombre", "tipo", "discriminador" },
                unique: true);

            // Vuelve con el mismo nombre y el mismo RESTRICT: la FK no cambia, sólo tuvo que
            // apartarse mientras el índice que la respalda se rehacía.
            migrationBuilder.AddForeignKey(
                name: "FK_categoria_usuario_usuario_id",
                table: "categoria",
                column: "usuario_id",
                principalTable: "usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mismo baile que en Up, por el mismo motivo.
            migrationBuilder.DropForeignKey(
                name: "FK_categoria_usuario_usuario_id",
                table: "categoria");

            migrationBuilder.DropIndex(
                name: "ux_categoria_ambito_nombre_tipo",
                table: "categoria");

            migrationBuilder.DropColumn(
                name: "discriminador",
                table: "categoria");

            migrationBuilder.CreateIndex(
                name: "ux_categoria_ambito_nombre_tipo",
                table: "categoria",
                columns: new[] { "usuario_id", "nombre", "tipo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_categoria_usuario_usuario_id",
                table: "categoria",
                column: "usuario_id",
                principalTable: "usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
