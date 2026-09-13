import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ErrorDeValidacion } from '../src/api/cliente';
import { PantallaCategorias } from '../src/categorias/PantallaCategorias';
import type { Categoria } from '../src/api/tipos';
import { CATEGORIAS } from './categorias.fixture';

/** Las diez del catálogo inicial más dos creadas a mano: doce filas y ninguna diferencia entre ellas. */
const CON_PROPIAS: Categoria[] = [
  ...CATEGORIAS,
  { id: 43, nombre: 'Gimnasio', tipo: 'gasto' },
  { id: 44, nombre: 'Alquileres', tipo: 'ingreso' },
];

function renderizar(props: Partial<Parameters<typeof PantallaCategorias>[0]> = {}) {
  return render(
    <PantallaCategorias
      categorias={CON_PROPIAS}
      onCrear={vi.fn()}
      onRenombrar={vi.fn()}
      onDarDeBaja={vi.fn()}
      onVolver={vi.fn()}
      {...props}
    />,
  );
}

/** La fila de una categoría, por su nombre visible. */
function fila(nombre: string) {
  return screen.getByRole('listitem', { name: nombre });
}

describe('PantallaCategorias — qué se lista y qué se ofrece', () => {
  it('lista las creadas a mano y las del catálogo inicial juntas', () => {
    renderizar();

    expect(fila('Gimnasio')).toBeInTheDocument();
    expect(fila('Alquileres')).toBeInTheDocument();
    expect(fila('Comida')).toBeInTheDocument();
    expect(fila('Sueldo')).toBeInTheDocument();
  });

  /**
   * FR-017: **todas** las filas ofrecen renombrar y dar de baja, sin excepciones.
   *
   * Este test decía lo contrario hasta la feature 013: comprobaba que una predefinida no ofreciera
   * botones. Esas filas eran del sistema y el servidor respondía `403`, así que esconderlos evitaba
   * un viaje que sólo podía terminar en error. Desde que cada cuenta recibe su propio catálogo no
   * hay ninguna fila de solo lectura, y una condición que siempre da lo mismo es una que esconde
   * botones sin motivo.
   *
   * Se recorren **todas** las filas y no una: comprobar sólo "Comida" pasaría en verde con la
   * condición vieja puesta sobre cualquier otra.
   */
  it('ofrece renombrar y dar de baja en todas las filas FR-017', () => {
    renderizar();

    // Se recorren por posición y no por nombre: "Otros" está dos veces —en gasto y en ingreso— y
    // buscarla por nombre encontraría dos filas.
    const filas = screen.getAllByRole('listitem');
    expect(filas).toHaveLength(CON_PROPIAS.length);

    filas.forEach((suya, i) => {
      const nombre = CON_PROPIAS[i].nombre;

      expect(within(suya).getByRole('button', { name: `Renombrar ${nombre}` })).toBeInTheDocument();
      expect(within(suya).getByRole('button', { name: `Dar de baja ${nombre}` })).toBeInTheDocument();
    });
  });
});

