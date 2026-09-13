using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionGastos.Api.Migrations
{
    /// <summary>
    /// **Las diez categorías compartidas pasan a ser diez por cuenta** (FR-010 a FR-014).
    ///
    /// Está escrita a mano y no por el scaffolding, y no por gusto: generada sola, esta migración
    /// pone los <c>DELETE</c> de las diez filas sembradas **antes** del reapuntado de los
    /// movimientos, y falla contra la clave foránea que los une. El orden correcto es el de abajo
    /// (research D-04 de la feature 013).
    ///
    /// **Las copias salen de lo que hay en la base, no de una lista escrita acá** (D-02). Una
    /// migración es un hecho histórico y tiene que seguir siendo correcta dentro de dos años, cuando
    /// el catálogo inicial del código haya cambiado: copiando lo que efectivamente existía, migra esa
    /// base y no una imaginaria.
    ///
    /// **Quién verifica que salió bien: las restricciones mismas.** El último paso falla si quedó
    /// alguna categoría sin dueño; la migración siguiente falla si quedó algún movimiento fuera de su
    /// ámbito. No hace falta una comprobación aparte.
    ///
    /// **Y por eso los tres pasos de datos van juntos y el único `ALTER TABLE` va último.** Una
    /// migración NO es atómica por el hecho de ser una migración: en MySQL un `ALTER TABLE` confirma
    /// la transacción abierta y la termina, así que todo lo que venga después queda fuera de ella y
    /// ningún `ROLLBACK` lo deshace. Medido el 2026-09-13 —un `INSERT` posterior a un `ALTER`
    /// sobrevivió a un `ROLLBACK` explícito—, y la primera versión de esta migración lo ignoraba:
    /// abría con un `ADD COLUMN` para una columna de trabajo, y un fallo posterior la dejaba puesta
    /// y hacía que el reintento muriera con `Duplicate column name`. `FR-014` pide fallar en vez de
    /// completarse a medias, y eso incluye poder volver a intentarlo.
    ///
    /// **La columna de trabajo desapareció, y el emparejamiento no perdió precisión.** Lo que hacía
    /// falta era distinguir la copia de "Comida" de una "Comida" que la cuenta ya tuviera dada de
    /// baja (D-03), y eso lo resuelve el `discriminador`: vale `0` en las activas y el propio `id` en
    /// las dadas de baja, y el índice único `(usuario_id, nombre, tipo, discriminador)` garantiza que
    /// para un ámbito, un nombre y un tipo haya **a lo sumo una** fila con `discriminador = 0`. El
    /// emparejamiento es unívoco por construcción, y lo garantiza una restricción y no la suerte.
    /// </summary>
    public partial class CategoriasPorCuenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            System.ArgumentNullException.ThrowIfNull(migrationBuilder);

            // 1 · Una copia de cada predefinida para cada cuenta que exista.
            //
            // Las copias salen de lo que HAY en la base y no de una lista escrita acá (D-02), y
            // nacen activas con `discriminador = 0` — que es lo que el paso siguiente usa para
            // encontrarlas sin ambigüedad.
            migrationBuilder.Sql("""
                INSERT INTO categoria (nombre, tipo, usuario_id, activa, discriminador)
                SELECT c.nombre, c.tipo, u.id, c.activa, 0
                FROM usuario u
                CROSS JOIN categoria c
                WHERE c.usuario_id IS NULL
                """);

            // 2 · Cada movimiento pasa a la copia de SU dueño.
            //
            // Se llega a la copia pasando por la predefinida a la que el movimiento apunta hoy
            // —`compartida`, que todavía existe— y de ahí a la fila del mismo nombre y tipo dentro
            // del ámbito del dueño.
            //
            // **El `discriminador = 0` es lo que vuelve unívoco el emparejamiento, y no es un
            // detalle.** Sin él, una cuenta que ya tuviera una "Comida" DADA DE BAJA daría dos
            // candidatas y el movimiento se iría a la equivocada, en silencio y sin error (D-03).
            // Con él, el índice único `(usuario_id, nombre, tipo, discriminador)` garantiza que haya
            // a lo sumo una: las dadas de baja llevan su propio `id` ahí y quedan fuera.
            migrationBuilder.Sql("""
                UPDATE movimiento m
                JOIN categoria compartida
                  ON compartida.id = m.categoria_id
                 AND compartida.usuario_id IS NULL
                JOIN categoria copia
                  ON copia.usuario_id = m.usuario_id
                 AND copia.nombre = compartida.nombre
                 AND copia.tipo = compartida.tipo
                 AND copia.discriminador = 0
                SET m.categoria_id = copia.id
                """);

            // 3 · Y recién ahora se van las compartidas. Si algún movimiento hubiera quedado
            // apuntando a una de ellas, la clave foránea frena este DELETE y los tres pasos se
            // deshacen juntos — que es lo que tiene que pasar.
            migrationBuilder.Sql("DELETE FROM categoria WHERE usuario_id IS NULL");

            // 4 · FR-001 en la base. Falla si quedó una categoría sin dueño, y ésa es justamente la
            // verificación de FR-014 que no hace falta escribir aparte.
            //
            // **Va último porque es DDL**: confirma la transacción y la termina, así que cualquier
            // paso de datos escrito debajo de esta línea dejaría de poder deshacerse.
            migrationBuilder.Sql(
                "ALTER TABLE categoria MODIFY usuario_id BIGINT NOT NULL");
        }

        /// <summary>
        /// Vuelve al modelo de las diez compartidas.
        ///
        /// **No es prolijidad: es lo que hace testeable a esta migración.** La única forma honesta de
        /// verificar una migración de datos es fabricar el estado anterior, y la técnica que este
        /// repositorio ya usa —<c>MigracionDeCuentasTests</c>— baja el esquema, siembra con SQL crudo
        /// y lo vuelve a subir. Bajar el esquema ejecuta esto.
        ///
        /// **Tiene un límite conocido y conviene decirlo.** Después del <c>Up</c> no queda ninguna
        /// marca que distinga una copia de una categoría que la cuenta creó a mano con ese mismo
        /// nombre: la columna temporal se borró. Así que la vuelta empareja por <c>(nombre, tipo)</c>
        /// —que en esta dirección no es ambiguo, porque los nombres de las diez compartidas son
        /// únicos por tipo— y trata como copia a toda categoría **activa** que coincida con una de
        /// ellas. Una "Comida" de gasto creada a mano se va con las copias. Es aceptable porque la
        /// vuelta atrás existe para el banco de pruebas, no para revertir producción en caliente
        /// (la spec asume que no hay datos en producción). Y si algo saliera mal, sale ruidoso: un
        /// movimiento que quedara apuntando a una categoría que el último paso intenta borrar frena
        /// el borrado contra la clave foránea.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            System.ArgumentNullException.ThrowIfNull(migrationBuilder);

            // 1 · El dueño vuelve a ser opcional: sin esto no entra ninguna fila compartida.
            migrationBuilder.Sql(
                "ALTER TABLE categoria MODIFY usuario_id BIGINT NULL");

            // 2 · Las diez compartidas, con los identificadores que tenían en el esquema anterior.
            //
            // Acá sí va la lista literal, y es correcto: lo que se está reconstruyendo es el estado
            // de ESE esquema, que es un hecho del pasado y no cambia. Es lo contrario del `Up`, que
            // copia lo que encuentra justamente porque mira hacia adelante.
            migrationBuilder.InsertData(
                table: "categoria",
                columns: ["id", "activa", "nombre", "tipo", "usuario_id"],
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
                    { 10, 1ul, "Otros", (sbyte)1, null },
                });

            // 3 · Los movimientos vuelven a la compartida equivalente.
            migrationBuilder.Sql("""
                UPDATE movimiento m
                JOIN categoria copia ON copia.id = m.categoria_id
                JOIN categoria compartida
                  ON compartida.usuario_id IS NULL
                 AND compartida.nombre = copia.nombre
                 AND compartida.tipo = copia.tipo
                SET m.categoria_id = compartida.id
                WHERE copia.usuario_id IS NOT NULL AND copia.activa = 1
                """);

            // 4 · Y las copias se van.
            migrationBuilder.Sql("""
                DELETE copia FROM categoria copia
                JOIN categoria compartida
                  ON compartida.usuario_id IS NULL
                 AND compartida.nombre = copia.nombre
                 AND compartida.tipo = copia.tipo
                WHERE copia.usuario_id IS NOT NULL AND copia.activa = 1
                """);
        }
    }
}
