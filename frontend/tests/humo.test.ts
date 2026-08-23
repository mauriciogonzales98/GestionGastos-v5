import { describe, expect, it } from 'vitest';

// Este test existe para que la suite del frontend nazca con al menos un test y Vitest no tenga
// que arrancar con --passWithNoTests, que apagaría la señal de "no encontré tests" en vez de
// arreglar la causa. Verifica lo que el resto de la suite da por sentado: que hay un DOM.
describe('entorno de tests', () => {
  it('corre sobre un DOM real, que es lo que AC-55 necesita para probar el teclado', () => {
    const boton = document.createElement('button');
    boton.textContent = 'Registrar';
    document.body.appendChild(boton);

    expect(document.querySelector('button')?.textContent).toBe('Registrar');

    boton.focus();
    expect(document.activeElement).toBe(boton);
  });
});
