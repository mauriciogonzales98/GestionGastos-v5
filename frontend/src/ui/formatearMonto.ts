/**
 * El monto con el símbolo de su moneda. `Intl` sabe el símbolo de cada código ISO 4217.
 *
 * **El `try` no es defensivo por las dudas: es por un dato que el esquema admite.**
 * `Intl.NumberFormat` con `style: 'currency'` exige **tres letras ASCII** y lanza `RangeError` con
 * cualquier otra cosa. La columna `moneda.codigo` es `char(3)`, que garantiza tres caracteres pero
 * no tres letras: `'BT1'` entra sin que nada lo rechace.
 *
 * Hasta la feature 009 eso era inalcanzable —se podían agregar monedas al catálogo pero no
 * registrar movimientos en ellas—; desde que la moneda se elige, ese dato llega hasta acá. Y un
 * `RangeError` en un render no ensucia una fila: sube, React desmonta el árbol, y la cuenta se
 * queda con la pantalla en blanco hasta que alguien borre el movimiento por SQL.
 *
 * Se degrada al número con su código al lado, que es información suficiente y nunca falla. Lo que
 * el esquema debería decir —que un código es tres letras— es un CHECK que le falta a la columna, y
 * está anotado como deuda D9-09: este guardarraíl protege además a los datos que ya estén cargados.
 *
 * **Vive acá y no dentro del listado desde la feature 010.** Nació privado de `ListadoMovimientos`
 * porque era el único lugar donde se mostraba plata; el resumen y el dashboard muestran plata
 * también, y una segunda copia sería una segunda que se olvida del `try` — o sea, el mismo
 * `RangeError` esperando en otra pantalla.
 */
export function formatearMonto(monto: number, monedaCodigo: string): string {
  try {
    return new Intl.NumberFormat('es-AR', {
      style: 'currency',
      currency: monedaCodigo,
    }).format(monto);
  } catch {
    // No es un catch silencioso: el fallo se muestra, en la forma de un monto sin símbolo. No hay
    // nada que reportarle a la persona —no puede hacer nada con esto— y el dato se sigue leyendo.
    return `${new Intl.NumberFormat('es-AR').format(monto)} ${monedaCodigo}`;
  }
}
