# Quickstart: Nota descriptiva del movimiento

**Feature**: 012-nota-del-movimiento · **Fecha**: 2026-09-10

Cómo validar la feature. Está dividido a propósito en **lo que ningún test cubre** (pasos 1 a 5, a
mano) y **la puerta** (pasos 6 a 10, comandos). Los primeros son los que importan leer: el resto lo
verifica la suite.

## Prerequisitos

- MySQL 8.4.10 en el 3306, con los esquemas `gestiongastos` y `gestiongastos_test`.
- `ConnectionStrings__Default` en user-secrets apuntando a `gestiongastos` para correr la app, y a
  `gestiongastos_test` para los tests. `BaseDeDatosFixture` **sólo** acepta `gestiongastos_test` o
  `gestiongastos_migracion_test`, y falla contra cualquier otro: migra y limpia tablas, así que
  apuntarlo al esquema de desarrollo se lleva los datos puestos.
- `pnpm --dir frontend install --frozen-lockfile`.
- Una cuenta con al menos un movimiento cargado, y al menos uno **registrado antes de esta feature**
  (o sea, una fila que exista en la base antes de aplicar la migración). El paso 4 lo necesita.

---

## Lo que ningún test cubre

### 1 · La columna nueva a 360 px, medida de verdad

Con el navegador a 360 px de ancho, en el listado:

- La página **no** se desplaza horizontalmente. Lo que se desplaza es el envoltorio de la tabla.
- La columna de la nota se lee, y una nota larga no empuja las otras seis fuera de la vista de forma
  que la tabla quede inusable.

**Esto es D12-01 y es la razón por la que este paso existe.** Los tests verifican por **regla** —que
ninguna hoja de estilos declare un ancho fijo mayor al objetivo y que el contenido ancho tenga su
envoltorio desplazable—, no por medición: jsdom no maqueta. La séptima columna es el caso más apretado
que la tabla tuvo nunca, así que es el paso a mano que más vale la pena de los cinco.

Si no hay navegador en el entorno, **anotarlo como D12-01**, que sigue abierta desde la feature 010 (y
antes, como D10-09 y D11-01).

### 2 · El camino rápido de carga, intacto

Registrar un gasto **sin tocar el campo de la nota**, usando sólo el teclado:

- Tabular desde el principio del formulario hasta el botón y enviar con Enter.
- Cuesta **exactamente un Tab más** que antes de esta feature, y ni una tecla más de datos.
- El movimiento queda registrado sin nota y el listado lo muestra sin texto de relleno.

Es `SC-002` y es la mitad del valor del ticket que se pierde más fácil: la nota nunca está en el camino
rápido.

### 3 · El campo de varias líneas con un lector de pantalla

- El campo tiene nombre accesible y se anuncia como opcional.
- Con una nota de más de 120 caracteres, el error se anuncia **asociado al campo**, no como un cartel
  suelto en otra parte del formulario.
- El foco se ve sobre el campo, con la paleta que la feature 011 dejó declarada.

Los tests verifican que la tripleta esté armada; que se **escuche** bien es lo que no pueden verificar.
Es parte de D12-07.

### 4 · Un movimiento de antes de la migración

En el listado, la fila de un movimiento que ya existía antes de aplicar la migración:

- Se ve **idéntica** a la de uno registrado sin nota: sin texto de relleno, sin `null`, sin error.

Es el caso que cubre toda la base existente, y el que delata una normalización de lectura incompleta
(`FR-011`). Si apareciera un `null` o un guion de relleno, el que está mal es el borde de la API.

### 5 · Los saltos de línea

Escribir una nota en dos líneas, guardar y después reabrir la ventana de edición:

- En el listado se lee en **una sola línea visual**, sin que el salto agregue ni quite nada.
- En la ventana de edición, el campo la trae **con sus saltos intactos**: lo guardado es lo que se
  escribió (`FR-012`).

---

## La puerta

### 6 · La puerta del frontend