describe('PantallaCategorias — crear', () => {
  it('crea una categoría con el nombre y el tipo elegidos', async () => {
    const usuario = userEvent.setup();
    const onCrear = vi.fn().mockResolvedValue(undefined);

    renderizar({ onCrear });

    await usuario.type(screen.getByLabelText('Nombre'), 'Mascotas');
    await usuario.selectOptions(screen.getByLabelText('Tipo'), 'gasto');
    await usuario.click(screen.getByRole('button', { name: 'Crear categoría' }));

    expect(onCrear).toHaveBeenCalledWith({ nombre: 'Mascotas', tipo: 'gasto' });
  });

  /**
   * El rechazo del servidor se muestra al lado del campo, no se traga (regla de `AGENTS.md`).
   *
   * Es el caso más probable de esta pantalla: el nombre repetido. Sin el mensaje, la persona
   * aprieta "Crear categoría" y no pasa nada visible.
   */
  it('muestra el rechazo del servidor al lado del campo', async () => {
    const usuario = userEvent.setup();
    const onCrear = vi
      .fn()
      .mockRejectedValue(
        new ErrorDeValidacion({ nombre: ['Ya tenés una categoría de ese tipo con ese nombre.'] }),
      );

    renderizar({ onCrear });

    await usuario.type(screen.getByLabelText('Nombre'), 'Comida');
    await usuario.click(screen.getByRole('button', { name: 'Crear categoría' }));

    expect(
      await screen.findByText('Ya tenés una categoría de ese tipo con ese nombre.'),
    ).toBeInTheDocument();

    // Y lo cargado no se pierde: hacer reescribir el nombre para corregir una letra es hostil.
    expect(screen.getByLabelText('Nombre')).toHaveValue('Comida');
  });

  it('un error que no es de validación también se ve', async () => {
    const usuario = userEvent.setup();
    const onCrear = vi.fn().mockRejectedValue(new Error('lo que sea'));

    renderizar({ onCrear });

    await usuario.type(screen.getByLabelText('Nombre'), 'Mascotas');
    await usuario.click(screen.getByRole('button', { name: 'Crear categoría' }));

    expect(await screen.findByRole('alert')).toBeInTheDocument();
  });
});

describe('PantallaCategorias — renombrar y dar de baja', () => {
  it('renombrar una propia manda el nombre nuevo', async () => {
    const usuario = userEvent.setup();
    const onRenombrar = vi.fn().mockResolvedValue(undefined);

    renderizar({ onRenombrar });

    await usuario.click(within(fila('Gimnasio')).getByRole('button', { name: /Renombrar/ }));

    const campo = within(fila('Gimnasio')).getByLabelText('Nombre nuevo');
    await usuario.clear(campo);
    await usuario.type(campo, 'Gimnasio y pileta');
    await usuario.click(within(fila('Gimnasio')).getByRole('button', { name: 'Guardar' }));

    expect(onRenombrar).toHaveBeenCalledWith(43, 'Gimnasio y pileta');
  });

  /**
   * Pasa por la confirmación porque la baja no se dispara con un solo clic: es irreversible, y el
   * porqué está en el bloque "la baja se confirma antes de ejecutarse" de más abajo.
   */
  it('dar de baja una propia la manda por su identificador', async () => {
    const usuario = userEvent.setup();
    const onDarDeBaja = vi.fn().mockResolvedValue(undefined);

    renderizar({ onDarDeBaja });

    await usuario.click(within(fila('Gimnasio')).getByRole('button', { name: /^Dar de baja/ }));
    await usuario.click(
      within(fila('Gimnasio')).getByRole('button', { name: 'Confirmar la baja' }),
    );

    expect(onDarDeBaja).toHaveBeenCalledWith(43);
  });

  it('vuelve a movimientos cuando se lo piden', async () => {
    const usuario = userEvent.setup();
    const onVolver = vi.fn();

    renderizar({ onVolver });

    await usuario.click(screen.getByRole('button', { name: 'Volver a movimientos' }));

    expect(onVolver).toHaveBeenCalled();
  });
});

