# Quickstart: verificar que una moneda es sólo un dato

**Feature**: `008-monedas-como-dato` · **Fecha**: 2026-09-03

Cómo comprobar a mano lo que esta feature automatiza. Son siete pasos y no hace falta escribir ni
una línea de C#: si en algún momento tenés que abrir un archivo de `backend/GestionGastos.Api/`, la
feature no cumplió su promesa.

**Prerrequisitos**: MySQL 8.4.10 en `127.0.0.1:3306`, el esquema `gestiongastos_test` migrado, el
cliente `mysql` en el `PATH`, y `ConnectionStrings__Default` apuntando a ese esquema.

---

## 1 · Ver el catálogo tal como viene

```bash
mysql -h 127.0.0.1 -u gestiongastos -p"$CLAVE" gestiongastos_test \
  -e "SELECT id, codigo, nombre, es_predeterminada FROM moneda ORDER BY id;"
```

**Esperado**: dos filas, `ARS` y `USD`, con `ARS` en `es_predeterminada`. Es AC-03 y FR-003.

---

## 2 · Compilar UNA vez y anotar el hash

```bash
dotnet build backend/GestionGastos.slnx -warnaserror
sha256sum backend/GestionGastos.Api/bin/Debug/net10.0/GestionGastos.Api.dll
```

Guardá ese hash. Es la línea de base de "0 recompilaciones": todo lo que sigue tiene que terminar con
el mismo número. La otra mitad —"0 líneas modificadas"— la comprueba `git status` en el paso 6, y
son dos mecanismos distintos por el motivo que ahí se explica.

---

## 3 · Agregar una moneda, sólo como dato

`XTS` es el código que ISO 4217 reserva para pruebas, así que no colisiona con ninguna moneda real
— y es el mismo que usa `verificar-monedas.sh`, del que este recorrido es la versión a mano. **No
uses `EUR`**: es el que usan `MonedaComoDatoTests` para crear la suya, y dejarlo puesto los rompe
con un choque contra `IX_moneda_codigo`.

```bash
mysql -h 127.0.0.1 -u gestiongastos -p"$CLAVE" gestiongastos_test -e \
  "INSERT INTO moneda (codigo, nombre, simbolo, decimales, es_predeterminada)
   VALUES ('XTS', 'Moneda de prueba', '¤', 2, 0);"
```

Una fila. Ningún archivo abierto, ningún proyecto recompilado.

---

## 4 · Pedir el resumen y ver que el euro está

```bash
dotnet test backend/GestionGastos.slnx --no-build \
  --filter "FullyQualifiedName~MonedaComoDato"
```

**Esperado**: verde, y el resumen devuelve **tres** entradas — `ARS` con sus números, `USD` en cero y
`XTS` en cero. Es AC-01 y FR-002.

**`--no-build` no es una optimización**: es la mitad de la afirmación. Si hiciera falta recompilar
para que el euro aparezca, este comando fallaría en vez de disimularlo.

---

## 5 · Registrar un movimiento en euros

Sin selector todavía (eso es 4b), la vía que el sistema permite es mover la predeterminada — que
también es administración del catálogo como dato. **Dos sentencias, apagar y después prender**: una
sola puede violar `ux_moneda_unica_predeterminada` según el orden en que el motor toque las filas.

```bash
mysql -h 127.0.0.1 -u gestiongastos -p"$CLAVE" gestiongastos_test -e \
  "UPDATE moneda SET es_predeterminada = 0 WHERE codigo = 'ARS';
   UPDATE moneda SET es_predeterminada = 1 WHERE codigo = 'EUR';"
```

Ahora registrá un gasto por la API, como lo haría cualquiera. **Esperado**: se acepta, y su monto
suma en los totales de `XTS` y en los de ninguna otra. Es AC-02, y es la prueba de que RF-032 se
cumple de punta a punta.

---

## 6 · Comprobar que no se recompiló nada

```bash
sha256sum backend/GestionGastos.Api/bin/Debug/net10.0/GestionGastos.Api.dll
git status --porcelain backend/GestionGastos.Api/
```

**Esperado**: el mismo hash del paso 2, y `git status` **vacío**. Cero recompilaciones y cero
líneas modificadas: AC-01 entero, y `PRD:NFR-01`.

**Son dos comprobaciones y no una, a propósito.** El hash cubre la recompilación; el `git status`
cubre las líneas. Un hash del árbol de fuentes tomado antes y después no serviría para lo segundo:
un archivo tocado *antes* de empezar entra en los dos hashes y los deja iguales.

---

## 7 · Dejar el catálogo como estaba

**No es prolijidad, es lo que evita romper la corrida siguiente.** Una moneda de más sobrevive a la
sesión y el próximo test que cuente entradas del resumen falla por lo que hiciste vos, no por el
código.

```bash
mysql -h 127.0.0.1 -u gestiongastos -p"$CLAVE" gestiongastos_test -e \
  "UPDATE moneda SET es_predeterminada = 0 WHERE codigo = 'EUR';
   UPDATE moneda SET es_predeterminada = 1 WHERE codigo = 'ARS';
   DELETE FROM moneda WHERE codigo = 'EUR';"
```

El `DELETE` falla si algún movimiento quedó apuntando a `XTS` — la clave foránea es `RESTRICT`.
Borrá esos movimientos primero. Que falle es correcto: es la base impidiendo que el catálogo quede
inconsistente.

---

## Todo esto, automatizado

```bash
./backend/verificar-monedas.sh
```

Hace los siete pasos, restaura el catálogo con un `trap` aunque se interrumpa, y exige el hash
intacto. **Y se lo vio fallar antes de darlo por bueno**: un script de verificación que nunca falló
no verifica nada.

## La medición de rendimiento

```bash
dotnet test backend/GestionGastos.slnx --filter "FullyQualifiedName~RendimientoResumen"
```

**Esperado**: los dos casos en verde — 1000 movimientos en una moneda y 1000 repartidos en dos, los
dos bajo 2 s en el percentil 95. Si el de dos monedas falla y el de una pasa, el costo lo agregó la
segunda moneda y la salida es el índice por `categoria_id` que la feature 006 dejó anotado en su
deuda D6-05, **con el número en la mano**.

No corre en CI: mide tiempo de pared y en un runner compartido da rojos que no dicen nada.
