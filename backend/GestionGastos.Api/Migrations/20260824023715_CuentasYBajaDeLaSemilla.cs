using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionGastos.Api.Migrations
{
    /// <summary>
    /// Las cuentas llegan y la fila semilla se va.
    ///
    /// Es un solo hecho y por eso va en una sola migración: partirlo dejaría un estado intermedio
    /// donde `usuario` ya no tiene semilla pero todavía no tiene contraseñas.
    ///
    /// El orden NO es preferencia:
    ///   1. los movimientos de la semilla, porque `movimiento.usuario_id` es una clave foránea
    ///      RESTRICT y borrar el usuario primero falla;
    ///   2. la fila de usuario;
    ///   3. recién entonces la columna `contrasena_hash`, que entra NOT NULL y **sin valor por
    ///      defecto**. Eso sólo es posible con la tabla ya vacía: agregarla antes obligaría a un
    ///      default, y un default en una columna de contraseñas dejaría pasar en silencio una fila
    ///      sin verificador.
    /// </summary>
    public partial class CuentasYBajaDeLaSemilla : Migration
    {
        private const long IdDeLaSemilla = 1L;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Los hijos primero (FR-07 / AC-09).
            migrationBuilder.Sql(
                $"DELETE FROM `movimiento` WHERE `usuario_id` = {IdDeLaSemilla};");

            // 2. Después el padre.
            migrationBuilder.DeleteData(
                table: "usuario",
                keyColumn: "id",
                keyValue: IdDeLaSemilla);

            // 3. El email se compara sin distinguir mayúsculas: `Ana@x.com` y `ana@x.com` son la
            //    misma cuenta, para el UNIQUE y para el login.
            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "usuario",
                type: "varchar(254)",
                maxLength: 254,
                nullable: false,
                collation: "utf8mb4_0900_ai_ci",
                oldClrType: typeof(string),
                oldType: "varchar(254)",
                oldMaxLength: 254)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // 4. Sin defaultValue, a propósito: la tabla ya está vacía y un default dejaría entrar
            //    una cuenta sin verificador de contraseña sin que nada se queje.
            migrationBuilder.AddColumn<string>(
                name: "contrasena_hash",
                table: "usuario",
                type: "varchar(72)",
                maxLength: 72,
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <summary>
        /// Revierte el ESQUEMA. **No restituye los datos**: los de la semilla eran datos de
        /// desarrollo y no se guardan en ningún lado, así que fabricarlos daría una base que se
        /// parece a la anterior sin serlo. Un Down que miente es peor que uno que avisa.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contrasena_hash",
                table: "usuario");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "usuario",
                type: "varchar(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(254)",
                oldMaxLength: 254,
                oldCollation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // La fila vuelve para que el esquema sea coherente con la migración anterior, pero sus
            // movimientos no: se perdieron y no hay de dónde sacarlos.
            //
            // Sin `contrasena_hash`: para cuando esta línea corre, la columna ya se eliminó arriba.
            // Incluirla hacía fallar la vuelta atrás con "no property mapped to the column".
            migrationBuilder.InsertData(
                table: "usuario",
                columns: new[] { "id", "email" },
                values: new object[] { IdDeLaSemilla, "semilla@gestiongastos.local" });
        }
    }
}
