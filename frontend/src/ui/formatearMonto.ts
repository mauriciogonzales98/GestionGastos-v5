import type { Moneda } from '../api/tipos';

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
 * **La escala sale del catálogo y no de `Intl`, desde la feature 011** (`FR-019`, D-08).
 * `Intl.NumberFormat` con `style: 'currency'` deduce cuántos decimales usa una moneda a partir de su
 * código ISO. Si esa deducción mandara, agregar al catálogo una moneda cuya escala no coincida con
 * la que su código tiene asignada produciría montos redondeados a una escala que nadie eligió, y en
 * silencio — que es exactamente lo contrario de `PRD:RF-32`, donde la moneda es un dato.
 *
 * `decimales` es opcional para no obligar a cada punto de llamada a tenerlo a mano: sin él se cae en
 * lo que `Intl` deduzca, que es lo que hacía hasta la 010. Los tres lugares que muestran plata sí lo
 * pasan.
 *
 * **Vive acá y no dentro del listado desde la feature 010.** Nació privado de `ListadoMovimientos`
 * porque era el único lugar donde se mostraba plata; el resumen y el dashboard muestran plata
 * también, y una segunda copia sería una segunda que se olvida del `try` — o sea, el mismo
 * `RangeError` esperando en otra pantalla.
 */
export function formatearMonto(monto: number, monedaCodigo: string, decimales?: number): string {
  // La escala va en las dos ramas: la degradada también tiene que respetarla, o una moneda sin
  // centavos mostraría centavos justo cuando su código es el que `Intl` no entiende.
  const escala =
    decimales === undefined
      ? {}
      : { minimumFractionDigits: decimales, maximumFractionDigits: decimales };

  try {
    return new Intl.NumberFormat('es-AR', {
      style: 'currency',
      currency: monedaCodigo,
      ...escala,
    }).format(monto);
  } catch {
    // No es un catch silencioso: el fallo se muestra, en la forma de un monto sin símbolo. No hay
    // nada que reportarle a la persona —no puede hacer nada con esto— y el dato se sigue leyendo.
    return `${new Intl.NumberFormat('es-AR', escala).format(monto)} ${monedaCodigo}`;
  }
}

/**
 * Cuántos decimales usa la moneda de ese código, según el catálogo.
 *
 * Devuelve `undefined` cuando el catálogo todavía no llegó o cuando no tiene esa moneda, y ahí
 * `formatearMonto` cae en lo que `Intl` deduzca. **Es la degradación correcta**: preferir la escala
 * deducida antes que no mostrar el monto, y no inventar un 2 acá — un valor por defecto escrito a
 * mano sería exactamente la lista de monedas cableada que `verificar-monedas.sh` persigue.
 */
export function decimalesDe(monedas: Moneda[] | undefined, codigo: string): number | undefined {
  return monedas?.find((m) => m.codigo === codigo)?.decimales;
}
