/**
 * La fuente de verdad del contrato HTTP entre esta pantalla y la API.
 *
 * Estos tipos están escritos a mano y NO se derivan del backend, así que nada los mantiene
 * alineados por construcción: un rename coherente del backend deja en verde el build, `tsc`,
 * ESLint y toda la suite, y hace llegar `undefined` a la pantalla. `tsc` verifica que el frontend
 * sea coherente consigo mismo, no que coincida con el backend.
 *
 * Lo que cierra esa brecha son los tests de `backend/GestionGastos.Api.Tests/Contrato/`, que LEEN
 * este archivo y lo comparan contra el JSON que la API emite de verdad, en las dos direcciones
 * (research.md D-09). Es la única excepción de estructura del proyecto, declarada en `AGENTS.md`:
 * lectura en una sola dirección, el frontend no lee nada del backend.
 *
 * Si cambiás algo acá, el contrato cambió. Esa es la idea.
 */

/**
 * Las dos mitades del dominio. Viaja como cadena y no como número: el `tinyint` de la base
 * obligaría a esta capa a conocer el mapeo del esquema.
 */
export type TipoMovimiento = 'gasto' | 'ingreso';

/** Una categoría del catálogo que alimenta el selector del formulario (FR-006). */
export interface Categoria {
  id: number;
  nombre: string;
  tipo: TipoMovimiento;
  /**
   * `true` = la creó esta cuenta y puede renombrarla o darla de baja; `false` = predefinida del
   * sistema, de solo lectura (FR-008).
   *
   * Es lo único que la pantalla de gestión necesita para saber qué ofrecer sobre cada fila, y por
   * eso viaja en vez de `usuarioId` (D-07): un número de cuenta no le sirve a nadie de este lado y
   * obligaría al cliente a saber cuál es la suya para poder compararlo. `activa` tampoco viaja —
   * el listado ya devuelve sólo activas.
   */
  esPropia: boolean;
}

/**
 * Lo que se manda al crear una categoría propia (FR-004).
 *
 * `tipo` se fija al crear y no vuelve a viajar nunca: `CategoriaEditada` no lo lleva, porque
 * cambiarlo movería de tipo a todos los movimientos que ya la usan.
 */
export interface NuevaCategoria {
  nombre: string;
  tipo: TipoMovimiento;
}

/**
 * Lo que se manda al renombrar una categoría propia (FR-007).
 *
 * Tiene un solo campo y aun así es un tipo aparte de `NuevaCategoria`, por el mismo motivo que
 * `NuevaCuenta` y `Credenciales`: son dos contratos que pueden divergir. Éste ya diverge, y en lo
 * más importante — no lleva `tipo`.
 */
export interface CategoriaEditada {
  nombre: string;
}

/**
 * Una moneda del catálogo, tal como la devuelve `GET /api/monedas` (FR-004).
 *
 * **El catálogo es del sistema y se administra como dato**: nadie lo crea, edita ni borra desde la
 * aplicación. Agregarle una fila tiene que alcanzar para que aparezca en el selector del formulario
 * y en el acotado del listado, sin tocar una línea de código — es `RF-32`, y lo que lo sostiene es
 * que esta lista salga siempre de acá y nunca de una constante escrita en el frontend.
 *
 * `esPredeterminada` viaja porque responde la única pregunta que el formulario se hace sobre el
 * catálogo —cuál propongo—, y viaja como la respuesta ya calculada y no como el dato con el que
 * calcularla. Es el mismo criterio que `esPropia` en `Categoria`.
 *
 * `decimales` NO viaja, aunque la columna existe: hoy no lo consume nadie. El formato regional del
 * monto es el ticket 6, y un campo que nadie usa es un dato que salió a la red sin que nadie lo
 * decidiera.
 */
export interface Moneda {
  id: number;
  /** ISO 4217: `ARS`, `USD`. */
  codigo: string;
  nombre: string;
  simbolo: string;
  /** Exactamente una del catálogo la tiene en `true` (RF-25). */
  esPredeterminada: boolean;
}

/** Un movimiento tal como lo devuelven el alta y el listado: la misma forma en los dos. */
export interface Movimiento {
  id: number;
  tipo: TipoMovimiento;
  monto: number;
  categoriaId: number;
  /** Viaja junto al id para que el listado no tenga que cruzar contra el catálogo. */
  categoriaNombre: string;
  monedaCodigo: string;
  /** `YYYY-MM-DD`. Sin hora ni zona horaria. */
  fecha: string;
}

/**
 * Lo que se manda al registrar. `fecha` es opcional: ausente o null significa hoy, y ese valor lo
 * pone el servidor (AC-17).
 *
 * `monedaId` también es opcional, y **ausente o null significa la moneda predeterminada del
 * catálogo** (FR-002). No es una comodidad: es `PRD:NFR-01` —quien opera en una sola moneda no
 * agrega ni un paso respecto de antes— y es la compatibilidad hacia atrás del contrato, porque
 * hasta la feature 009 este campo no existía y todo cliente que ya andaba sigue andando sin
 * mandarlo.
 */
