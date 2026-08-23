import { useId, useMemo, useRef, useState } from 'react';
import type { Categoria, NuevoMovimiento, TipoMovimiento } from '../api/tipos';
import { CampoConError } from '../ui/CampoConError';

export interface PropsFormularioMovimiento {
  categorias: Categoria[];
  /** El día de hoy en formato `YYYY-MM-DD`. Entra por prop para que el test sea determinista. */
  hoy: string;
  onGuardar: (movimiento: NuevoMovimiento) => void | Promise<void>;
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

    await onGuardar({
      tipo,
      monto: Number(monto),
      categoriaId: Number(categoriaId),
      fecha,
    });

    // Sólo tras un guardado exitoso. Si onGuardar rechaza, el await propaga y no se llega acá: el
    // formulario conserva lo cargado, que es lo que la spec exige ante un error.
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

      <CampoConError campo="monto" etiqueta="Monto">
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

      <CampoConError campo="categoriaId" etiqueta="Categoría">
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

      <CampoConError campo="fecha" etiqueta="Fecha">
        {(props) => (
          <input {...props} type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} />
        )}
      </CampoConError>

      <button type="submit">Registrar</button>
    </form>
  );
}
