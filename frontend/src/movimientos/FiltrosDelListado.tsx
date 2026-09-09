import { useId, useState } from 'react';
import type { AcotadoDelListado } from '../api/cliente';
import type { Categoria, Moneda } from '../api/tipos';
import { ControlesDelPeriodo } from '../periodo/ControlesDelPeriodo';

export interface PropsFiltrosDelListado {
  /** El catálogo de categorías activas, que es lo que `GET /api/categorias` devuelve. */
  categorias: Categoria[];
  /** El catálogo de monedas. Sale de acá y nunca de una lista escrita a mano (`PRD:RF-32`). */
  monedas: Moneda[];
  /**
   * El período con el que arranca el control. Es el que el servidor eligió: el `desde`/`hasta` del
   * resumen del mes en curso que la pantalla ya tiene cargado (`FR-015`).
   */
  desdeInicial?: string;
  hastaInicial?: string;
  /** Pide el listado con los tres acotados juntos. Una sola vez por "Aplicar" (`NFR-005`). */
  onAplicar: (acotado: AcotadoDelListado) => void;
  /** El mensaje con el que el servidor rechazó el período, o `null` (`FR-018`). */
  errorDelPeriodo: string | null;
}

/**
 * La barra de acotado del listado: categoría, rango de fechas y moneda (`PRD:RF-17`, `PRD:RF-18`,
 * `PRD:RF-28`).
 *
 * **Salda la deuda D9-01.** El servidor acota por los cuatro desde FEAT-001b y hasta esta feature
 * sólo la moneda tenía un control; el comentario de `AcotadoDelListado` decía que ese tipo era
 * "donde va a crecer cuando se salde", y esto es saldarla.
 *
 * **Un solo "Aplicar" para los tres, y es una decisión** (D-06). Aplicar en cada cambio significaría
 * hasta tres peticiones para expresar una sola pregunta, y el listado ya arrastra una guarda contra
 * la respuesta que llega tarde justamente porque dos peticiones en vuelo se pisan. Peor: un rango se
 * escribe dígito a dígito, y aplicar en cada cambio dispararía una petición rechazada por cada tecla
 * con su cartel de error apareciendo y desapareciendo mientras la persona escribe.
 *
 * El costo, dicho: el acotado por moneda **dejó** de aplicarse solo al cambiar el `<select>`, que es
 * como funcionaba desde la feature 009. Se acepta porque la alternativa —dos interacciones distintas
 * en la misma barra, una que aplica sola y dos que esperan un botón— es peor para quien la usa que
 * para quien la escribe.
 *
 * **No valida nada del período.** Eso es de `ControlesDelPeriodo`, que a su vez no valida nada
 * porque `PeriodoPedido` es el único intérprete de `desde` y `hasta` (D-05).
 */
export function FiltrosDelListado({
  categorias,
  monedas,
  desdeInicial,
  hastaInicial,
  onAplicar,
  errorDelPeriodo,
}: PropsFiltrosDelListado) {
  const idCategoria = useId();
  const idMoneda = useId();

  // Las etiquetas dicen "Acotar por" y no sólo "Categoría" y "Moneda": el formulario de registro,
  // que está en la misma pantalla, ya tiene un campo con cada uno de esos dos nombres. Dos combos
  // homónimos son indistinguibles para quien recorre con un lector de pantalla — y también lo eran
  // para los tests, que fue como se descubrió.

  /**
   * Lo elegido, que no es lo aplicado. Cadenas y no `number | null` porque son valores de un
   * `<select>`: convertirlos de ida y vuelta en cada render abre la posibilidad de que el control
   * muestre una cosa y el estado guarde otra.
   */
  const [categoria, setCategoria] = useState('');
  const [moneda, setMoneda] = useState('');

  return (
    <section className="l-pila" aria-label="Acotar el listado">
      <div className="l-fila l-filtros">
        <div className="l-pila c-campo">
          <label htmlFor={idCategoria}>Acotar por categoría</label>
          <select
            id={idCategoria}
            value={categoria}
            onChange={(evento) => setCategoria(evento.target.value)}
          >
            {/* "Todas" es el valor por omisión (`PRD:RF-17`), y es la opción vacía: no se manda
                ningún `categoriaId` y el servidor devuelve todo. */}
            <option value="">Todas las categorías</option>
            {/* El catálogo trae **sólo las activas**, que es lo que corresponde: una categoría dada
                de baja no se puede elegir, pero sus movimientos siguen existiendo y aparecen cuando
                no se acota. Acotar no es el desglose del resumen — allá filtrar por activa está
                prohibido y `verificar-desglose.sh` lo vigila. */}
            {categorias.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nombre}
              </option>
            ))}
          </select>
        </div>

        <div className="l-pila c-campo">
          <label htmlFor={idMoneda}>Acotar por moneda</label>
          <select
            id={idMoneda}
            value={moneda}
            onChange={(evento) => setMoneda(evento.target.value)}
          >
            <option value="">Todas las monedas</option>
            {monedas.map((m) => (
              <option key={m.id} value={m.id}>
                {m.nombre}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* El rango y el botón: el mismo componente que usa el dashboard, con su mensaje de rechazo
          al lado de los campos que lo produjeron (D-05, FR-018). El "Aplicar" de este formulario es
          el de los TRES acotados: los dos selectores de arriba no aplican solos. */}
      <ControlesDelPeriodo
        /**
         * **`key` con el período inicial, para que llegue cuando llega.**
         *
         * `ControlesDelPeriodo` lee sus valores iniciales sólo al montarse — es lo mismo que hace
         * `CamposDelMovimiento`— y el resumen del que salen llega después del primer render. Sin
         * `key`, los campos se quedaban vacíos para siempre y `FR-015` no se cumplía.
         *
         * La `key` es el período y no un contador: las recargas posteriores del resumen —tras cada
         * alta, edición o borrado— devuelven el mismo mes, así que la `key` no cambia y **no se
         * remonta**. Si se remontara en cada recarga, borraría un rango a medio escribir. Es la
         * misma decisión que la `key` de `VentanaDeEdicion`, y por el mismo motivo: que no dependa
         * de un efecto de sincronización sino de la estructura.
         */
        key={desdeInicial ?? 'sin-periodo'}
        desdeInicial={desdeInicial}
        hastaInicial={hastaInicial}
        error={errorDelPeriodo}
        onAplicar={(desde, hasta) =>
          onAplicar({
            categoriaId: categoria === '' ? null : Number(categoria),
            monedaId: moneda === '' ? null : Number(moneda),
            desde,
            hasta,
          })
        }
      />
    </section>
  );
}
