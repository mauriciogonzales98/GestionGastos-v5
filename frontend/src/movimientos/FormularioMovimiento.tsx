import { useId, useMemo, useRef, useState } from 'react';
import { ErrorDeValidacion } from '../api/cliente';
import type { Categoria, Moneda, NuevoMovimiento, TipoMovimiento } from '../api/tipos';
import { CampoConError } from '../ui/CampoConError';

export interface PropsFormularioMovimiento {
  categorias: Categoria[];
  /**
   * El catálogo de monedas que alimenta el selector (FR-005).
   *
   * **Baja por props y no se pide acá**, igual que el de categorías y por la misma razón: vive en
   * la raíz para que esta pantalla y el acotado del listado miren la misma lista, y para que se
   * pida una sola vez (`PRD:NFR-02`, AC-12).
   */
  monedas: Moneda[];
  /** El día de hoy en formato `YYYY-MM-DD`. Entra por prop para que el test sea determinista. */
  hoy: string;
  onGuardar: (movimiento: NuevoMovimiento) => void | Promise<void>;
}

type Errores = Record<string, string[]>;

/**
 * Los campos que tienen un lugar donde mostrar su error. Cualquier clave de `errors` fuera de esta
 * lista no tiene dónde ir, así que cae en la región general en vez de perderse.
 */
const CAMPOS_CON_LUGAR = ['tipo', 'monto', 'categoriaId', 'monedaId', 'fecha'];

/** El techo de FR-004b. Igual que en el servidor: la validación de cliente no lo relaja. */
const MONTO_MAXIMO = 999_999_999.99;

/**
 * Las mismas reglas que aplica el servidor, adelantadas para no gastar un viaje. El servidor
 * vuelve a validar igual: esto es comodidad, no la barrera.
 */
function validar(monto: string, categoriaId: string): Errores {
  const errores: Errores = {};
  const valor = Number(monto);

  if (monto.trim() === '' || Number.isNaN(valor)) {
    errores.monto = ['Ingresá un monto.'];
  } else if (valor <= 0 || Math.round(valor * 100) !== Number((valor * 100).toFixed(4))) {
    errores.monto = ['El monto debe ser mayor a cero y tener hasta dos decimales.'];
  } else if (valor > MONTO_MAXIMO) {
    errores.monto = ['El monto no puede superar 999.999.999,99.'];
  }

  if (categoriaId === '') {
    errores.categoriaId = ['Elegí una categoría.'];
  }

  return errores;
}

/**
 * El formulario de alta (FR-001, FR-002, FR-003, FR-005).
 *
 * Es un `<form>` real con `<button type="submit">`: el envío con Enter desde cualquier campo lo
 * hace el navegador solo, y reimplementarlo con handlers de tecla sería romper algo que ya
 * funciona. Es también la mitad de lo que AC-55 verifica.
 */
