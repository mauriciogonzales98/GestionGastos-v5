import { useId, useMemo, useRef, useState } from 'react';
import { ErrorDeValidacion } from '../api/cliente';
import type { Categoria, NuevoMovimiento, TipoMovimiento } from '../api/tipos';
import { CampoConError } from '../ui/CampoConError';

export interface PropsFormularioMovimiento {
  categorias: Categoria[];
  /** El día de hoy en formato `YYYY-MM-DD`. Entra por prop para que el test sea determinista. */
  hoy: string;
  onGuardar: (movimiento: NuevoMovimiento) => void | Promise<void>;
}

type Errores = Record<string, string[]>;

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
export function FormularioMovimiento({ categorias, hoy, onGuardar }: PropsFormularioMovimiento) {
  const [tipo, setTipo] = useState<TipoMovimiento>('gasto');
  const [monto, setMonto] = useState('');
  const [categoriaId, setCategoriaId] = useState('');
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

  function cambiarTipo(nuevo: TipoMovimiento) {
    setTipo(nuevo);
    // Se limpia la selección: dejarla puesta permitiría enviar un gasto con categoría de ingreso,
    // que el servidor rechaza por FR-011. Que la combinación imposible no sea alcanzable es mejor
    // que rechazarla después.
    setCategoriaId('');
  }

  async function enviar(evento: React.FormEvent<HTMLFormElement>) {
    evento.preventDefault();

    const delCliente = validar(monto, categoriaId);
    if (Object.keys(delCliente).length > 0) {
      setErrores(delCliente);
      setErrorGeneral(null);
      return;
    }

    setErrores({});
    setErrorGeneral(null);
    setEnviando(true);

    try {
      await onGuardar({ tipo, monto: Number(monto), categoriaId: Number(categoriaId), fecha });
    } catch (error) {
      // Nada se traga: un error de validación se enruta a sus campos y cualquier otro se muestra
      // en la región del formulario. En los dos casos el formulario conserva lo cargado.
      if (error instanceof ErrorDeValidacion) {
        setErrores(error.errores);
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
              ref={valor === 'gasto' ? primerCampo : undefined}
            />
            <label htmlFor={`${grupoTipo}-${valor}`}>
              {valor === 'gasto' ? 'Gasto' : 'Ingreso'}
            </label>
          </span>
        ))}
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
          <select {...props} value={categoriaId} onChange={(e) => setCategoriaId(e.target.value)}>
            <option value="">Elegí una categoría</option>
            {delTipoElegido.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nombre}
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
