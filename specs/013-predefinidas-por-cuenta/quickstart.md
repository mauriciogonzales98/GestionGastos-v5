# Quickstart: cómo comprobar que esta feature funciona

**Spec**: [spec.md](./spec.md) | **Diseño**: [research.md](./research.md), [data-model.md](./data-model.md)

No es una guía de implementación: es lo que hay que correr y lo que hay que ver para creerle.

---

## Prerrequisitos

```bash
export ConnectionStrings__Default="Server=127.0.0.1;Port=3306;Database=gestiongastos_test;User Id=<usuario>;Password=<clave>;"
```

`BaseDeDatosFixture` sólo acepta `gestiongastos_test` o `gestiongastos_migracion_test`: migra y
limpia tablas, así que apuntarlo al esquema de desarrollo se lleva los datos puestos.

**Todo corre contra `gestiongastos_test`, incluido el test de migración.** La segunda base de la
lista blanca no la usa nadie: el usuario de MySQL del proyecto no puede crear una tercera base
—`CREATE DATABASE` responde *Access denied*, verificado el 2026-09-12— y por eso el test de
migración mueve el esquema de la misma base que usa el resto de la suite. Eso es lo que obliga a
que vaya serializado en la colección compartida.

---

## 1 · La puerta de VERIFY, por tarea

```bash
dotnet format backend/GestionGastos.slnx --verify-no-changes
dotnet build backend/GestionGastos.slnx -warnaserror
dotnet test backend/
pnpm --dir frontend lint && pnpm --dir frontend exec tsc --noEmit && pnpm --dir frontend test
```

**Durante la tarea del contrato, el rojo es el esperado.** Sacar `esPropia` de una sola pila deja los
tests de `Contrato/` en rojo a propósito; se ponen en verde cuando cambian las dos.

---

## 2 · Que una cuenta nueva nazca con sus diez (US1)

```bash
dotnet test backend/ --filter "FullyQualifiedName~AltaDeCuenta"
```

Se tiene que ver un caso que registra una cuenta y pide su catálogo: **diez** categorías, siete de
gasto y tres de ingreso, todas activas. Y un caso con **dos** cuentas, comprobando que los
identificadores de una no aparecen en el catálogo de la otra aunque los nombres coincidan.

---

## 3 · Que la base rechace por su cuenta lo que la aplicación ya rechazaba (US2)

Es el corazón de la feature, y se verifica **sin pasar por la aplicación**: SQL directo contra la
base, como ya hacen los tests de esquema de la nota y del código de moneda.

```bash
dotnet test backend/ --filter "FullyQualifiedName~AmbitoDeCategoriaEsquema"
```

Tienen que estar los cuatro casos de `FR-006` y `FR-009`:

| Escritura directa | Esperado |
|---|---|
| `INSERT` de un movimiento de A con categoría de B | rechazado por la base |
| `UPDATE` de un movimiento de A poniéndole categoría de B | rechazado por la base |
| `INSERT` de un movimiento de A con categoría **de A** | aceptado |
| La suite completa | verde: la restricción no rechaza ningún caso legítimo |

**Comprobación manual, si querés verlo con los ojos** (contra `gestiongastos_test`, que los tests
limpian igual):

```sql
-- con dos cuentas ya creadas y sus catálogos:
INSERT INTO movimiento (usuario_id, tipo, monto, moneda_id, categoria_id, fecha)
VALUES (<cuentaA>, 0, 100, 1, <categoria_de_cuentaB>, '2026-09-12');
-- esperado: ERROR 1452 (23000) ... foreign key constraint fails
```

---

## 4 · Que la migración no haya movido un solo número (US3)

La forma de comprobarlo es **antes y después sobre la misma base**, no un test de una base vacía.

```bash
# 1. Sobre una copia de `gestiongastos` ANTES de migrar, guardar la foto:
#    por cuenta y por período: total ingresado, total gastado, balance y desglose por categoría.
# 2. Aplicar la migración.
# 3. Repetir la consulta y comparar. Tiene que dar idéntico (FR-012, SC-003).
```

Y las dos comprobaciones que la propia base hace al migrar:

```sql
SELECT COUNT(*) FROM categoria WHERE usuario_id IS NULL;  -- esperado: 0
SELECT COUNT(*) FROM movimiento m JOIN categoria c ON c.id = m.categoria_id
 WHERE c.usuario_id <> m.usuario_id;                      -- esperado: 0
```

Si alguna de las dos da distinto de cero, la migración no debería haber terminado: revisá que la
foránea compuesta y el `NOT NULL` estén efectivamente puestos.

---

## 5 · Que el catálogo sea enteramente editable (US4)

```bash
pnpm --dir frontend test PantallaCategorias
dotnet test backend/ --filter "FullyQualifiedName~CategoriasPropias"
```

Ninguna fila del catálogo debe quedar sin botones, y ningún renombre ni baja debe responder `403`:
esa respuesta ya no existe para categorías del ámbito.

---

## 6 · Antes de cerrar la feature: todas las barreras

```bash
dotnet test backend/GestionGastos.slnx --settings backend/cobertura.runsettings
./backend/verificar-contrato.sh          # ~2,5 min
./backend/verificar-autorizacion.sh
./backend/verificar-desglose.sh
./backend/verificar-monedas.sh           # ~1 min, exige los dos árboles limpios
./backend/verificar-nota.sh
./backend/verificar-aislamiento.sh       # ~4 min
./backend/verificar-linter.sh            # va después de los tests
./backend/verificar-ambito-de-categoria.sh   # la nueva (D-09)
pnpm --dir frontend build
```

**`verificar-aislamiento.sh` es la que más probablemente se ponga en rojo con esta feature**, y por
una razón conocida de antemano: declara `CategoriasEndpoints.cs` como el único archivo que puede
escribir categorías, y ahora hay un segundo (D-06 de research). Si se pone en rojo por eso, la
corrección es declarar el archivo nuevo — no agregar una excepción genérica.

**`verificar-monedas.sh` exige los dos árboles de trabajo limpios antes de empezar**: commiteá o
guardá lo que tengas suelto antes de correrla, o no puede distinguir lo que ensució ella de lo que
ya estaba sucio.
