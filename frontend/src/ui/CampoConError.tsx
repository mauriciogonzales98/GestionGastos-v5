import type { ReactNode } from 'react';

/**
 * Lo que el componente le pasa al control que envuelve. Se esparce con `{...props}` para que el
 * control no tenga que saber cómo se arma el vínculo con su error.
 */
export interface PropsDelControl {
  id: string;
  'aria-invalid'?: 'true';
  'aria-describedby'?: string;
}

export interface PropsCampoConError {
  /** Nombre del campo. Es el `id` del control y la raíz del `id` del error: `{campo}-error`. */
  campo: string;
  etiqueta: string;
  /** Mensaje a mostrar. Ausente = el campo no tiene error. */
  error?: string;
  children: (props: PropsDelControl) => ReactNode;
}

/**
 * El componente único de campo: `label` + control + contenedor de error, con `aria-describedby` y
 * `aria-invalid` puestos por él y no por cada pantalla.
 *
 * Existe para que la tripleta se arme en UN solo lugar. Si cada formulario la armara a mano, el
 * ticket 6 tendría que cambiar la presentación en tantos lugares como formularios haya, y alguno
 * queda atrás — que es exactamente la cicatriz que el plan denuncia.
 *
 * La clave de `errors` del ProblemDetails que devuelve el servidor es el nombre del campo, así que
 * un error del servidor y uno del cliente entran por la misma prop y se muestran en el mismo lugar.
 */
export function CampoConError({ campo, etiqueta, error, children }: PropsCampoConError) {
  const idDelError = `${campo}-error`;

  // Sin error, ninguno de los dos atributos se pone: `aria-invalid="false"` no es lo mismo que
  // ausente para todos los lectores de pantalla, y describir un error que no existe es ruido.
  const propsDelControl: PropsDelControl = error
    ? { id: campo, 'aria-invalid': 'true', 'aria-describedby': idDelError }
    : { id: campo };

  return (
    <div className="l-pila c-campo">
      <label htmlFor={campo}>{etiqueta}</label>
      {children(propsDelControl)}
      {/* Inmediatamente después del control, con role="alert": se anuncia al aparecer sin robar
          el foco a quien está escribiendo. */}
      {error ? (
        <p id={idDelError} role="alert" className="c-campo__error">
          {error}
        </p>
      ) : null}
    </div>
  );
}
