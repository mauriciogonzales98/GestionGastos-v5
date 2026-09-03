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
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  /** Qué fila está en modo renombre, y con qué texto. `null` = ninguna. */
  const [renombrando, setRenombrando] = useState<{ id: number; nombre: string } | null>(null);

  /**
   * Reparte un rechazo del servidor: el mensaje de `nombre` va al lado de su campo y todo lo demás
   * a la región general. Un error sin lugar donde ir es un rechazo invisible.
   */
  function mostrar(error: unknown) {
    if (error instanceof ErrorDeValidacion) {
      const delNombre = error.errores.nombre?.[0];
      setErrorDelNombre(delNombre);

      const sueltos = Object.entries(error.errores)
        .filter(([clave]) => clave !== 'nombre')
        .flatMap(([, mensajes]) => mensajes);

      setErrorGeneral(sueltos.length > 0 ? sueltos.join(' ') : delNombre ? null : ERROR_GENERICO);
      return;
    }

    setErrorGeneral(ERROR_GENERICO);
  }

  function limpiarErrores() {
    setErrorDelNombre(undefined);
    setErrorGeneral(null);
  }

  async function crear(evento: React.FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    limpiarErrores();
    setEnviando(true);

    try {
      await onCrear({ nombre, tipo });
    } catch (error) {
      mostrar(error);
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
      mostrar(error);
      return;
    }

    setRenombrando(null);
  }

  async function darDeBaja(id: number) {
    limpiarErrores();

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
                  <label htmlFor={`renombre-${categoria.id}`}>Nombre nuevo</label>
                  <input
                    id={`renombre-${categoria.id}`}
                    type="text"
                    value={renombrando.nombre}
                    onChange={(e) => setRenombrando({ id: categoria.id, nombre: e.target.value })}
                  />
                  <button
                    type="button"
                    onClick={() => void guardarRenombre(categoria.id, renombrando.nombre)}
                  >
                    Guardar
                  </button>
                  <button type="button" onClick={() => setRenombrando(null)}>
                    Cancelar
                  </button>
                </>
              ) : (
                <>
                  <button
                    type="button"
                    onClick={() => setRenombrando({ id: categoria.id, nombre: categoria.nombre })}
                  >
                    Renombrar {categoria.nombre}
                  </button>
                  <button type="button" onClick={() => void darDeBaja(categoria.id)}>
                    Dar de baja {categoria.nombre}
                  </button>
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
