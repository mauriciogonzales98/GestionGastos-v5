import { useState } from 'react';
import { ErrorDeCredenciales, ErrorDeValidacion, crearCuenta, iniciarSesion } from '../api/cliente';
import type { SesionActual } from '../api/tipos';
import { CampoConError } from '../ui/CampoConError';

export interface PropsFormularioAcceso {
  /** Se llama con la cuenta recién autenticada. Quién guarda ese estado es la raíz, no este form. */
  onEntrar: (sesion: SesionActual) => void;
}

type Modo = 'entrar' | 'crear';
type Errores = Record<string, string[]>;

/**
 * Los campos que tienen dónde mostrar su error. Cualquier otra clave de `errors` cae en la región
 * del formulario en vez de perderse, igual que en `FormularioMovimiento`.
 */
const CAMPOS_CON_LUGAR = ['email', 'contrasena'];

/**
 * El mensaje del alta, siempre el mismo (NFR-03).
 *
 * Lo pone la pantalla y no el servidor a propósito: que el texto sea idéntico exista o no la cuenta
 * es el requisito, y una constante de acá lo cumple por construcción.
 */
const ALTA_ENVIADA =
  'Si el email no estaba registrado, la cuenta fue creada. Ya podés iniciar sesión.';

/**
 * La pantalla de acceso (FR-001, FR-003), con el conmutador entre iniciar sesión y crear cuenta.
 *
 * Es un `<form>` real con `<button type="submit">`: el envío con Enter desde cualquier campo lo
 * hace el navegador solo. Es el primer formulario de la aplicación — si acá hiciera falta el
 * mouse, no se llegaría a ninguna otra pantalla.
 */
export function FormularioAcceso({ onEntrar }: PropsFormularioAcceso) {
  const [modo, setModo] = useState<Modo>('entrar');
  const [email, setEmail] = useState('');
  const [contrasena, setContrasena] = useState('');
  const [errores, setErrores] = useState<Errores>({});
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
  const [confirmacion, setConfirmacion] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  function cambiarModo(nuevo: Modo) {
    setModo(nuevo);
    // Los mensajes son del envío anterior y del otro modo: dejarlos puestos haría que un error de
    // login apareciera sobre el formulario de alta, señalando algo que ya no se está haciendo.
    setErrores({});
    setErrorGeneral(null);
    setConfirmacion(null);
    // La contraseña se limpia y el email no: el email es el mismo en los dos modos —quien acaba de
    // darse de alta quiere entrar con ése— y la contraseña, no necesariamente.
    setContrasena('');
  }

  /** Los errores del servidor, cada uno a su campo; lo que no tenga campo, a la región general. */
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
      setErrorGeneral('No se pudo completar la operación. Volvé a intentarlo.');
    }
  }

  async function enviar(evento: React.FormEvent<HTMLFormElement>) {
    evento.preventDefault();

    setErrores({});
    setErrorGeneral(null);
    setConfirmacion(null);
    setEnviando(true);

    try {
      if (modo === 'crear') {
        await crearCuenta({ email, contrasena });
        // Se pasa a "Iniciar sesión" con el email puesto: el alta no deja sesión abierta, y lo que
        // sigue siempre es entrar.
        setModo('entrar');
        setContrasena('');
        setConfirmacion(ALTA_ENVIADA);
        return;
      }

      onEntrar(await iniciarSesion({ email, contrasena }));
    } catch (error) {
      // Nada se traga. El rechazo de credenciales va a la región del formulario y NO al lado de un
      // campo: señalar "email" o "contraseña" diría cuál de los dos estaba bien (NFR-03).
      if (error instanceof ErrorDeCredenciales) {
        setErrorGeneral(error.message);
      } else if (error instanceof ErrorDeValidacion) {
        repartir(error.errores);
      } else {
        setErrorGeneral('No se pudo contactar al servidor. Revisá la conexión y volvé a intentar.');
      }
    } finally {
      setEnviando(false);
    }
  }

  const esAlta = modo === 'crear';
  const textoDelBoton = esAlta ? 'Crear mi cuenta' : 'Entrar';
  const textoEnviando = esAlta ? 'Creando…' : 'Entrando…';

  return (
    <main className="l-pila">
      <h1>Gestión de gastos</h1>

      {/* El conmutador son dos `<button>` con `aria-pressed` y no un enlace ni un radio: cambian
          lo que hace este formulario, no navegan a ningún lado ni son un dato a enviar. */}
      <div className="l-fila c-conmutador-acceso">
        <button type="button" aria-pressed={!esAlta} onClick={() => cambiarModo('entrar')}>
          Iniciar sesión
        </button>
        <button type="button" aria-pressed={esAlta} onClick={() => cambiarModo('crear')}>
          Crear cuenta
        </button>
      </div>

      <form className="l-pila c-formulario-acceso" onSubmit={(e) => void enviar(e)} noValidate>
        <CampoConError campo="email" etiqueta="Email" error={errores.email?.[0]}>
          {(props) => (
            <input
              {...props}
              type="email"
              // `username` en los dos modos: es lo que un gestor de contraseñas espera para
              // asociar la credencial a la cuenta.
              autoComplete="username"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          )}
        </CampoConError>

        <CampoConError campo="contrasena" etiqueta="Contraseña" error={errores.contrasena?.[0]}>
          {(props) => (
            <input
              {...props}
              type="password"
              // `new-password` en el alta hace que el gestor ofrezca generar una; `current-password`
              // en el login, que ofrezca la guardada. Invertirlos rompe las dos cosas.
              autoComplete={esAlta ? 'new-password' : 'current-password'}
              value={contrasena}
              onChange={(e) => setContrasena(e.target.value)}
            />
          )}
        </CampoConError>

        {/* La región de error del formulario: sólo para lo que no corresponde a ningún campo. */}
        {errorGeneral ? (
          <p role="alert" className="c-formulario-acceso__error">
            {errorGeneral}
          </p>
        ) : null}

        {/* role="status" y no "alert": es una confirmación, no un error. */}
        {confirmacion ? <p role="status">{confirmacion}</p> : null}

        <button type="submit" disabled={enviando}>
          {enviando ? textoEnviando : textoDelBoton}
        </button>
      </form>
    </main>
  );
}
