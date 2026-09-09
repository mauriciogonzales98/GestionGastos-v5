# Quickstart: Maquetación, filtros del listado y accesibilidad

Cómo comprobar que la feature 011 hace lo que dice. Primero la puerta automática, después los pasos
a mano — que acá pesan más que en cualquier feature anterior, porque **lo que esta feature entrega
es cómo se ve y cómo se recorre**, y de eso hay una parte que ningún test de este proyecto puede
medir (D-04).

## Prerequisitos

```bash
pnpm --dir frontend install --frozen-lockfile
# La cadena de conexión va en user-secrets, nunca en appsettings.
dotnet user-secrets --project backend/GestionGastos.Api list | grep ConnectionStrings
export ConnectionStrings__Default='...;Database=gestiongastos_test;...'
```

## La puerta, en orden

```bash
# Frontend — es la puerta de cuatro de las cinco historias
pnpm --dir frontend lint
pnpm --dir frontend format
pnpm --dir frontend exec tsc --noEmit
pnpm --dir frontend test
pnpm --dir frontend build

# Backend — sólo la Historia 5 lo toca, y toca un campo
dotnet format backend/GestionGastos.slnx --verify-no-changes
dotnet build backend/GestionGastos.slnx -warnaserror
dotnet test backend/
dotnet test backend/GestionGastos.slnx --settings backend/cobertura.runsettings
```

Y al cierre, las **seis** barreras. `verificar-monedas.sh` **exige los dos árboles limpios**: hay que
commitear antes de correrla, o no puede distinguir lo que ensució ella de lo que ya estaba sucio.

```bash
git status --short          # tiene que estar vacío antes de la de monedas
./backend/verificar-contrato.sh        # ~2,5 min · el campo nuevo del contrato
./backend/verificar-autorizacion.sh
./backend/verificar-desglose.sh
./backend/verificar-linter.sh
./backend/verificar-monedas.sh         # ~1 min · LA barrera crítica de esta feature
./backend/verificar-aislamiento.sh     # ~7 min
```

**Por qué la de monedas es la crítica acá**: `FR-019` toca `formatearMonto`, que es el código que esa
barrera vigila en las dos pilas. Si alguien resolviera los decimales con una lista escrita a mano en
el frontend —`{ ARS: 2, USD: 2, JPY: 0 }`— todos los tests darían verde y la promesa de `RF-32`
estaría rota del único lado que el usuario mira. La barrera se pone en rojo. Es su día.

## Los pasos a mano

Se necesita la aplicación corriendo y un navegador. Los pasos 1 a 4 son los que **ningún test de
este proyecto puede reemplazar**.

```bash
dotnet run --project backend/GestionGastos.Api &
pnpm --dir frontend dev
```

### 1. El ancho de 360 px, medido de verdad (`FR-003`, `FR-004`, `SC-002`)

Abrir las herramientas de desarrollo, poner el ancho de la ventana en **360 px**, y recorrer las
cinco superficies: acceso, movimientos, la ventana de edición, categorías y dashboard.

En cada una: **la página no debe desplazarse en horizontal**. La tabla del listado sí puede, dentro
de su propio contenedor.

> **Esto es lo que `AnchoDeLasPantallas.test.ts` no verifica.** Ese test comprueba *reglas de
> estilo* —que ninguna declare un ancho fijo mayor a 360 px, que el contenedor de la tabla declare
> desborde desplazable—. jsdom no maqueta, así que un verde ahí significa "ninguna regla puede
> producir desborde", no "no desborda". Es la deuda **D11-01** y este paso es su única cobertura
> real hoy.

### 2. El recorrido completo con el teclado, sin tocar el mouse (`FR-005` a `FR-009`)

Desde el acceso, sólo con `Tab`, `Shift+Tab`, `Enter`, `Espacio` y las flechas:

1. Crear la cuenta o entrar.
2. Registrar un movimiento entero.
3. Abrir la ventana de edición desde una fila, corregir la moneda, guardar. **Comprobar que con la
   ventana abierta el `Tab` no se escapa al fondo**, y que al cerrarla el foco vuelve a un lugar
   sensato.