describe('PantallaCategorias — cada rechazo al lado del control que lo produjo', () => {
  /**
   * El rechazo de un renombre se muestra **en la fila que se está renombrando**, no colgado del
   * campo del alta.
   *
   * El error del servidor viene con la clave `nombre` en los dos casos —es el mismo campo de la
   * misma validación (FR-005)— así que un único estado para los dos lo manda siempre al mismo
   * lugar: el formulario de arriba. La persona ve marcado como inválido un campo que puede estar
   * vacío, y el input que causó el rechazo se queda sin decir nada.
   */
  it('el rechazo del renombre se ve en la fila, no en el formulario de alta', async () => {
    const usuario = userEvent.setup();
    const onRenombrar = vi
      .fn()
      .mockRejectedValue(
        new ErrorDeValidacion({ nombre: ['Ya tenés una categoría de ese tipo con ese nombre.'] }),
      );

    renderizar({ onRenombrar });

    await usuario.click(within(fila('Gimnasio')).getByRole('button', { name: /Renombrar/ }));
    const campo = within(fila('Gimnasio')).getByLabelText('Nombre nuevo');
    await usuario.clear(campo);
    await usuario.type(campo, 'Comida');
    await usuario.click(within(fila('Gimnasio')).getByRole('button', { name: 'Guardar' }));

    expect(await within(fila('Gimnasio')).findByRole('alert')).toHaveTextContent(
      'Ya tenés una categoría de ese tipo con ese nombre.',
    );

    // Y el campo del alta, que no tiene nada que ver, queda limpio.
    expect(screen.getByLabelText('Nombre')).toBeValid();
  });

  /**
   * La otra mitad: el rechazo del alta sigue yendo a su campo. Sin este caso, mover el error del
   * renombre podría llevárselos a los dos.
   */
  it('el rechazo del alta sigue yendo al campo del alta', async () => {
    const usuario = userEvent.setup();
    const onCrear = vi
      .fn()
      .mockRejectedValue(new ErrorDeValidacion({ nombre: ['El nombre no puede estar vacío.'] }));

    renderizar({ onCrear });

    await usuario.type(screen.getByLabelText('Nombre'), 'x');
    await usuario.click(screen.getByRole('button', { name: 'Crear categoría' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('El nombre no puede estar vacío.');
    expect(within(fila('Gimnasio')).queryByRole('alert')).not.toBeInTheDocument();
  });
});

describe('PantallaCategorias — la baja se confirma antes de ejecutarse', () => {
  /**
   * La baja es **un camino de ida**: no hay forma de reactivar una categoría (Key Entities, y la
   * deuda D7-04 lo deja fuera de alcance a propósito). La única salida de un clic errado es crear
   * otra con el mismo nombre (FR-009), y entonces los movimientos viejos quedan apuntando a la
   * vieja: el desglose del resumen muestra dos entradas homónimas y no hay forma de juntarlas.
   *
   * Ninguna otra acción irreversible de la aplicación se dispara con un clic pelado.
   *
   * El paso de confirmación es el mismo patrón de dos botones que ya usa el renombre: sin
   * dependencias nuevas y sin un `window.confirm`, que no se puede maquetar ni testear como parte
   * de la pantalla.
   */
  it('el primer clic no da de baja: pide confirmación', async () => {
    const usuario = userEvent.setup();
    const onDarDeBaja = vi.fn().mockResolvedValue(undefined);

    renderizar({ onDarDeBaja });

    await usuario.click(within(fila('Gimnasio')).getByRole('button', { name: /^Dar de baja/ }));

    expect(onDarDeBaja).not.toHaveBeenCalled();
    expect(
      within(fila('Gimnasio')).getByRole('button', { name: 'Confirmar la baja' }),
    ).toBeInTheDocument();
  });

  it('el segundo clic sí la da de baja', async () => {
    const usuario = userEvent.setup();
    const onDarDeBaja = vi.fn().mockResolvedValue(undefined);

    renderizar({ onDarDeBaja });

    await usuario.click(within(fila('Gimnasio')).getByRole('button', { name: /^Dar de baja/ }));
    await usuario.click(
      within(fila('Gimnasio')).getByRole('button', { name: 'Confirmar la baja' }),
    );

    expect(onDarDeBaja).toHaveBeenCalledWith(43);
  });

  it('cancelar deja la categoría donde estaba', async () => {
    const usuario = userEvent.setup();
    const onDarDeBaja = vi.fn().mockResolvedValue(undefined);

    renderizar({ onDarDeBaja });

    await usuario.click(within(fila('Gimnasio')).getByRole('button', { name: /^Dar de baja/ }));
    await usuario.click(within(fila('Gimnasio')).getByRole('button', { name: 'No dar de baja' }));

    expect(onDarDeBaja).not.toHaveBeenCalled();
    expect(
      within(fila('Gimnasio')).getByRole('button', { name: 'Dar de baja Gimnasio' }),
    ).toBeInTheDocument();
  });
});
