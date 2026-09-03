using System.Diagnostics;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// **Toda barrera del repositorio tiene que estar registrada en git como ejecutable.**
///
/// Es la segunda vez que este proyecto pierde un bit de ejecución: la primera fue `FIX-002`, con
/// `verificar-linter.sh`, y la segunda fue `verificar-monedas.sh` — que corrió en verde todo el
/// desarrollo y falló en CI con `Permission denied` y exit 126.
///
/// **Por qué se escapa siempre.** En Windows y en WSL sobre un montaje de Windows, `core.filemode`
/// vale `false`: el `chmod` cambia el archivo en disco y **no** llega al índice de git. En local el
/// script corre perfecto, así que nada avisa; el checkout de CI trae el modo que git guardó, y ahí
/// aparece. Ninguna cantidad de correr la barrera en local puede detectarlo.
///
/// Este test mira el índice de git —no el disco— porque el índice es lo que viaja.
/// </summary>
public class BarrerasEjecutablesTests
{
    [Fact]
    public void Todas_Las_Barreras_Estan_Registradas_Como_Ejecutables_En_Git()
    {
        var raiz = RaizDelRepositorio();

        var salida = CorrerGit(raiz, "ls-files", "-s", "backend/verificar-*.sh");

        var barreras = salida
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(linea => linea.Split('\t', 2))
            .Select(partes => (Modo: partes[0].Split(' ')[0], Ruta: partes[1].Trim()))
            .ToList();

        Assert.NotEmpty(barreras);

        var sinBit = barreras.Where(b => b.Modo != "100755").Select(b => b.Ruta).ToList();

        Assert.True(
            sinBit.Count == 0,
            $"Estas barreras no están registradas como ejecutables en git: {string.Join(", ", sinBit)}. " +
            "En local van a correr igual —`core.filemode` es `false` en Windows y en WSL sobre un " +
            "montaje de Windows, así que el `chmod` no llega al índice— y en CI van a fallar con " +
            "`Permission denied` y exit 126. Se arregla con: " +
            $"git update-index --chmod=+x {string.Join(" ", sinBit)}");
    }

    private static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null && !Directory.Exists(Path.Combine(directorio.FullName, ".git")))
        {
            directorio = directorio.Parent;
        }

        Assert.NotNull(directorio);
        return directorio!.FullName;
    }

    private static string CorrerGit(string raiz, params string[] argumentos)
    {
        using var proceso = Process.Start(new ProcessStartInfo("git")
        {
            WorkingDirectory = raiz,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        }.Tap(inicio =>
        {
            foreach (var argumento in argumentos)
            {
                inicio.ArgumentList.Add(argumento);
            }
        }));

        Assert.NotNull(proceso);
        var salida = proceso!.StandardOutput.ReadToEnd();
        proceso.WaitForExit();

        Assert.True(proceso.ExitCode == 0, $"`git {string.Join(' ', argumentos)}` falló.");
        return salida;
    }
}

internal static class ExtensionesDeInicio
{
    /// <summary>Configura el objeto y lo devuelve, para poder armarlo en una sola expresión.</summary>
    public static T Tap<T>(this T valor, Action<T> configurar)
    {
        configurar(valor);
        return valor;
    }
}
