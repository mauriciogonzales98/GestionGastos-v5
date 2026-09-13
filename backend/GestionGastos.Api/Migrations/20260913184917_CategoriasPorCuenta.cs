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
    /// **Quién verifica que salió bien: las restricciones mismas.** El paso 5 falla si quedó alguna
    /// categoría sin dueño; la migración siguiente falla si quedó algún movimiento fuera de su
    /// ámbito. No hace falta una comprobación aparte, y como cada migración corre en su transacción,
    /// un fallo deja la base como estaba en vez de completarse a medias (FR-014).
    /// </summary>
    public partial class CategoriasPorCuenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            System.ArgumentNullException.ThrowIfNull(migrationBuilder);

            // 1 · Una columna temporal para recordar de qué predefinida salió cada copia.
            //
            // Es lo que permite reapuntar **por identidad**. Emparejar por `(nombre, tipo)` sería
            // más corto y estaría mal: una cuenta puede tener una categoría propia DADA DE BAJA
            // homónima de una predefinida —para eso existe el discriminador—, y el JOIN por nombre
            // encontraría dos candidatas y mandaría el movimiento a la equivocada, en silencio y sin
            // error (D-03).
            migrationBuilder.Sql(
                "ALTER TABLE categoria ADD COLUMN migracion_origen_id INT NULL");

            // 2 · Una copia de cada predefinida para cada cuenta que exista.
            migrationBuilder.Sql("""
                INSERT INTO categoria (nombre, tipo, usuario_id, activa, discriminador, migracion_origen_id)
                SELECT c.nombre, c.tipo, u.id, c.activa, 0, c.id
                FROM usuario u
                CROSS JOIN categoria c
                WHERE c.usuario_id IS NULL
                """);

            // 3 · Cada movimiento pasa a la copia de SU dueño, emparejando por la columna temporal.
            migrationBuilder.Sql("""
                UPDATE movimiento m
                JOIN categoria copia
                  ON copia.usuario_id = m.usuario_id
                 AND copia.migracion_origen_id = m.categoria_id
                SET m.categoria_id = copia.id
                """);

            // 4 · Y recién ahora se van las compartidas. Si algún movimiento hubiera quedado
            // apuntando a una de ellas, la clave foránea frena este DELETE y la migración entera se
            // deshace — que es lo que tiene que pasar.
            migrationBuilder.Sql("DELETE FROM categoria WHERE usuario_id IS NULL");

            // 5 · FR-001 en la base. Falla si quedó una categoría sin dueño, y ésa es justamente la
            // verificación de FR-014 que no hace falta escribir aparte.
            migrationBuilder.Sql(
                "ALTER TABLE categoria MODIFY usuario_id BIGINT NOT NULL");

            // 6 · La columna temporal se va: cumplió su función dentro de esta misma migración.
            migrationBuilder.Sql(
                "ALTER TABLE categoria DROP COLUMN migracion_origen_id");
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
