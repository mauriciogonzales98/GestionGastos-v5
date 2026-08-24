import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { PantallaMovimientos } from './movimientos/PantallaMovimientos';
import './estilos/index.css';

/**
 * El día de hoy en `YYYY-MM-DD`, en la zona horaria de quien usa la app.
 *
 * No se usa `toISOString()`: devuelve UTC, y en Argentina (UTC-3) eso adelanta el día desde las
 * 21:00. Un gasto cargado a la noche quedaría con la fecha de mañana.
 *
 * Este "hoy" es sólo la fecha que el formulario propone y la referencia con la que la pantalla
 * decide si el movimiento recién guardado entra en el listado. Quien manda sobre el recorte del
 * mes es el servidor, con la zona de `Aplicacion:ZonaHoraria`.
 *
 * Supuesto que queda en pie, a la vista: el navegador y esa zona son la misma. Si alguien usara la
 * app desde otro huso, su "hoy" podría diferir del que recorta el listado y la confirmación podría
 * decir que el movimiento entra cuando el listado no lo muestra. Cerrarlo del todo exige que el
 * servidor exponga su día actual, y eso es un cambio de contrato.
 */
function hoyLocal(): string {
  const ahora = new Date();
  const mes = String(ahora.getMonth() + 1).padStart(2, '0');
  const dia = String(ahora.getDate()).padStart(2, '0');
  return `${ahora.getFullYear()}-${mes}-${dia}`;
}

const raiz = document.getElementById('raiz');
if (!raiz) {
  throw new Error('No se encontró el elemento #raiz en index.html.');
}

createRoot(raiz).render(
  <StrictMode>
    <PantallaMovimientos hoy={hoyLocal()} />
  </StrictMode>,
);
