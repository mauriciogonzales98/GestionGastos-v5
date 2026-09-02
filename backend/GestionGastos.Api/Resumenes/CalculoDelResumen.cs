using GestionGastos.Api.Dominio;
using GestionGastos.Api.Movimientos;
using GestionGastos.Api.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Resumenes;

/// <summary>
/// Compone el resumen a partir del agregado y del catálogo de monedas.
///
/// Son dos lecturas y cada una responde una pregunta distinta: **qué pasó** —el agregado, acotado
/// por cuenta— y **sobre qué monedas hay que informar** —el catálogo, que no es de nadie—. La
/// segunda es la que hace que un período sin movimientos devuelva ceros en lugar de una lista
/// vacía: si las monedas salieran del resultado del agregado, no habría ninguna sobre la que
/// informar y AC-31 quedaría a cargo del cliente (D-05).
///
/// La composición es en memoria y no es traer el listado para sumarlo a mano: la suma la hace el
/// motor. Lo que llega acá son, como mucho, monedas × tipos × categorías filas — decenas.
/// </summary>
public static class CalculoDelResumen
{
    public static async Task<Resumen> CalcularAsync(
        GestionGastosDbContext contexto,
        long usuarioId,
        RangoDeFechas rango,
        CancellationToken cancelacion = default)
    {
        var agregado = await MovimientosConsulta
            .Agrupado(contexto, usuarioId, rango)
            .ToListAsync(cancelacion);

        // El catálogo se ordena por id para que la respuesta sea estable entre pedidos: sin orden
        // explícito, el motor puede devolverlas como le convenga y la pantalla reordenaría sola.
        var monedas = await contexto.Monedas
            .OrderBy(m => m.Id)
            .Select(m => new { m.Id, m.Codigo })
            .ToListAsync(cancelacion);

        return new Resumen(
            rango.Desde,
            rango.Hasta,
            [.. monedas.Select(moneda => DeLaMoneda(moneda.Id, moneda.Codigo, agregado))]);
    }

    /// <summary>
    /// Los cuatro números de una moneda, todos derivados de las MISMAS filas.
    ///
    /// Ésa es la propiedad que sostiene INV-02 —la suma del desglose es el total gastado—: no se
    /// verifica al final, se cumple porque no hay dos fuentes que puedan discrepar.
    /// </summary>
    private static ResumenPorMoneda DeLaMoneda(
        short monedaId, string codigo, List<MontoAgrupado> agregado)
    {
        var suyas = agregado.Where(f => f.MonedaId == monedaId).ToList();

        var ingresado = suyas.Where(f => f.Tipo == TipoMovimiento.Ingreso).Sum(f => f.Total);
        var gastado = suyas.Where(f => f.Tipo == TipoMovimiento.Gasto).Sum(f => f.Total);

        return new ResumenPorMoneda(
            monedaId,
            codigo,
            ingresado,
            gastado,
            ingresado - gastado,
            // Sólo los gastos (RF-19). Un ingreso colado acá no rompería ningún total —el gastado
            // seguiría bien— pero pondría una barra de plata que ENTRÓ en el gráfico de "en qué se
            // me va la plata", y de paso rompería INV-02: la suma del desglose dejaría de dar el
            // total gastado.
            //
            // Se filtra sobre las mismas filas de las que salen los totales, no con una consulta
            // aparte: es lo que mantiene la igualdad estructural (D-04).
            //
            // De mayor a menor, y el empate lo desempata el id. El desempate NO es prolijidad: el
            // agregado llega sin `ORDER BY` a propósito (`MovimientosConsulta.Agrupado`), así que
            // dos categorías con el mismo total salen en el orden que el motor elija ese día — y
            // comprobado, sale el de carga. Sin desempatar, las barras del gráfico se intercambian
            // solas entre dos pedidos idénticos. Es el mismo motivo por el que el catálogo de
            // monedas se ordena por id unas líneas más arriba.
            [.. suyas
                .Where(f => f.Tipo == TipoMovimiento.Gasto)
                .OrderByDescending(f => f.Total)
                .ThenBy(f => f.CategoriaId)
                .Select(f => new TotalPorCategoria(f.CategoriaId, f.CategoriaNombre, f.Total))]);
    }
}
