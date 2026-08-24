import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

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
    // Un DOM real es lo que permite verificar AC-55, que exige recorrer y enviar el formulario
    // con el teclado.
    //
    // Es happy-dom y no jsdom, que es lo que research.md D-10 eligió, por una razón del entorno y
    // no del código: este repositorio vive en /mnt/c, un montaje de Windows dentro de WSL2, y ahí
    // jsdom tarda en arrancar más de los 60 s que Vitest espera por un worker. Ese límite
    // (START_TIMEOUT) está hardcodeado en Vitest 4 y no se puede subir por configuración.
    // happy-dom arranca en ~25 s y entra. Si el repositorio se muda a un filesystem Linux nativo,
    // conviene volver a jsdom: tiene más fidelidad y es lo que la decisión documentada eligió.
    environment: 'happy-dom',
    globals: true,
    setupFiles: ['./tests/setup.ts'],
    include: ['tests/**/*.{test,spec}.{ts,tsx}'],
    // Sin passWithNoTests a propósito: que Vitest falle cuando no encuentra tests es una señal,
    // y la bandera la apagaría en vez de arreglar la causa. Con TDD siempre hay un test primero.

    // El repositorio vive en /mnt/c, un montaje de Windows dentro de WSL2. El pool por defecto
    // ('forks') arranca un proceso por archivo y ahí el arranque tarda tanto que el worker hace
    // timeout antes de responder. Con hilos el arranque es de milisegundos. En el runner de CI,
    // que es Linux nativo, cualquiera de los dos anda.
    pool: 'threads',
  },
});
