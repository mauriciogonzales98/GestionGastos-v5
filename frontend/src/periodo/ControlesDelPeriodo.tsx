import { useId, useState } from 'react';

export interface PropsControlesDelPeriodo {
  /**
   * Con qué valores arrancan los dos extremos. **Vienen del servidor, no de una cuenta hecha acá.**
   *
   * El dashboard no los pasa: allá los campos arrancan vacíos y el título del período lo pone el
   * `Resumen` que llegó. El listado sí, y por una razón concreta: `GET /api/movimientos` devuelve un
   * arreglo pelado y **no dice qué período aplicó**, así que sin esto no habría forma de mostrar el
   * mes en curso sin calcularlo en la pantalla — el segundo intérprete que D-05 prohíbe. Lo que el
   * listado pasa es el `desde`/`hasta` del `Resumen` que ya tiene cargado, que salió del servidor
   * (`FR-015`).
   */
  desdeInicial?: string;
  hastaInicial?: string;
  /**
   * Aplica el rango. Los dos extremos van tal como se escribieron, **sin validar acá**.
   *
   * Cadenas vacías significan *sin período pedido*, que es lo que el servidor entiende como el mes
   * en curso.
   */
  onAplicar: (desde: string, hasta: string) => void;
  /** El mensaje del servidor cuando rechazó el rango, o `null`. Va al lado de los campos. */
  error: string | null;
}

/**
 * Los dos extremos de un período, **para el dashboard y para el listado** (`PRD:RF-18`, `PRD:RF-21`).
 *
 * **Vive en `periodo/` desde la feature 011 y ya no en `dashboard/`** (D-05). Lo que pinta algo que
 * dos pantallas usan, sube; lo que decide qué pedir con eso, se queda en cada pantalla. Una carpeta
 * con nombre de pantalla que exporta a otra pantalla es la clase de dependencia que nadie encuentra
 * cuando busca qué rompe si toca el dashboard.
 *
 * **No valida nada, y eso es la decisión** (D-08). `PeriodoPedido` lleva escrito que es *"el único
 * intérprete de `desde` y `hasta`"* y que con dos intérpretes la igualdad entre las vistas *"depende
 * de que nadie toque uno sin tocar el otro"*. Comprobar acá que la fecha de inicio no sea posterior
 * a la de fin sería el segundo intérprete, con sus propias palabras y su propio criterio de "hoy".
 *
 * Así que el rango se manda como está y el mensaje que se muestra es el que vuelve, bajo la clave
 * `rango` — una clave que existe, según su propio comentario, *"porque el frontend la usa para poner
 * el mensaje al lado del control"*. Se escribió para este momento.
 */
export function ControlesDelPeriodo({
  onAplicar,
  error,
  desdeInicial = '',
  hastaInicial = '',
}: PropsControlesDelPeriodo) {
  const idDesde = useId();
  const idHasta = useId();
  const idError = useId();

  // Lo tecleado, que no es lo aplicado: escribir una fecha no tiene que disparar una petición por
  // cada dígito, y el rango sólo tiene sentido cuando los dos extremos están puestos.
  const [desde, setDesde] = useState(desdeInicial);
  const [hasta, setHasta] = useState(hastaInicial);

  return (
    <form
      className="l-fila"
      onSubmit={(evento) => {
        evento.preventDefault();
        onAplicar(desde, hasta);
      }}
    >
      <label htmlFor={idDesde}>Desde</label>
      <input
        id={idDesde}
        type="date"
        value={desde}
        aria-describedby={error ? idError : undefined}
        onChange={(evento) => setDesde(evento.target.value)}
      />

      <label htmlFor={idHasta}>Hasta</label>
      <input
        id={idHasta}
        type="date"
        value={hasta}
        aria-describedby={error ? idError : undefined}
        onChange={(evento) => setHasta(evento.target.value)}
      />

      <button type="submit">Aplicar</button>

      {/* El mensaje del servidor, al lado de los campos que lo produjeron y no en una franja
          general: es lo que permite saber QUÉ hay que corregir sin adivinar. */}
      {error ? (
        <p id={idError} role="alert" className="c-campo__error">
          {error}
        </p>
      ) : null}
    </form>
  );
}
