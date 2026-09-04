import { useEffect, useRef } from 'react';
import type { Categoria, Moneda, Movimiento, MovimientoEditado } from '../api/tipos';
import { CamposDelMovimiento } from './CamposDelMovimiento';

export interface PropsVentanaDeEdicion {
  movimiento: Movimiento;
  categorias: Categoria[];
  monedas: Moneda[];
  hoy: string;
  onGuardar: (cambio: MovimientoEditado) => void | Promise<void>;
  onCerrar: () => void;
}

/**
 * La ventana emergente para corregir un movimiento ya registrado (FR-011, `PRD:FR-07`).
 *
 * **Es un `<dialog>` nativo con `showModal()`**, no un `<div role="dialog">` con el foco manejado a
 * mano (D-07). La plataforma ya trae lo que una modal necesita y lo que una hecha a mano
 * reimplementa mal: el foco atrapado adentro, el cierre con `Escape`, el fondo inerte, el rol de
 * accesibilidad y el `::backdrop`. `AGENTS.md` prohíbe agregar librerías sin justificarlas, y acá
 * no hay nada que justificar.
 *
 * **Corregir la moneda es la tercera mitigación del riesgo central de multi-moneda** que anota
 * PRD-001 —cargar en la moneda equivocada—, y la que actúa cuando el error ya ocurrió. Por eso la
 * ventana abre con todo cargado: si un campo llegara vacío, quien viene a corregir la moneda
 * tendría que reescribir el monto, y un dígito de menos convierte una corrección en un dato falso.
 */
export function VentanaDeEdicion({
  movimiento,
  categorias,
  monedas,
  hoy,
  onGuardar,
  onCerrar,
}: PropsVentanaDeEdicion) {
  const ventana = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    // `showModal()` y no el atributo `open`: sólo la llamada vuelve inerte el fondo y atrapa el
    // foco. Un `<dialog open>` se ve igual y deja el resto de la página alcanzable con Tab.
    ventana.current?.showModal();
  }, []);

  return (
    <dialog
      ref={ventana}
      aria-label="Editar movimiento"
      // `Escape` lo maneja el navegador y emite `close`. Escucharlo acá —y no un `onKeyDown`— es lo
      // que hace que la ventana se cierre por las DOS vías con el mismo camino.
      onClose={onCerrar}
    >
      <h2>Editar movimiento</h2>

      <CamposDelMovimiento
        categorias={categorias}
        monedas={monedas}
        hoy={hoy}
        iniciales={{
          tipo: movimiento.tipo,
          monto: movimiento.monto,
          categoriaId: movimiento.categoriaId,
          // La moneda del movimiento se busca por CÓDIGO, que es lo único que la respuesta trae. Si
          // el catálogo todavía no llegó, queda sin elegir y los campos derivan la predeterminada.
          monedaId: monedas.find((m) => m.codigo === movimiento.monedaCodigo)?.id,
          fecha: movimiento.fecha,
        }}
        etiquetaDeEnvio="Guardar cambios"
        mensajeDeFallo="No se pudo guardar el cambio. Volvé a intentarlo."
        onGuardar={(valores) =>
          onGuardar({
            tipo: valores.tipo,
            monto: valores.monto,
            categoriaId: valores.categoriaId,
            monedaId: valores.monedaId,
            // Obligatoria al editar: ausente significaría "hoy" y el movimiento saltaría de fecha
            // en silencio. Los campos siempre la traen, así que acá no hay nada que decidir.
            fecha: valores.fecha,
          })
        }
      />

      {/* Cancelar cierra sin guardar. `formMethod="dialog"` sería más corto y ataría el botón al
          form de los campos, que es el que envía: se prefiere el handler explícito. */}
      <button type="button" onClick={() => ventana.current?.close()}>
        Cancelar
      </button>
    </dialog>
  );
}
