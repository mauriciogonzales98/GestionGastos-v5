import { useCallback, useEffect, useState } from 'react';
import { FormularioAcceso } from './acceso/FormularioAcceso';
import { ErrorDeSesion, cerrarSesion, consultarSesion } from './api/cliente';
import type { SesionActual } from './api/tipos';
import { PantallaMovimientos } from './movimientos/PantallaMovimientos';

export interface PropsApp {
  /** El día de hoy en `YYYY-MM-DD`. Entra por prop para que los tests sean deterministas. */
  hoy: string;
}

/**
 * Averiguando · sin sesión · con sesión. El estado arranca en `averiguando` y no en `sin-sesion`
 * porque todavía no se sabe: afirmar que no hay sesión antes de preguntar haría parpadear el login
 * en cada recarga de quien sí la tiene.
 */
type Estado = 'averiguando' | 'sin-sesion' | 'con-sesion';

/**
 * La raíz: decide qué pantalla se muestra según haya sesión o no (D-08).
 *
 * No hay router. No son dos rutas que enrutar sino un estado con dos valores: nadie navega a
 * `/login`, se llega ahí por no tener sesión. Quien gobierna es la respuesta del servidor, no la
 * URL — y así no existe la URL que alguien pueda escribir a mano para ver la pantalla protegida
 * antes de que el servidor diga que no.
 */
export function App({ hoy }: PropsApp) {
  const [estado, setEstado] = useState<Estado>('averiguando');
  const [sesion, setSesion] = useState<SesionActual | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  useEffect(() => {
    void consultarSesion()
      .then((actual) => {
        setSesion(actual);
        setEstado('con-sesion');
      })
      .catch((error: unknown) => {
        setEstado('sin-sesion');

        // Sin sesión es el caso normal al arrancar y no lleva aviso: nadie necesita que le digan
        // que no inició sesión todavía. Cualquier OTRO error sí, porque termina en la misma
        // pantalla por un motivo completamente distinto —el servidor no contestó— y confundirlos
        // haría que alguien probara su contraseña una y otra vez contra nada.
        if (!(error instanceof ErrorDeSesion)) {
          setAviso('No se pudo contactar al servidor. Revisá la conexión y volvé a intentar.');
        }
      });
  }, []);

  /**
   * La reacción única a cualquier `401`, venga de donde venga (D-09).
   *
   * `useCallback` no es una optimización: `PantallaMovimientos` la usa dentro de un `useEffect`, y
   * una función nueva en cada render volvería a disparar la carga inicial en bucle.
   */
  const alVencerLaSesion = useCallback((motivo: string) => {
    setSesion(null);
    setEstado('sin-sesion');
    setAviso(motivo);
  }, []);

  async function salir() {
    try {
      await cerrarSesion();
    } catch {
      // No es un catch silencioso: la salida ocurre igual, y eso es la decisión. Quien apretó
      // "Cerrar sesión" quiso irse; dejarlo dentro porque el servidor no contestó es lo contrario
      // de lo que pidió, y en una máquina compartida es un problema de verdad. La cookie sigue
      // viva del otro lado hasta que expire, y eso ya está acotado a 24 h.
    }

    setSesion(null);
    setEstado('sin-sesion');
    // Sin aviso: no venció, se cerró a propósito. Decir "tu sesión venció" acá sería mentirle a
    // quien acaba de apretar el botón.
    setAviso(null);
  }

  if (estado === 'averiguando') {
    return (
      <main className="l-pila">
        <p>Cargando…</p>
      </main>
    );
  }

  if (estado === 'con-sesion' && sesion) {
    return (
      <PantallaMovimientos
        hoy={hoy}
        email={sesion.email}
        onCerrarSesion={() => void salir()}
        onSesionVencida={alVencerLaSesion}
      />
    );
  }

  return (
    <>
      {/* Antes del formulario y con role="alert": es lo que explica por qué se está viendo esta
          pantalla en lugar de la otra. */}
      {aviso ? <p role="alert">{aviso}</p> : null}
      <FormularioAcceso
        onEntrar={(actual) => {
          setSesion(actual);
          setEstado('con-sesion');
          setAviso(null);
        }}
      />
    </>
  );
}
