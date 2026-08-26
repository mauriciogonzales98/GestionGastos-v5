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
            e.ToTable("moneda");
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

            // Impide dos categorías con el mismo nombre y tipo dentro del mismo ámbito. "Otros"
            // existe en gasto y en ingreso: son dos filas, y difieren en `tipo`.
            e.HasIndex(c => new { c.UsuarioId, c.Nombre, c.Tipo })
                .IsUnique()
                .HasDatabaseName("ux_categoria_ambito_nombre_tipo");

            e.HasOne<Usuario>()
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Movimiento>(e =>
        {
            e.ToTable("movimiento", t => t.HasCheckConstraint("ck_movimiento_monto_positivo", "monto > 0"));
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
