import { useState } from 'react';
import { ErrorDeValidacion } from '../api/cliente';
import type { Categoria, NuevaCategoria, TipoMovimiento } from '../api/tipos';
import { CampoConError } from '../ui/CampoConError';

export interface PropsPantallaCategorias {
  /**
   * El catálogo completo, predefinidas incluidas. Baja por props y no se pide acá: vive en la raíz
   * para que el selector del formulario y esta pantalla miren la MISMA lista (D-08, AC-12).
   */
  categorias: Categoria[];
  /** Crea. Lanza `ErrorDeValidacion` si el servidor la rechaza; esta pantalla lo muestra. */
  onCrear: (nueva: NuevaCategoria) => Promise<void>;
  onRenombrar: (id: number, nombre: string) => Promise<void>;
  onDarDeBaja: (id: number) => Promise<void>;
  onVolver: () => void;
}

const ERROR_GENERICO = 'No se pudo completar la operación. Volvé a intentarlo.';

/**
 * La gestión del catálogo propio (FR-017): crear, renombrar y dar de baja.
 *
 * **Las predefinidas se listan pero no ofrecen botones** (AC-03, FR-008). El servidor responde
 * `403` igual, así que esto no es la barrera: es no ofrecer un botón que sólo puede terminar en un
 * error que la persona no pidió.
 *
 * No hace peticiones por su cuenta. Todo lo que modifica el catálogo entra por props, y quien las
 * ejecuta es la raíz — que es también la que tiene el estado, así que un alta acá se ve en el
 * selector del formulario sin recargar y sin una segunda petición (FR-019, AC-13).
 *
 * **Nunca un catch silencioso**, como exige `AGENTS.md`: un `ErrorDeValidacion` va al lado de su
 * campo y cualquier otro a la región del formulario. Lo que no se sabe manejar se muestra.
 */