4. Aplicar los tres acotados.
5. Eliminar un movimiento y confirmar. **Comprobar que después de que la fila desaparece el foco no
   quedó en el principio de la página** (D-09).
6. Ir a categorías, crear una, renombrarla, darla de baja.
7. Ir al dashboard, cambiar el período y la moneda, volver.

En todo el recorrido: **siempre se ve dónde está el foco**, y ningún control queda inalcanzable.

### 3. La pantalla leída, no mirada

Recorrer el formulario de registro con un lector de pantalla si hay uno a mano, o en su defecto
inspeccionar el árbol de accesibilidad en las herramientas de desarrollo. Cada control tiene que
anunciar **qué es**; los botones de una fila tienen que decir sobre qué actúan ("Eliminar el
movimiento del 2026-09-01 de $1.500"), no sólo "Eliminar".

Intentar guardar con el monto vacío: el motivo tiene que llegar **al llegar al campo**, no como un
cartel suelto arriba de la pantalla (`FR-008`).

### 4. El color, mirado por alguien (`NFR-001`)

`Paleta.test.ts` mide la relación de contraste de cada par declarado y es una cuenta exacta: si pasa,
pasa. Lo que la cuenta no dice es si la paleta **se ve bien**, y eso hay que mirarlo. Es el único
criterio de esta feature que no tiene forma de verificarse solo, y está bien que así sea.

### 5. Los decimales, con una moneda que no use dos (`FR-019`)

```sql
-- En gestiongastos (desarrollo), no en el esquema de tests.
INSERT INTO moneda (codigo, nombre, simbolo, decimales, es_predeterminada)
VALUES ('JPY', 'Yen japonés', '¥', 0, 0);
```

Recargar, registrar un movimiento en yenes y mirarlo en las **tres** pantallas: el listado, el
resumen del mes y el dashboard. **Ninguna debe mostrar centavos.** Después borrar la fila del
catálogo.

Es el mismo movimiento que hace `verificar-monedas.sh`, y sirve para lo mismo: comprobar que sumar
una moneda es sólo un dato.

### 6. El acotado, contra los números que ya se conocen

1. Cargar movimientos en dos categorías, dos monedas y dos meses.
2. Acotar por una categoría → sólo esa (`PRD:AC-23`).
3. Quitar el acotado de categoría → todas (`PRD:AC-24`).
4. Poner un rango que incluya un solo extremo de los datos → los extremos entran (`PRD:AC-26`).
5. Poner la fecha final antes que la inicial → **el mensaje del servidor, al lado de los campos**, y
   el listado sigue mostrando lo que mostraba (`FR-018`).
6. Cargar un solo extremo → el mismo tratamiento.
7. Los tres acotados a la vez → sólo lo que cumple los tres (`FR-016`).

En la pestaña de red: **una sola petición por cada "Aplicar"**, y ninguna mientras se escribe una
fecha (`NFR-005`, D-06).

### 7. El borrado, contra el resumen (`PRD:AC-21`)

1. Anotar el total gastado del mes que muestra el resumen, arriba.
2. Eliminar un gasto de ese mes y confirmar.
3. El total baja exactamente por ese monto, **sin recargar la página** (D-11).
4. Apretar "Eliminar" y después cancelar: en la pestaña de red, **ninguna petición** (`FR-011`).
5. Eliminar el último movimiento del listado: aparece el mensaje de vacío, no una tabla sin filas.

## Lo que queda sin comprobar, y se dice

| Qué | Por qué | Deuda |
|---|---|---|
| Que ninguna pantalla desborde a 360 px, medido | jsdom no maqueta; el test verifica reglas, no anchos | **D11-01** |
| Que la paleta se vea bien | No hay criterio automatizable para eso, y no debería haberlo | — |
| El comportamiento con lectores de pantalla concretos | `PRD:RNF-06` pide etiquetas, foco y asociación de errores, no una auditoría de ARIA | **D11-06** |
| Los pasos 1 a 4, si no hay navegador en el entorno donde se implemente | Es lo que le pasó a la feature 010 | **D10-09**, que sigue abierta |
