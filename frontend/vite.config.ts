import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  test: {
    // jsdom es lo que da un DOM real a Vitest. Sin él no se puede verificar AC-55, que exige
    // recorrer y enviar el formulario con el teclado.
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./tests/setup.ts'],
    include: ['tests/**/*.{test,spec}.{ts,tsx}'],
    // Sin passWithNoTests a propósito: que Vitest falle cuando no encuentra tests es una señal,
    // y la bandera la apagaría en vez de arreglar la causa. Con TDD siempre hay un test primero.
  },
});