export function FormularioMovimiento({
  categorias,
  monedas,
  hoy,
  onGuardar,
}: PropsFormularioMovimiento) {
  const [tipo, setTipo] = useState<TipoMovimiento>('gasto');
  const [monto, setMonto] = useState('');
  const [categoriaId, setCategoriaId] = useState('');
  const [monedaId, setMonedaId] = useState('');
  const [fecha, setFecha] = useState(hoy);
  const [errores, setErrores] = useState<Errores>({});
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const grupoTipo = useId();
  const primerCampo = useRef<HTMLInputElement>(null);

  const delTipoElegido = useMemo(
    () => categorias.filter((c) => c.tipo === tipo),
    [categorias, tipo],
  );

  /**
   * La selección efectiva: la elegida, o ninguna si dejó de estar en el catálogo (FR-022).
   *
   * Pasa de verdad: la categoría se da de baja desde la pantalla de gestión mientras el movimiento
   * está a medio cargar, y el catálogo baja sin ella. Sin esto, el `<select>` se queda con un valor
   * que ya no tiene `<option>` —el control se ve vacío— pero el estado sigue guardando el id viejo,
   * así que "Registrar" manda una categoría que el servidor rechaza con un mensaje que la persona
   * no puede entender: eligió algo y le dicen que no eligió nada.
   *
   * Se deriva en vez de corregir el estado con un `useEffect`: el efecto haría un render de más y
   * abriría una ventana en la que el valor mostrado y el enviado difieren, que es justamente el
   * problema que se está arreglando.
   */
  const seleccionVigente = delTipoElegido.some((c) => String(c.id) === categoriaId)
    ? categoriaId
    : '';

  /**
   * La moneda efectiva: la elegida, o la predeterminada del catálogo si no se eligió ninguna
   * (FR-006, AC-02).
   *
   * **El estado arranca vacío y no con la predeterminada puesta**, y eso es deliberado: el catálogo
   * baja por props y puede llegar después del primer render, así que sembrar el estado con él
   * exigiría un `useEffect` que lo corrija — un render de más y una ventana en la que el valor
   * mostrado y el que se enviaría difieren. Derivarlo no tiene ninguna de las dos cosas. Es el
   * mismo criterio con el que se resuelve la categoría que dejó de estar en el catálogo.
   *
   * Si el catálogo todavía no llegó, esto queda vacío y el alta sale **sin** `monedaId`, que el
   * servidor interpreta como la predeterminada: exactamente lo mismo, decidido del otro lado.
   */
  const monedaVigente = monedas.some((m) => String(m.id) === monedaId)
    ? monedaId
    : (monedas.find((m) => m.esPredeterminada)?.id.toString() ?? '');

  function cambiarTipo(nuevo: TipoMovimiento) {
    setTipo(nuevo);
    // Se limpia la selección: dejarla puesta permitiría enviar un gasto con categoría de ingreso,
    // que el servidor rechaza por FR-011. Que la combinación imposible no sea alcanzable es mejor
    // que rechazarla después.
    setCategoriaId('');
  }

  /**
   * Reparte los errores que devolvió el servidor: los de un campo conocido van al lado de su
   * control, y todo lo demás a la región del formulario.
   *
   * Sin esto, un `errors` vacío o con una clave que ningún campo conoce —`peticion`, `$`, lo que
   * el binder de .NET decida— desaparecía: se guardaba en el estado y nadie lo pintaba. La persona
   * hacía clic en Registrar y no pasaba nada. Un rechazo invisible es peor que un mensaje feo.
   */
  function repartir(delServidor: Errores) {
    const propios: Errores = {};
    const sueltos: string[] = [];

    for (const [clave, mensajes] of Object.entries(delServidor)) {
      if (CAMPOS_CON_LUGAR.includes(clave)) {
        propios[clave] = mensajes;
      } else {
        sueltos.push(...mensajes);
      }
    }

    setErrores(propios);

    if (sueltos.length > 0) {
      setErrorGeneral(sueltos.join(' '));
    } else if (Object.keys(propios).length === 0) {
      // Rechazado, pero sin decir por qué ni dónde. Al menos que se vea que fue rechazado.
      setErrorGeneral('No se pudo registrar el movimiento. Volvé a intentarlo.');
    }
  }

  async function enviar(evento: React.FormEvent<HTMLFormElement>) {
    evento.preventDefault();

    const delCliente = validar(monto, seleccionVigente);
    if (Object.keys(delCliente).length > 0) {
      setErrores(delCliente);
      setErrorGeneral(null);
      return;
    }

    setErrores({});
    setErrorGeneral(null);
    setEnviando(true);

    try {
      await onGuardar({
        tipo,
        monto: Number(monto),
        categoriaId: Number(seleccionVigente),
        // Sin catálogo todavía no se manda nada: el servidor pone la predeterminada, que es el
        // mismo resultado (FR-002).
        monedaId: monedaVigente === '' ? null : Number(monedaVigente),
        fecha,
      });
    } catch (error) {
      // Nada se traga: un error de validación se enruta a sus campos y cualquier otro se muestra
      // en la región del formulario. En los dos casos el formulario conserva lo cargado.
      if (error instanceof ErrorDeValidacion) {
        repartir(error.errores);
      } else {
        setErrorGeneral('No se pudo registrar el movimiento. Volvé a intentarlo.');
      }
      return;
    } finally {
      setEnviando(false);
    }

    // Sólo tras un guardado exitoso.
    setTipo('gasto');
    setMonto('');
    setCategoriaId('');
    // Vuelve a vacío, o sea a la predeterminada derivada. Encadenar cargas en la misma moneda es
    // más común que cambiarla, pero recordarla sería un historial, y `RF-25` dice que el valor por
    // defecto es el del catálogo.
    setMonedaId('');
    setFecha(hoy);

    // El foco vuelve por código al primer campo (FR-014). Es lo que permite encadenar cargas sin
    // tocar el mouse, y la otra mitad de lo que AC-55 verifica.
    primerCampo.current?.focus();
  }

  return (
    <form className="l-pila c-formulario-movimiento" onSubmit={(e) => void enviar(e)} noValidate>
      <fieldset className="l-fila">
        <legend>Tipo</legend>
        {(['gasto', 'ingreso'] as const).map((valor) => (
          <span key={valor}>
            <input
              type="radio"
              id={`${grupoTipo}-${valor}`}
              name={`${grupoTipo}-tipo`}
              value={valor}
              checked={tipo === valor}
              onChange={() => cambiarTipo(valor)}
              aria-invalid={errores.tipo ? 'true' : undefined}
              aria-describedby={errores.tipo ? 'tipo-error' : undefined}
              ref={valor === 'gasto' ? primerCampo : undefined}
            />
            <label htmlFor={`${grupoTipo}-${valor}`}>
              {valor === 'gasto' ? 'Gasto' : 'Ingreso'}
            </label>
          </span>
        ))}

        {/* El backend puede rechazar por `tipo` (ValidacionDelAlta lo produce). Sin este lugar,
            ese mensaje llegaba al navegador y no se mostraba en ninguna parte. */}
        {errores.tipo?.[0] ? (
          <p id="tipo-error" role="alert" className="c-campo__error">
            {errores.tipo[0]}
          </p>
        ) : null}
      </fieldset>

      <CampoConError campo="monto" etiqueta="Monto" error={errores.monto?.[0]}>
        {(props) => (
          <input
            {...props}
            type="number"
            step="0.01"
            value={monto}
            onChange={(e) => setMonto(e.target.value)}
          />
        )}
      </CampoConError>

      <CampoConError campo="categoriaId" etiqueta="Categoría" error={errores.categoriaId?.[0]}>
        {(props) => (
          // `<select>` nativo: un combo propio tendría que reimplementar teclado, foco y anuncio,
          // y AGENTS.md prohíbe la dependencia que lo evitaría.
          <select
            {...props}
            value={seleccionVigente}
            onChange={(e) => setCategoriaId(e.target.value)}
          >
            <option value="">Elegí una categoría</option>
            {delTipoElegido.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nombre}
              </option>
            ))}
          </select>
        )}
      </CampoConError>

      <CampoConError campo="monedaId" etiqueta="Moneda" error={errores.monedaId?.[0]}>
        {(props) => (
          // Sin opción vacía, a diferencia de categoría: la moneda SIEMPRE tiene un valor. Un
          // "elegí una moneda" obligaría a tocar el control, y `PRD:NFR-01` exige poder guardar sin
          // tocarlo — cero interacciones adicionales para quien usa una sola moneda.
          <select {...props} value={monedaVigente} onChange={(e) => setMonedaId(e.target.value)}>
            {monedas.map((m) => (
              <option key={m.id} value={m.id}>
                {m.nombre}
              </option>
            ))}
          </select>
        )}
      </CampoConError>

      <CampoConError campo="fecha" etiqueta="Fecha" error={errores.fecha?.[0]}>
        {(props) => (
          <input {...props} type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} />
        )}
      </CampoConError>

      {/* La región de error del formulario: sólo para lo que no corresponde a ningún campo. */}
      {errorGeneral ? (
        <p role="alert" className="c-formulario-movimiento__error">
          {errorGeneral}
        </p>
      ) : null}

      <button type="submit" disabled={enviando}>
        {enviando ? 'Enviando…' : 'Registrar'}
      </button>
    </form>
  );
}