export interface NuevoMovimiento {
  tipo: TipoMovimiento;
  monto: number;
  categoriaId: number;
  /** Del catálogo de `Moneda`. Ausente o null = la predeterminada. */
  monedaId?: number | null;
  fecha?: string | null;
}

/**
 * Lo que se manda al modificar un movimiento ya registrado (RF-14).
 *
 * Tiene los mismos campos que `NuevoMovimiento` y aun así es un tipo aparte, por el mismo motivo
 * que `NuevaCuenta` y `Credenciales`: son dos contratos que pueden divergir. Éste ya diverge en
 * `fecha`.
 *
 * `fecha` es OBLIGATORIA acá y opcional al registrar. Ausente significa "hoy" en el alta, que es lo
 * correcto; en una edición sería una trampa —el movimiento saltaría a hoy en silencio—, así que se
 * exige.
 *
 * No lleva moneda: no se elige al registrar y tampoco al editar. Tampoco lleva propietario: lo
 * decide la sesión, nunca el cuerpo.
 */
export interface MovimientoEditado {
  tipo: TipoMovimiento;
  monto: number;
  categoriaId: number;
  /** `YYYY-MM-DD`. Sin hora ni zona horaria. */
  fecha: string;
}

/**
 * El resumen de un período (RF-19, RF-20, RF-22).
 *
 * `desde` y `hasta` viajan SIEMPRE, también cuando no se pidieron. Sin ellos, esta capa tendría que
 * calcular el mes en curso por su cuenta —en la zona horaria del navegador— para poder titularlo, y
 * ahí volverían a existir dos criterios de "hoy". El mes por omisión lo decide el servidor, y esto
 * es cómo se entera el cliente de cuál eligió.
 */
export interface Resumen {
  /** `YYYY-MM-DD`. Primer día del período, incluido. */
  desde: string;
  /** `YYYY-MM-DD`. Último día del período, incluido. */
  hasta: string;
  /** Una entrada por cada moneda del catálogo, tenga o no movimientos. Nunca viene vacío. */
  monedas: ResumenPorMoneda[];
}

/**
 * Lo que pasó en una moneda durante el período.
 *
 * Dos de éstos son dos universos separados: **nada se suma nunca a través de ellos** y no hay
 * conversión en ningún lado (RF-29). Si alguna vez hace falta un total único, va a ser una decisión
 * de producto con su tasa de cambio, no una suma que esta capa pueda hacer sola.
 */
export interface ResumenPorMoneda {
  monedaId: number;
  /** ISO 4217. Viaja junto al id para no cruzar contra un catálogo. */
  monedaCodigo: string;
  totalIngresado: number;
  totalGastado: number;
  /** Ingresos menos gastos. **Puede ser negativo**, y eso se muestra: un mes en rojo es un dato. */
  balance: number;
  /** Sólo las categorías con al menos un gasto en el período. `[]` es normal, no un error. */
  gastosPorCategoria: TotalPorCategoria[];
}

/** Cuánto suma una categoría dentro de una moneda y un período. */
export interface TotalPorCategoria {
  categoriaId: number;
  /** El nombre vigente de la categoría, no una copia del momento del alta. */
  categoriaNombre: string;
  total: number;
}

/**
 * El formato único de error de las dos capas (RFC 9457). La clave de `errors` es el nombre del
 * campo de la petición, y eso es lo que permite poner cada mensaje al lado de su control en vez de
 * volcar un texto suelto.
 */
export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  errors?: Record<string, string[]>;
}

/**
 * Lo que se manda al crear una cuenta (FR-001).
 *
 * La contraseña viaja en claro dentro del cuerpo —protegida por el transporte, no por el
 * contrato— y no se guarda en ningún lado del cliente: se manda y se olvida.
 */
export interface NuevaCuenta {
  email: string;
  contrasena: string;
}

/**
 * Lo que se manda al iniciar sesión (FR-003).
 *
 * Tiene la misma forma que `NuevaCuenta` y aun así es un tipo aparte: son dos endpoints distintos
 * que pueden divergir —el alta puede sumar un campo que el login no tiene— y unificarlos ataría
 * los dos contratos a que eso nunca pase.
 */
export interface Credenciales {
  email: string;
  contrasena: string;
}

/**
 * La cuenta en sesión, tal como la devuelven el inicio de sesión y la consulta: la misma forma en
 * los dos. Es lo que la pantalla pide al arrancar para saber si tiene que mostrar el acceso o los
 * movimientos.
 *
 * No lleva identificador: el cliente no necesita saber cuál es la cuenta, sino que hay una. Quién
 * es el propietario de cada movimiento lo resuelve el servidor con la cookie, nunca con un dato
 * que el cliente mande de vuelta.
 */
export interface SesionActual {
  email: string;
}
