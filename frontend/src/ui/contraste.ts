/**
 * La cuenta de contraste de WCAG 2.1, y nada más.
 *
 * **Los colores ya no viven acá** (D-01 de la feature 011). Hasta la 010 este archivo tenía los
 * cuatro del dashboard, porque eran los únicos del proyecto y los bajaba un solo componente. Desde
 * que existe una paleta entera, la declara `estilos/base.css` y la mide `tests/Paleta.test.ts`
 * leyendo ese archivo: un color tiene que existir antes de que corra JavaScript, y un verificador
 * que lee el CSS cubre cualquier color que alguien agregue sin agendarlo (NFR-003).
 *
 * Lo que quedó acá es la función, que es lo que no depende de qué colores haya.
 */

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
