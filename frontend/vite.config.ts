import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import { playwright } from '@vitest/browser-playwright';

/**
 * Lo que comparten los dos proyectos de prueba: el entorno de hoy y el navegador que mide.
 *
 * Se declara una vez y se referencia dos veces en lugar de usar `extends: true`, para que al leer
 * cada proyecto se vea exactamente con qué corre sin tener que ir a buscar qué heredó.
 */
const TESTS_DEL_DOM = 'tests/**/*.{test,spec}.{ts,tsx}';
const TESTS_QUE_MIDEN = 'tests/**/*.navegador.test.tsx';

export default defineConfig({
  plugins: [react()],

  server: {
    proxy: {
      // El cliente pide a rutas relativas (`/api/...`), así que en producción front y API salen
      // del mismo origen. En desarrollo no: Vite sirve en 5173 y la API en 5125.
      //
      // Sin este proxy, `fetch('/api/categorias')` le pegaba a Vite, que respondía su index.html
      // con 200 — o sea `respuesta.ok` en true y un HTML donde el cliente esperaba JSON. La
      // pantalla quedaba cargando para siempre y nadie se enteraba.
      //
      // El puerto es el de backend/GestionGastos.Api/Properties/launchSettings.json.
      '/api': {
        target: 'http://localhost:5125',
        changeOrigin: true,
      },
    },
  },

  test: {
    /**
     * **Dos proyectos, y la diferencia entre ellos es qué pueden afirmar** (feature 014, D-01).
     *
     * `dom` es el de siempre: rápido, corre todo, y no maqueta. `navegador` abre Chromium de
     * verdad, así que `getBoundingClientRect`, `scrollWidth` y `matchMedia` devuelven números. Una
     * feature entera visual no se puede dar por verificada sin eso: era la deuda D11-01.
     *
     * `pnpm test` corre los dos. Si Chromium no arranca, el segundo **falla**; no se saltea.
     */
    projects: [
      {
        // Hereda del archivo que lo declara: plugins y `server` salen de arriba. Explícito y no por
        // defecto porque Vitest avisa cuando no se dice, y porque leyendo el proyecto conviene ver
        // de dónde le viene el plugin de React.
        extends: true,
        test: {
          name: 'dom',

          // Un DOM real es lo que permite verificar AC-55, que exige recorrer y enviar el
          // formulario con el teclado.
          //
          // Es happy-dom y no jsdom, que es lo que research.md D-10 de la feature 011 eligió, por
          // una razón del entorno y no del código: este repositorio vive en /mnt/c, un montaje de
          // Windows dentro de WSL2, y ahí jsdom tarda en arrancar más de los 60 s que Vitest espera
          // por un worker. Ese límite (START_TIMEOUT) está hardcodeado y no se puede subir por
          // configuración. happy-dom arranca en ~25 s y entra. Si el repositorio se muda a un
          // filesystem Linux nativo, conviene volver a jsdom: tiene más fidelidad y es lo que la
          // decisión documentada eligió.
          environment: 'happy-dom',
          globals: true,
          setupFiles: ['./tests/setup.ts'],

          include: [TESTS_DEL_DOM],

          // Los que miden son del otro proyecto. Sin esta exclusión los correría happy-dom, que no
          // maqueta: devolvería ceros y los daría por pasados, que es exactamente el verde vacío
          // que esta feature vino a eliminar.
          exclude: [TESTS_QUE_MIDEN],

          // Sin passWithNoTests a propósito: que Vitest falle cuando no encuentra tests es una
          // señal, y la bandera la apagaría en vez de arreglar la causa. Con TDD siempre hay un
          // test primero.

          // El repositorio vive en /mnt/c, un montaje de Windows dentro de WSL2. El pool por
          // defecto ('forks') arranca un proceso por archivo y ahí el arranque tarda tanto que el
          // worker hace timeout antes de responder. Con hilos el arranque es de milisegundos. En el
          // runner de CI, que es Linux nativo, cualquiera de los dos anda.
          pool: 'threads',
        },
      },
      {
        extends: true,
        test: {
          name: 'navegador',
          globals: true,

          // Setup propio: el de `dom` importa `@testing-library/jest-dom/vitest`, que espera el DOM
          // inyectado por el entorno. El modo navegador trae sus propios matchers.
          setupFiles: ['./tests/setup.navegador.ts'],

          include: [TESTS_QUE_MIDEN],

          browser: {
            enabled: true,
            provider: playwright(),

            // Chromium solo. Firefox y WebKit triplican el tiempo y la descarga, y lo que se mide
            // —anchos y alturas de CSS estándar— no cambia entre motores de forma que importe acá.
            // Queda para cuando lo mobile necesite Safari de verdad (D-01).
            instances: [{ browser: 'chromium' }],
            headless: true,

            // El tamaño de partida. Cada prueba fija el suyo con el ayudante de `tests/anchos.ts`:
            // lo que se mide es cómo cambia la pantalla con el ancho, así que ninguna prueba puede
            // depender de este valor.
            viewport: { width: 1440, height: 900 },
          },
        },
      },
    ],
  },
});
