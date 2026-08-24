using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionGastos.Api.Migrations
{
    /// <inheritdoc />
    public partial class UnicaMonedaPredeterminada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RF-25 exige exactamente una moneda predeterminada, y hasta acá esa invariante vivía
            // sólo en data-model.md. El alta la daba por cierta al tomar la predeterminada del
            // catálogo: con dos filas en 1, elegía una sin criterio y podía cambiar entre
            // reinicios, registrando movimientos en una moneda arbitraria sin que nadie se entere.
            //
            // Se resuelve con una columna generada que vale 1 cuando la fila es la predeterminada
            // y NULL cuando no, más un UNIQUE encima. MySQL admite varios NULL en un índice único,
            // así que la restricción limita las predeterminadas a una sola y deja libres al resto.
            //
            // Va en SQL crudo porque EF Core no modela columnas generadas con índice único.
            migrationBuilder.Sql(
                "ALTER TABLE `moneda` " +
                "ADD COLUMN `unica_predeterminada` TINYINT " +
                "GENERATED ALWAYS AS (CASE WHEN `es_predeterminada` THEN 1 ELSE NULL END) VIRTUAL;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX `ux_moneda_unica_predeterminada` " +
                "ON `moneda` (`unica_predeterminada`);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX `ux_moneda_unica_predeterminada` ON `moneda`;");
            migrationBuilder.Sql("ALTER TABLE `moneda` DROP COLUMN `unica_predeterminada`;");
        }
    }
}
