import type { ComponentType } from 'react';

export interface PropsBotonIcono {
  /** Nombre accesible completo: "Renombrar Supermercado", "Eliminar el gasto del 3/9 en Comida". */
  nombre: string;
  /** Rótulo corto visible al pasar el puntero o recibir foco: "Renombrar", "Eliminar". */
  accion: string;
  icono: ComponentType;
  onClick: () => void;
}

/**
 * Un botón de **sólo ícono**, con las tres reglas de `FR-045` puestas en un solo lugar.
 *
 * Un botón sin palabra no se vuelve accesible por llevar un ícono claro: hacen falta las tres cosas
 * juntas —nombre accesible completo, el ícono y el rótulo fuera del árbol de accesibilidad, y la
 * palabra a la vista cuando alguien la busca—. Escritas a mano en cada fila, la cuarta se olvida; y
 * la que se olvida no se nota hasta que alguien recorre la tabla con un lector de pantalla
 * (research D-06).
 *
 * **Dos nombres y no uno**: `nombre` es lo que se anuncia y dice sobre qué actúa —"Renombrar
 * Supermercado"—; `accion` es lo que se lee al apoyar el puntero, y ahí el contexto ya está a la
 * vista, así que repetir el nombre de la fila sería ruido. El rótulo va `aria-hidden` justamente
 * para que no se sume: un lector anunciaría "Renombrar Supermercado Renombrar".
 *
 * **Por qué no `title`**, que sería un atributo en lugar de un componente: el `title` del navegador
 * no aparece al llegar con el teclado, que es exactamente lo que `FR-042` exige, y en un teléfono no
 * aparece nunca. Cumple para quien usa mouse y no cumple para nadie más.
 *
 * El rótulo **está siempre en el DOM** y lo esconde CSS, en lugar de montarse al apoyar el puntero.
 * Es la misma razón por la que el modo oscuro no pasa por JavaScript: lo que depende de `:hover` y
 * `:focus-visible` lo resuelve el navegador sin que React se entere, y así no hay un estado de
 * "estoy apuntando a este botón" que pueda quedar desincronizado del puntero real.
 */
export function BotonIcono({ nombre, accion, icono: Icono, onClick }: PropsBotonIcono) {
  return (
    /* `type="button"`: estas filas viven dentro de pantallas con formularios, y sin esto un
       `<button>` envía el que lo contenga. */
    <button type="button" className="c-boton-icono" aria-label={nombre} onClick={onClick}>
      <Icono />
      <span className="c-boton-icono__rotulo" aria-hidden="true">
        {accion}
      </span>
    </button>
  );
}
