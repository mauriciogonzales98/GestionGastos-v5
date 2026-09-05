/**
 * Los colores que el dashboard declara, y la cuenta que verifica que alcanzan (`PRD:AC-13`).
 *
 * **Son pocos a propósito.** `estilos/base.css` dice, en su primer comentario, que colores,
 * espaciados y tipografía son del ticket 6, y el proyecto no tiene ni un color declarado. Inventar
 * acá una paleta entera sería hacer ese trabajo antes de tiempo y sin su contexto, para que después
 * lo rehagan. Lo que hay acá es el mínimo que el dashboard necesita para dibujar una barra.
 *
 * Y son la **única** fuente: el componente los baja a variables CSS en su propio elemento, así que
 * no hay una copia en la hoja de estilos que pueda quedar desincronizada de la que el test mide.
 */
export const COLORES_DEL_DASHBOARD = {
  /**
   * El texto y el fondo de la página. **Hoy son los del navegador**, no una elección: el proyecto
   * no declara colores todavía. Están escritos acá porque el test necesita medir contra algo, y
   * porque el día que el ticket 6 traiga la paleta, este archivo es donde se va a notar si el par
   * elegido no llega a 4,5:1.
   */
  texto: '#000000',
  fondo: '#ffffff',

  /**
   * El relleno de **todas** las barras. Una sola entrada y no una paleta categórica: las categorías
   * no se codifican por color (D-04). Si el color no lleva información, no hay nada que un
   * daltonismo pueda quitarle.
   */
  barra: '#1f5c8b',

  /** El riel sobre el que se dibuja la barra, para que se vea de dónde a dónde va. */
  rielDeLaBarra: '#e8e8e8',
} as const;

/** Los tres canales de un color, de 0 a 255. */
function canales(color: string): [number, number, number] {
  const rgb = /^rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)$/.exec(color);
  if (rgb) {
    return [Number(rgb[1]), Number(rgb[2]), Number(rgb[3])];
  }

  const hex = color.replace('#', '');
  // `#abc` es la forma corta de `#aabbcc`: cada dígito se duplica.
  const completo =
    hex.length === 3
      ? hex
          .split('')
          .map((d) => d + d)
          .join('')
      : hex;

  if (!/^[0-9a-fA-F]{6}$/.test(completo)) {
    // No es un catch silencioso ni un valor por defecto disimulado: un color que no se puede leer
    // es un error del código que lo escribió, y devolver "negro" haría que el verificador informara
    // un contraste que nadie va a ver.
    throw new Error(`No se puede interpretar el color "${color}".`);
  }

  return [
    parseInt(completo.slice(0, 2), 16),
    parseInt(completo.slice(2, 4), 16),
    parseInt(completo.slice(4, 6), 16),
  ];
}

/** La luminancia relativa de WCAG 2.1, que es lo que la relación de contraste compara. */
function luminancia(color: string): number {
  const [r, g, b] = canales(color).map((canal) => {
    const proporcion = canal / 255;
    // La corrección de gamma: el ojo no percibe el brillo de forma lineal, y sin esto dos colores
    // que se ven muy distintos podrían dar la misma cuenta.
    return proporcion <= 0.04045 ? proporcion / 12.92 : ((proporcion + 0.055) / 1.055) ** 2.4;
  });

  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/**
 * La relación de contraste entre dos colores, de 1:1 a 21:1.
 *
 * AA pide **4,5:1 en texto normal** y **3:1 en texto grande y en componentes de interfaz**
 * (`PRD:RNF-06`, `NFR-003`).
 *
 * Es simétrica: cuál es el frente y cuál el fondo no cambia el número.
 */
export function relacionDeContraste(unColor: string, otroColor: string): number {
  const uno = luminancia(unColor);
  const otro = luminancia(otroColor);

  const masClaro = Math.max(uno, otro);
  const masOscuro = Math.min(uno, otro);

  return (masClaro + 0.05) / (masOscuro + 0.05);
}
