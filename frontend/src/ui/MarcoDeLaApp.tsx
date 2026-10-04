import type { ComponentType, ReactNode } from 'react';
import { IconoCategorias, IconoDashboard, IconoMovimientos } from './iconos';

/**
 * Las tres secciones de la app con sesión.
 *
 * Es el mismo tipo `Vista` que `App.tsx` ya tenía, nombrado acá porque este componente es el que lo
 * usa como contrato. No son rutas: no hay router y no hace falta uno (research D-05).
 */
export type Seccion = 'movimientos' | 'dashboard' | 'categorias';

export interface PropsMarcoDeLaApp {
  /** La sección actual: lleva `aria-current="page"` (`FR-024`). */
  seccion: Seccion;
  /**
   * La cuenta con la que se está trabajando, por email (`FR-027`).
   *
   * La feature 015 lo reemplaza por el nombre de la persona; esta feature sólo deja el lugar donde
   * va a ir. Hasta entonces se muestra el email, como hoy: sin esto, dos cuentas en el mismo
   * navegador son indistinguibles hasta que alguien carga un gasto en la equivocada.
   */
  email: string;
  onIrA: (seccion: Seccion) => void;
  onCerrarSesion: () => void;
  /** La pantalla. Se renderiza dentro del `<main>`. */
  children: ReactNode;
}

/** Las secciones en el orden en que la barra las muestra, con su rótulo y su ícono. */
const SECCIONES: { id: Seccion; rotulo: string; Icono: ComponentType }[] = [
  { id: 'movimientos', rotulo: 'Movimientos', Icono: IconoMovimientos },
  { id: 'dashboard', rotulo: 'Dashboard', Icono: IconoDashboard },
  { id: 'categorias', rotulo: 'Categorías', Icono: IconoCategorias },
];

/**
 * El marco de la app con sesión: marca, navegación, cuenta y contenido (`FR-022` a `FR-028`).
 *
 * **Un solo `<nav>`, reubicado por CSS** (research D-05). La alternativa —uno lateral y uno
 * inferior, mostrando y ocultando según el ancho— serían dos puntos de referencia iguales para un
 * lector de pantalla, que los anuncia los dos aunque uno esté oculto, y dos lugares donde marcar la
 * sección actual que pueden quedar desincronizados. Acá la estructura es una y la decide CSS dónde
 * ponerla, con el corte de 48rem.
 *
 * **Botones y no enlaces.** La app no tiene rutas: cambiar de sección es cambiar un estado de
 * `App.tsx`, como hoy. Un enlace sin dirección a la que ir no es un enlace, y sumar un enrutador
 * sería una dependencia que la spec no pide. `aria-current` vale en cualquier elemento y es lo que
 * los lectores anuncian como "página actual" (`FR-024`).
 *
 * **El orden del DOM es marca, secciones, cuenta, contenido**, y en el teléfono las secciones se
 * **ven** abajo pero se **recorren** antes del contenido. Queda anotado para que no se lea como un
 * descuido: es el patrón de las barras inferiores de las apps nativas, y ponerlas al final del
 * recorrido obligaría a atravesar un formulario entero para cambiar de sección.
 */
export function MarcoDeLaApp({
  seccion,
  email,
  onIrA,
  onCerrarSesion,
  children,
}: PropsMarcoDeLaApp) {
  return (
    <div className="l-marco">
      {/*
        El panel lateral envuelve marca, secciones y cuenta — **y en el teléfono no existe**.
        Por debajo del corte lleva `display: contents`, así que sus tres hijos participan
        directamente de la grilla del marco: la marca y la cuenta arriba, y las secciones fijas
        abajo. Desde el corte pasa a ser un elemento de verdad, con su superficie, su borde y su
        `position: sticky`.

        Hace falta un envoltorio y no alcanza con poner la superficie en los tres hijos porque
        `FR-023` pide que la barra **permanezca a la vista mientras se desplaza el contenido**, y
        eso es una sola caja pegajosa, no tres. Con los tres sueltos el panel además terminaba
        arriba del bloque de la cuenta y la barra se veía cortada a media altura.
      */}
      <div className="c-lateral">
        <p className="c-marca c-marca--barra">Gestión de gastos</p>

        <nav aria-label="Secciones" className="c-barra">
          {SECCIONES.map(({ id, rotulo, Icono }) => (
            <button
              key={id}
              type="button"
              /* `aria-current` es el dato, y también de donde el estilo lo toma: lo que se ve y lo
               que se anuncia no pueden desincronizarse porque son lo mismo (`FR-024`). Va
               `undefined` y no `false` en las otras dos: `aria-current="false"` es un valor válido
               que algunos lectores anuncian, y lo correcto es que el atributo no esté. */
              aria-current={seccion === id ? 'page' : undefined}
              onClick={() => onIrA(id)}
            >
              <Icono />
              {/* El ícono acompaña, no reemplaza: las secciones son lugares y no acciones, así que
                llevan su palabra (`FR-046`). Un ícono solo no le dice a nadie adónde lleva. */}
              <span>{rotulo}</span>
            </button>
          ))}
        </nav>

        <div className="c-cuenta">
          {/* El email se corta con puntos suspensivos y no ensancha la barra (caso borde de la
            spec). El `title` deja el completo disponible para quien lo quiera leer. */}
          <p className="c-cuenta__email" title={email}>
            {email}
          </p>
          {/* Un `<button>` y no un enlace: cambia estado del servidor, y los enlaces son para
            navegar. Un enlace acá además sería seguible por un prefetch del navegador.
            Lleva palabra: un ícono de "salir" se confunde con volver o cerrar la ventana
            (`FR-046`). */}
          <button type="button" onClick={onCerrarSesion}>
            Cerrar sesión
          </button>
        </div>
      </div>

      <main className="l-marco__contenido">{children}</main>
    </div>
  );
}