```bash
pnpm --dir frontend lint
pnpm --dir frontend format
pnpm --dir frontend exec tsc --noEmit
pnpm --dir frontend test
```

`format` es `prettier --check`: **verifica sin modificar**. Para formatear de verdad,
`pnpm --dir frontend format:fix`.

### 7 · La puerta del backend

```bash
dotnet format backend/GestionGastos.slnx --verify-no-changes
dotnet build backend/GestionGastos.slnx -warnaserror
dotnet test backend/
```

La migración se aplica sola al correr los tests. En local corren **todos**, incluido
`RendimientoListadoTests`; en CI los de rendimiento quedan fuera por
`--filter "FullyQualifiedName!~Rendimiento"`.

**Lo esperable del test de rendimiento nuevo**: el resumen agrupa las mismas 1000 filas en 6 ms, así que
el listado con una columna de texto más debería quedar en el mismo orden de magnitud, muy lejos del
techo de 2000 ms. Si diera cerca del techo, el que está mal es el diseño y no el techo.

### 8 · Commitear antes de las barreras

```bash
git status --short   # tiene que estar vacío
```

`verificar-monedas.sh` **exige los dos árboles limpios** o no puede distinguir lo que ensució ella de lo
que ya estaba sucio.

### 9 · Las siete barreras

```bash
./backend/verificar-nota.sh          # NUEVA: la nota no entra en el filtro del listado
./backend/verificar-contrato.sh      # ~2,5 min — el campo nuevo viaja en las tres formas
./backend/verificar-monedas.sh       # ~1 min — FR-010 podría romperla y no lo hace
./backend/verificar-aislamiento.sh   # ~7 min — la nota es el primer texto libre que tapa
./backend/verificar-autorizacion.sh
./backend/verificar-desglose.sh
./backend/verificar-linter.sh
```

**~13 min en total.** Las dos que más de cerca miran esta feature son la del contrato —el campo viaja en
las tres formas del movimiento— y la de monedas, por `FR-010`: siembra con `XTS`, que son tres letras,
así que la restricción nueva no la afecta (verificado en [data-model.md](./data-model.md)).

### 10 · El cierre

```bash
dotnet test backend/GestionGastos.slnx --settings backend/cobertura.runsettings
pnpm --dir frontend build
git diff main -- frontend/package.json frontend/pnpm-lock.yaml 'backend/**/*.csproj'   # vacío (NFR-005)
git diff --stat main -- frontend/tests backend/GestionGastos.Api.Tests                 # contra la tabla de D-12
```

El último comando es la verificación de que el presupuesto de tests tocables se respetó: cada test
modificado tiene que estar en la tabla de [D-12](./research.md). Si hay uno que no está, se justifica o
se revierte.

---

## Cómo saber que algo salió mal, y dónde mirar

| Síntoma | Dónde está el problema |
|---|---|
| Un `null` o un guion de relleno en la columna de la nota | La normalización de lectura no está en el único lugar que la hereda (D-04, `FR-011`) |
| Una nota de 120 emoji rechazada por "superar 120" | Alguna de las dos validaciones cuenta unidades UTF-16 en vez de caracteres Unicode (D-02) |
| Una nota de 120 caracteres con espacios alrededor rechazada | Se mide antes de recortar, y el orden es al revés (D-03) |
| El error del largo aparece al pie del formulario y no al lado del campo | Falta `nota` en la lista de campos con lugar propio (D-05) |
| Un test de `Resumenes/`, `Rendimiento/` (salvo el nuevo), aislamiento o categorías en rojo | **El código de esta feature.** La nota no toca los totales ni el aislamiento: un rojo ahí es información, no un test para arreglar (D-12) |
| `verificar-monedas.sh` en rojo por el código de una moneda | La restricción de `FR-010` es más estricta de lo que se escribió, o la barrera cambió con qué siembra |
| El test de teclado en rojo | **Esperado**: enumera el orden de tabulación y hay un control nuevo. Extenderlo es trabajo previsto (D-07, fila 2 de D-12) |
