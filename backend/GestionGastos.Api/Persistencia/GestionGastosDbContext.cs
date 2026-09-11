using GestionGastos.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Persistencia;

/// <summary>
/// El esquema de data-model.md, escrito como configuración explícita y no por convención: los
/// nombres van en <c>snake_case</c> y los tipos son los que las reglas de FR-004 a FR-011
/// necesitan, no los que EF elegiría solo.
/// </summary>
public class GestionGastosDbContext(DbContextOptions<GestionGastosDbContext> opciones)
    : DbContext(opciones)
{
    public DbSet<Movimiento> Movimientos => Set<Movimiento>();

    public DbSet<Categoria> Categorias => Set<Categoria>();

    public DbSet<Moneda> Monedas => Set<Moneda>();

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<IntentoDeAcceso> IntentosDeAcceso => Set<IntentoDeAcceso>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("usuario");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id");
            // Colación insensible a mayúsculas: es lo que hace que `Ana@x.com` y `ana@x.com` sean
            // LA MISMA cuenta, tanto para el UNIQUE como para la búsqueda del login. Sin esto el
            // UNIQUE dejaría entrar las dos y FR-002 quedaría incumplido por una diferencia que
            // ninguna persona percibe como distinta.
            e.Property(u => u.Email)
                .HasColumnName("email")
                .HasMaxLength(254)
                .UseCollation("utf8mb4_0900_ai_ci")
                .IsRequired();
            e.HasIndex(u => u.Email).IsUnique();

            // 60 caracteres en el formato bcrypt actual; 72 deja aire sin convertirlo en un `text`.
            e.Property(u => u.ContrasenaHash)
                .HasColumnName("contrasena_hash")
                .HasMaxLength(72)
                .IsRequired();
        });

        modelBuilder.Entity<IntentoDeAcceso>(e =>
        {
            e.ToTable("intento_de_acceso");

            // El email ES la clave: una fila por email presentado, y sólo mientras tenga fallos que
            // contar. Sin fila significa cero fallos, que es el estado normal de todos los emails.
            e.HasKey(i => i.Email);

            // La MISMA colación que `usuario.email`, y por el mismo motivo llevado un paso más
            // allá: si acá fuera binaria, `ana@` y `Ana@` tendrían contadores separados y el límite
            // se esquivaría cambiando una letra de mayúscula.
            e.Property(i => i.Email)
                .HasColumnName("email")
                .HasMaxLength(254)
                .UseCollation("utf8mb4_0900_ai_ci")
                .IsRequired();

            // El límite son 5: un byte sobra y deja claro que acá no se acumula una bitácora.
            e.Property(i => i.FallosConsecutivos)
                .HasColumnName("fallos_consecutivos")
                .HasColumnType("tinyint unsigned")
                .IsRequired();

            // datetime(6): la ventana son 15 minutos y los tests la recorren con precisión de
            // microsegundos adelantando el reloj.
            e.Property(i => i.UltimoFallo)
                .HasColumnName("ultimo_fallo")
                .HasColumnType("datetime(6)")
                .IsRequired();

            // Para que la purga por inactividad sea un DELETE por índice y no un recorrido.
            e.HasIndex(i => i.UltimoFallo).HasDatabaseName("ix_intento_de_acceso_ultimo_fallo");
        });

        modelBuilder.Entity<Moneda>(e =>
        {
            // El CHECK exige TRES LETRAS, no sólo tres caracteres (FR-010 de la feature 012).
            //
            // Es la deuda D11-02 —nacida como D9-09 en la 009 y pasada por D10-03— que esperaba a un
            // ticket que abriera una migración por otro motivo. La 012 la abre para la nota del
            // movimiento, y con el plan DISC-001 terminándose ahí ya no había un próximo ticket al
            // que apuntarla.
            //
            // El `char(3)` de abajo ya acotaba el LARGO desde la migración Inicial; lo que esto
            // agrega es que sean letras. Sin él, un `1X2` metido con SQL puro entraba sin protesta,
            // viajaba en el contrato y llegaba hasta `formatearMonto`, que es el cuarto lugar donde
            // esta deuda se podía cruzar y la razón por la que esa función tiene un try/catch que la
            // nombra.
            //
            // Vive SÓLO en el esquema: no hay validación de aplicación sobre el catálogo. El
            // catálogo se administra como dato (RF-32) y nadie lo escribe desde la aplicación, así
            // que un guardarraíl en el código no tendría llamador.
            // **Letras, y las minúsculas se admiten a propósito.** La revisión del PR #29 propuso
            // apretarlo a `[A-Z]{3}` —ISO 4217 define los códigos en mayúsculas— y la propuesta se
            // descartó con la evidencia en la mano, por dos razones que se suman:
            //
            //   1. **No cierra nada.** El motivo de esta deuda (D11-02, antes D9-09) es que un código
            //      que `Intl` no entiende llegue hasta `formatearMonto`, e `Intl` interpreta los
            //      códigos sin distinguir mayúsculas: medido, `ars` y `ARS` dan los dos "$ 1.234,50".
            //      Un código en minúsculas no es el caso que la restricción viene a atrapar.
            //   2. **Costaría cablear una colación en el esquema.** `codigo` usa
            //      `utf8mb4_0900_ai_ci`, que es insensible a mayúsculas, así que `REGEXP '^[A-Z]{3}$'`
            //      acepta `ars` igual; expresarlo exigiría un `COLLATE utf8mb4_0900_as_cs` dentro del
            //      CHECK. Es una dependencia del nombre de una colación, a cambio de nada.
            e.ToTable("moneda", t => t.HasCheckConstraint(
                "ck_moneda_codigo_tres_letras",
                "codigo REGEXP '^[A-Za-z]{3}$'"));
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).HasColumnName("id");
            e.Property(m => m.Codigo).HasColumnName("codigo").HasColumnType("char(3)").IsRequired();
            e.Property(m => m.Nombre).HasColumnName("nombre").HasMaxLength(30).IsRequired();
            e.Property(m => m.Simbolo).HasColumnName("simbolo").HasMaxLength(5).IsRequired();
            e.Property(m => m.Decimales).HasColumnName("decimales").HasDefaultValue((byte)2);
            e.Property(m => m.EsPredeterminada)
                .HasColumnName("es_predeterminada")
                .HasColumnType("bit(1)")
                .HasDefaultValue(false);
            e.HasIndex(m => m.Codigo).IsUnique();
        });

        modelBuilder.Entity<Categoria>(e =>
        {
            e.ToTable("categoria");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id");
            e.Property(c => c.Nombre).HasColumnName("nombre").HasMaxLength(50).IsRequired();
            e.Property(c => c.Tipo).HasColumnName("tipo").HasColumnType("tinyint");

            // Nullable a propósito: NULL = predefinida del sistema (D-06).
            e.Property(c => c.UsuarioId).HasColumnName("usuario_id");
            e.Property(c => c.Activa).HasColumnName("activa").HasColumnType("bit(1)").HasDefaultValue(true);

            // 0 mientras está activa, su propio id al darla de baja. El DEFAULT es lo que deja que
            // las diez filas sembradas sobrevivan la migración sin que nadie las toque (D-10).
            e.Property(c => c.Discriminador)
                .HasColumnName("discriminador")
                .HasColumnType("bigint")
                .HasDefaultValue(0L);

            // Impide dos categorías ACTIVAS con el mismo nombre y tipo dentro del mismo ámbito.
            // "Otros" existe en gasto y en ingreso: son dos filas, y difieren en `tipo`.
            //
            // `discriminador` entra al índice para que la unicidad conviva con la baja lógica
            // (D-01): las activas comparten el casillero 0 y chocan entre sí, y cada dada de baja
            // se lleva el suyo, así que no le ocupa el nombre a nadie. Es lo que hace posible
            // FR-009 sin aflojar FR-005.
            //
            // El nombre del índice NO cambia: es el mismo índice cumpliendo la misma regla con una
            // columna más, y renombrarlo obligaría a mirar dos nombres en el historial para
            // entender una sola cosa.
            e.HasIndex(c => new { c.UsuarioId, c.Nombre, c.Tipo, c.Discriminador })
                .IsUnique()
                .HasDatabaseName("ux_categoria_ambito_nombre_tipo");

            e.HasOne<Usuario>()
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Movimiento>(e =>
        {
            // El segundo CHECK es la deuda D12-08, y lo que fija es que "sin nota" tenga **una sola**
            // forma en el almacenamiento: la ausencia de valor. La aplicación ya escribía sólo esa
            // —`ValidacionDelMovimiento.NotaNormalizada` convierte la cadena vacía desde la feature
            // 012—, así que esto no cambia ningún camino de escritura: lo garantiza. Sin él, un
            // INSERT con SQL puro o el próximo camino que se olvide de normalizar crea el segundo
            // estado que FR-005 dice que no existe, y la invariante queda dependiendo de que nadie
            // se olvide (FR-014).
            e.ToTable("movimiento", t =>
            {
                t.HasCheckConstraint("ck_movimiento_monto_positivo", "monto > 0");
                t.HasCheckConstraint("ck_movimiento_nota_sin_cadena_vacia", "nota IS NULL OR nota <> ''");
            });
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).HasColumnName("id");
            e.Property(m => m.UsuarioId).HasColumnName("usuario_id").IsRequired();
            e.Property(m => m.Tipo).HasColumnName("tipo").HasColumnType("tinyint");

            // decimal(11,2) topa exactamente en 999.999.999,99, el techo de FR-004b (D-01).
            e.Property(m => m.Monto).HasColumnName("monto").HasColumnType("decimal(11,2)").IsRequired();

            e.Property(m => m.MonedaId).HasColumnName("moneda_id").IsRequired();
            e.Property(m => m.CategoriaId).HasColumnName("categoria_id").IsRequired();

            // `date`: sin hora ni zona horaria (D-02).
            e.Property(m => m.Fecha).HasColumnName("fecha").HasColumnType("date").IsRequired();

            // `varchar(120)` mide EXACTAMENTE el límite de FR-003, no más, y eso es deliberado
            // (D-01 de la feature 012). En utf8mb4 `varchar(n)` cuenta CARACTERES y no bytes, que es
            // la misma unidad en la que el requisito cuenta sus 120: una nota que las dos
            // validaciones aceptan entra siempre, y una que no entra nunca llegó hasta acá. Con la
            // columna más ancha que el requisito, el día que una validación falle la base aceptaría
            // la nota larga en silencio y el límite dejaría de existir sin que nada se ponga en
            // rojo. Es el mismo criterio que `decimal(11,2)` para el techo del monto.
            //
            // Anulable y SIN índice. Lo segundo es FR-007: un índice acá sólo serviría para buscar
            // por la nota, que es exactamente lo que no se hace.
            e.Property(m => m.Nota).HasColumnName("nota").HasMaxLength(120);

            // Sirve al listado de FR-007/FR-008 y al ticket 5 con 10.000 filas (RNF-01).
            // CUIDADO: este índice hace que MySQL devuelva las filas ya ordenadas aunque la
            // consulta no lo pida, así que un test que sólo mire el resultado pasa en verde con el
            // OrderBy borrado. Por eso D-04 exige verificar el orden en doble capa.
            e.HasIndex(m => new { m.UsuarioId, m.Fecha, m.Id })
                .IsDescending(false, true, true)
                .HasDatabaseName("ix_movimiento_usuario_fecha");

            e.HasOne<Usuario>().WithMany().HasForeignKey(m => m.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Categoria).WithMany().HasForeignKey(m => m.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Moneda).WithMany().HasForeignKey(m => m.MonedaId).OnDelete(DeleteBehavior.Restrict);
        });

        Sembrar(modelBuilder);
    }

    /// <summary>
    /// El catálogo inicial. Va en la migración y no en un script suelto para que crear la base y
    /// tenerla usable sean el mismo paso, también en el runner del CI y en la base de tests.
    /// </summary>
    private static void Sembrar(ModelBuilder modelBuilder)
    {
        // RF-31. Exactamente una fila con es_predeterminada = true (RF-25).
        modelBuilder.Entity<Moneda>().HasData(
            new Moneda { Id = 1, Codigo = "ARS", Nombre = "Peso argentino", Simbolo = "$", Decimales = 2, EsPredeterminada = true },
            new Moneda { Id = 2, Codigo = "USD", Nombre = "Dólar estadounidense", Simbolo = "US$", Decimales = 2, EsPredeterminada = false });

        // Las diez de FR-006, exactamente: 7 de gasto y 3 de ingreso. "Otros" está en los dos
        // tipos y son dos filas distintas; la restricción UNIQUE las admite porque difieren en
        // `tipo`. Todas nacen con usuario_id NULL, o sea predefinidas del sistema.
        modelBuilder.Entity<Categoria>().HasData(
            new Categoria { Id = 1, Nombre = "Comida", Tipo = TipoMovimiento.Gasto, Activa = true },
            new Categoria { Id = 2, Nombre = "Transporte", Tipo = TipoMovimiento.Gasto, Activa = true },
            new Categoria { Id = 3, Nombre = "Vivienda", Tipo = TipoMovimiento.Gasto, Activa = true },
            new Categoria { Id = 4, Nombre = "Servicios", Tipo = TipoMovimiento.Gasto, Activa = true },
            new Categoria { Id = 5, Nombre = "Salud", Tipo = TipoMovimiento.Gasto, Activa = true },
            new Categoria { Id = 6, Nombre = "Ocio", Tipo = TipoMovimiento.Gasto, Activa = true },
            new Categoria { Id = 7, Nombre = "Otros", Tipo = TipoMovimiento.Gasto, Activa = true },
            new Categoria { Id = 8, Nombre = "Sueldo", Tipo = TipoMovimiento.Ingreso, Activa = true },
            new Categoria { Id = 9, Nombre = "Ingreso extra", Tipo = TipoMovimiento.Ingreso, Activa = true },
            new Categoria { Id = 10, Nombre = "Otros", Tipo = TipoMovimiento.Ingreso, Activa = true });
    }
}