export function PantallaCategorias({
  categorias,
  onCrear,
  onRenombrar,
  onDarDeBaja,
  onVolver,
}: PropsPantallaCategorias) {
  const [nombre, setNombre] = useState('');
  const [tipo, setTipo] = useState<TipoMovimiento>('gasto');
  const [errorDelNombre, setErrorDelNombre] = useState<string | undefined>(undefined);

  /**
   * El error del renombre va aparte del de `errorDelNombre` **aunque el servidor los mande con la
   * misma clave**: es el mismo campo de la misma validación (FR-005), pero no el mismo control.
   * Con un solo estado, el rechazo de un renombre aparecía colgado del campo del alta —que puede
   * estar vacío o a medio escribir— y el input que lo causó se quedaba sin decir nada.
   */
  const [errorDelRenombre, setErrorDelRenombre] = useState<string | undefined>(undefined);
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  /** Qué fila está en modo renombre, y con qué texto. `null` = ninguna. */
  const [renombrando, setRenombrando] = useState<{ id: number; nombre: string } | null>(null);

  /**
   * Qué fila está pidiendo confirmación para darse de baja. `null` = ninguna.
   *
   * **La baja es un camino de ida** (Key Entities; D7-04 deja la restauración fuera de alcance a
   * propósito), así que no puede dispararse con un clic pelado. La única salida de un clic errado
   * es crear otra categoría con el mismo nombre (FR-009), y entonces los movimientos viejos quedan
   * apuntando a la vieja: el desglose muestra dos entradas homónimas y no hay forma de juntarlas.
   *
   * Es el mismo patrón de dos botones que usa el renombre, y no un `window.confirm`: ése no se
   * puede maquetar —el ticket 6 no podría tocarlo— ni se comporta igual en todos los navegadores.
   */
  const [confirmandoLaBaja, setConfirmandoLaBaja] = useState<number | null>(null);

  /**
   * Reparte un rechazo del servidor: el mensaje de `nombre` va al lado del control que lo produjo
   * y todo lo demás a la región general. Un error sin lugar donde ir es un rechazo invisible.
   *
   * <paramref name="enElCampo"/> es ese control. Lo elige quien llama porque el servidor no puede:
   * el alta y el renombre comparten la clave `nombre`, así que la única forma de saber a cuál de
   * los dos inputs pertenece el mensaje es haberlo pedido. La baja no pasa ninguno —no tiene campo
   * donde poner nada— y su mensaje cae entero en la región general.
   */
  function mostrar(error: unknown, enElCampo?: (mensaje: string | undefined) => void) {
    if (error instanceof ErrorDeValidacion) {
      const delNombre = enElCampo ? error.errores.nombre?.[0] : undefined;
      enElCampo?.(delNombre);

      const sueltos = Object.entries(error.errores)
        .filter(([clave]) => !(enElCampo && clave === 'nombre'))
        .flatMap(([, mensajes]) => mensajes);

      setErrorGeneral(sueltos.length > 0 ? sueltos.join(' ') : delNombre ? null : ERROR_GENERICO);
      return;
    }

    setErrorGeneral(ERROR_GENERICO);
  }

  function limpiarErrores() {
    setErrorDelNombre(undefined);
    setErrorDelRenombre(undefined);
    setErrorGeneral(null);
  }

  async function crear(evento: React.FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    limpiarErrores();
    setEnviando(true);

    try {
      await onCrear({ nombre, tipo });
    } catch (error) {
      mostrar(error, setErrorDelNombre);
      return;
    } finally {
      setEnviando(false);
    }

    // Sólo tras un alta exitosa: si falló, lo cargado se conserva para poder corregirlo.
    setNombre('');
  }

  async function guardarRenombre(id: number, nuevo: string) {
    limpiarErrores();

    try {
      await onRenombrar(id, nuevo);
    } catch (error) {
      mostrar(error, setErrorDelRenombre);
      return;
    }

    setRenombrando(null);
  }

  async function darDeBaja(id: number) {
    limpiarErrores();
    setConfirmandoLaBaja(null);

    try {
      await onDarDeBaja(id);
    } catch (error) {
      mostrar(error);
    }
  }

  return (
    <main className="l-pila">
      <div className="l-fila l-cabecera">
        <h1>Mis categorías</h1>
        <button type="button" onClick={onVolver}>
          Volver a movimientos
        </button>
      </div>

      <form className="l-pila" onSubmit={(e) => void crear(e)} noValidate>
        <CampoConError campo="nombre" etiqueta="Nombre" error={errorDelNombre}>
          {(props) => (
            <input
              {...props}
              type="text"
              value={nombre}
              onChange={(e) => setNombre(e.target.value)}
            />
          )}
        </CampoConError>

        <CampoConError campo="tipo" etiqueta="Tipo">
          {(props) => (
            <select
              {...props}
              value={tipo}
              onChange={(e) => setTipo(e.target.value as TipoMovimiento)}
            >
              <option value="gasto">Gasto</option>
              <option value="ingreso">Ingreso</option>
            </select>
          )}
        </CampoConError>

        <button type="submit" disabled={enviando}>
          {enviando ? 'Enviando…' : 'Crear categoría'}
        </button>
      </form>

      {errorGeneral ? <p role="alert">{errorGeneral}</p> : null}

      <ul className="l-pila">
        {categorias.map((categoria) => (
          // `aria-label` en el `<li>`: es lo que le da nombre accesible a la fila, y lo que
          // permite hablar de "la fila de Gimnasio" en vez de "la tercera".
          <li key={categoria.id} aria-label={categoria.nombre}>
            <span>{categoria.nombre}</span>
            <span>{categoria.tipo === 'gasto' ? 'Gasto' : 'Ingreso'}</span>

            {/* FR-008: una predefinida se ve y no se toca. */}
            {categoria.esPropia ? (
              renombrando?.id === categoria.id ? (
                <>
                  {/* Mismo componente que el alta: el mensaje queda dentro de esta fila, con su
                      `aria-invalid` y su `role="alert"`, en vez de arriba de la pantalla. El
                      `campo` lleva el id porque es el `id` del control, y sólo hay una fila en
                      modo renombre a la vez. */}
                  <CampoConError
                    campo={`renombre-${categoria.id}`}
                    etiqueta="Nombre nuevo"
                    error={errorDelRenombre}
                  >
                    {(props) => (
                      <input
                        {...props}
                        type="text"
                        value={renombrando.nombre}
                        onChange={(e) =>
                          setRenombrando({ id: categoria.id, nombre: e.target.value })
                        }
                      />
                    )}
                  </CampoConError>
                  <button
                    type="button"
                    onClick={() => void guardarRenombre(categoria.id, renombrando.nombre)}
                  >
                    Guardar
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      limpiarErrores();
                      setRenombrando(null);
                    }}
                  >
                    Cancelar
                  </button>
                </>
              ) : (
                <>
                  <button
                    type="button"
                    onClick={() => {
                      limpiarErrores();
                      setConfirmandoLaBaja(null);
                      setRenombrando({ id: categoria.id, nombre: categoria.nombre });
                    }}
                  >
                    Renombrar {categoria.nombre}
                  </button>
                  {confirmandoLaBaja === categoria.id ? (
                    <>
                      {/* Dicho entero y no "¿Seguro?": lo que hay que saber antes de apretar es
                          que no se puede deshacer, y ése es el dato que una pregunta genérica se
                          guarda. */}
                      <span role="alert">
                        Se deja de ofrecer y no se puede reactivar. Los movimientos ya registrados
                        la conservan.
                      </span>
                      <button type="button" onClick={() => void darDeBaja(categoria.id)}>
                        Confirmar la baja
                      </button>
                      <button type="button" onClick={() => setConfirmandoLaBaja(null)}>
                        No dar de baja
                      </button>
                    </>
                  ) : (
                    <button
                      type="button"
                      onClick={() => {
                        limpiarErrores();
                        setConfirmandoLaBaja(categoria.id);
                      }}
                    >
                      Dar de baja {categoria.nombre}
                    </button>
                  )}
                </>
              )
            ) : (
              // Dicho, no sólo omitido: sin esto, la ausencia de botones parece un error de carga.
              <span>Del sistema</span>
            )}
          </li>
        ))}
      </ul>
    </main>
  );
}
