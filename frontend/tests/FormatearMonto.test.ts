import { describe, expect, it } from 'vitest';
import { decimalesDe, formatearMonto } from '../src/ui/formatearMonto';
import { LA_DE_TRES, MONEDAS, SIN_CENTAVOS } from './monedas.fixture';

/**
 * FR-019, D-08 — el monto se muestra en la escala que declara **el catálogo**.
 *
 * Es la deuda anotada tres veces: **D8-05**, **D9-05** y **D10-02**. La columna `moneda.decimales`
 * existe desde FEAT-001a y hasta esta feature no salía a la red porque no la consumía nadie.
 */

describe('FR-019 · la escala sale del catálogo', () => {
  it('una moneda de dos decimales muestra dos', () => {
    expect(formatearMonto(1250.5, 'ARS', 2)).toMatch(/1\.250,50/);
  });

  it('una moneda sin centavos no muestra ninguno (FR-019)', () => {
    const formateado = formatearMonto(1250, 'JPY', 0);

    expect(formateado).toMatch(/1\.250/);
    expect(formateado).not.toMatch(/1\.250,/);
  });

  /**
   * **El caso que separa las dos fuentes posibles de la escala.**
   *
   * Con el yen no alcanza: `Intl` también le asigna 0 decimales por su código ISO, así que un
   * formateo correcto no dice de dónde salió el número. `XCT` sí lo dice — no es un código que
   * `Intl` conozca, y el catálogo le pone **tres** decimales, que ninguna deducción habría dado.
   *
   * Si esto mostrara dos decimales, la escala no vino del catálogo, y `PRD:RF-32` estaría roto del
   * único lado que el usuario mira.
   */
  it('el catálogo le gana a lo que Intl deduzca del código ISO (FR-019, D-08)', () => {
    expect(formatearMonto(1250, 'XCT', 3)).toMatch(/1\.250,000/);
  });

  it('sin el dato del catálogo se cae en lo que Intl deduzca, sin inventar un valor', () => {
    // La degradación correcta mientras el catálogo no llegó: mostrar el monto con la escala
    // deducida, no dejar de mostrarlo ni cablear un 2 acá.
    expect(formatearMonto(1250.5, 'ARS')).toMatch(/1\.250,50/);
  });
});

/**
 * El `try/catch` **no se toca en esta feature** y sigue haciendo falta.
 *
 * Está ahí porque `moneda.codigo` es `char(3)` y admite `'BT1'`, que hace lanzar a `Intl`; ese
 * guardarraíl es independiente de la escala y sigue esperando el `CHECK` que le falta a la columna
 * (deuda **D11-02**). Lo que sí cambia es que la rama degradada **también** respeta la escala del
 * catálogo: sin eso, una moneda sin centavos mostraría centavos justo cuando su código es el que
 * `Intl` no entiende.
 */
describe('FR-019 · la degradación también respeta la escala', () => {
  it('un código que Intl no interpreta sigue mostrando el número con su código al lado', () => {
    const formateado = formatearMonto(1250, 'BT1', 0);

    expect(formateado).toContain('BT1');
    expect(formateado).toMatch(/1\.250/);
    expect(formateado).not.toMatch(/1\.250,/);
  });

  it('y con tres decimales muestra tres, no dos', () => {
    expect(formatearMonto(1250, 'BT1', 3)).toMatch(/1\.250,000/);
  });
});

describe('decimalesDe · el catálogo responde, o no responde', () => {
  it('devuelve la escala de la moneda que está en el catálogo', () => {
    expect(decimalesDe([...MONEDAS, SIN_CENTAVOS], 'JPY')).toBe(0);
    expect(decimalesDe(MONEDAS, 'ARS')).toBe(2);
    expect(decimalesDe([LA_DE_TRES], 'XCT')).toBe(3);
  });

  /**
   * **`undefined` y no un 2 escrito a mano.**
   *
   * Un valor por defecto acá sería exactamente la lista de monedas cableada que
   * `verificar-monedas.sh` persigue: diría que la escala normal es dos, que es una afirmación sobre
   * el catálogo hecha desde el código.
   */
  it('devuelve undefined cuando el catálogo no llegó o no tiene esa moneda', () => {
    expect(decimalesDe(undefined, 'ARS')).toBeUndefined();
    expect(decimalesDe(MONEDAS, 'JPY')).toBeUndefined();
  });
});

/**
 * **Una escala que `Intl` no admite no puede tirar la pantalla abajo** (hallazgo 1 de la revisión
 * del PR #28).
 *
 * `moneda.decimales` es `tinyint unsigned`: el esquema admite **0 a 255** y no tiene `CHECK` —es la
 * misma deuda que `moneda.codigo`, anotada como D11-02—. `Intl.NumberFormat` acepta como mucho 20
 * decimales y lanza `RangeError` con más.
 *
 * Hasta el arreglo, un `decimales` grande lanzaba dentro del `try`, caía al `catch`, y **el `catch`
 * volvía a lanzar** porque también le pasaba la escala a `Intl`. El `RangeError` subía, React
 * desmontaba el árbol y la cuenta se quedaba con la pantalla en blanco — que es exactamente el
 * fallo que este `try/catch` fue escrito para prevenir, reintroducido por una columna nueva que
 * tampoco tiene `CHECK`.
 */
describe('FR-019 · una escala imposible se degrada, no tira la pantalla', () => {
  it('un decimales fuera del rango de Intl no lanza', () => {
    expect(() => formatearMonto(1250, 'ARS', 255)).not.toThrow();
  });

  it('y muestra el monto con la escala que Intl deduzca, en vez de no mostrarlo', () => {
    expect(formatearMonto(1250.5, 'ARS', 255)).toMatch(/1\.250,50/);
  });

  it('lo mismo cuando además el código es uno que Intl no interpreta', () => {
    // Los dos guardarraíles a la vez: el código ilegible manda a la rama degradada, y la escala
    // imposible haría lanzar también a esa rama.
    expect(() => formatearMonto(1250, 'BT1', 255)).not.toThrow();
    expect(formatearMonto(1250, 'BT1', 255)).toContain('BT1');
  });

  it('el límite que Intl sí admite se sigue respetando', () => {
    expect(formatearMonto(1250, 'ARS', 20)).toMatch(/1\.250,0{20}/);
  });
});
