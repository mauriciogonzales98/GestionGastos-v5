using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionGastos.Api.Migrations
{
    /// <summary>
    /// "Sin nota" pasa a tener **una sola** forma en el almacenamiento: la ausencia de valor.
    ///
    /// Es la deuda D12-08, la única que la feature 012 creó y la única que nació aceptada a
    /// sabiendas. Sus *Clarifications* decidieron que el esquema no eligiera entre `NULL` y `''`, y
    /// eso dejó la invariante de `FR-005` sostenida por la lectura —`MovimientoDto`, `FR-011`— en
    /// vez de por el almacenamiento (`FR-014`).
    /// </summary>
    public partial class NotaSinCadenaVacia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La normalización va ANTES de la restricción, y no es ceremonia: MySQL se niega a
            // agregar un CHECK que las filas existentes ya incumplen, así que sin este UPDATE la
            // migración sería inaplicable sobre cualquier base que tuviera un `''` guardado.
            //
            // No se conoce ninguna: la aplicación normaliza al escribir desde que la columna existe
            // (`ValidacionDelMovimiento.NotaNormalizada`, feature 012), así que ninguna fila escrita
            // por la API puede tener la cadena vacía. Es exactamente por eso que va — lo que esta
            // migración cierra es el camino que NO pasa por la aplicación, y ese mismo camino es el
            // que pudo haber dejado una fila antes de hoy.
            migrationBuilder.Sql("UPDATE movimiento SET nota = NULL WHERE nota = ''");

            migrationBuilder.AddCheckConstraint(
                name: "ck_movimiento_nota_sin_cadena_vacia",
                table: "movimiento",
                sql: "nota IS NULL OR nota <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La vuelta atrás sólo suelta la restricción. Las notas normalizadas por el `Up` no se
            // "des-normalizan": `NULL` y `''` significan lo mismo para todo lector, así que no hay
            // información que restaurar, y reescribirlas inventaría un estado que nadie pidió.
            migrationBuilder.DropCheckConstraint(
                name: "ck_movimiento_nota_sin_cadena_vacia",
                table: "movimiento");
        }
    }
}
