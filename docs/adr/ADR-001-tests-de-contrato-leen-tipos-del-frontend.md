# ADR-001 — Los tests del backend leen `frontend/src/api/tipos.ts`

- **Estado**: aceptado
- **Fecha**: 2026-08-23
- **Ticket**: FEAT-001a — Alta de movimientos y listado simple
- **Referencias**: [research.md D-09](../../specs/001-alta-listado-movimientos/research.md),
  `AGENTS.md` (*Architecture conventions*), Principio V de `.specify/memory/constitution.md`

## Contexto

`AGENTS.md` fija que frontend y backend viven separados y que ninguno de los dos alcanza los
archivos del otro. Este ADR documenta la **única excepción** a esa regla, que la propia `AGENTS.md`
declara y remite acá.

El contrato HTTP está escrito **dos veces**: en los `record` de C# que la API serializa, y en las
interfaces de `frontend/src/api/tipos.ts` que el frontend consume. Las dos definiciones se escriben
a mano y nada las mantiene alineadas por construcción.

Eso abre una clase de error que ninguna herramienta del proyecto detecta. Si alguien renombra un
campo en el backend de forma coherente —el `record`, su uso, sus tests—, entonces:

- el build de .NET pasa, porque el backend es coherente consigo mismo;
- `dotnet test` pasa, por lo mismo;
- `tsc --noEmit` pasa, porque el frontend también es coherente **consigo mismo**;
- ESLint y Prettier pasan, porque no es un problema de estilo;
- y la pantalla recibe `undefined` en tiempo de ejecución.

`tsc` verifica que el frontend concuerde con su propia idea del contrato, no con la API real. Esa
distinción es la que este ADR resuelve. El plan `DISC-001` la registra como conclusión medida en su
ítem D-2, y fue la razón por la que ese hallazgo salió como FEATURE y no como una corrección de
configuración.

## Decisión

`frontend/src/api/tipos.ts` es **la fuente de verdad del contrato**.

Los tests de `backend/GestionGastos.Api.Tests/Contrato/` **leen ese archivo** y comparan sus campos
contra el JSON que la API emite de verdad, en las dos direcciones:

- un campo declarado en el contrato que la API no emite → el frontend leería `undefined`;
- un campo que la API emite y el contrato no declara → salió un dato a la red que nadie decidió
  exponer.

`backend/verificar-contrato.sh` comprueba, además, que esa comparación **sabe ponerse en rojo**:
desalinea el contrato a propósito, exige el rojo, restaura y exige el verde. Que los tests pasen
sólo prueba que hoy están alineados; no prueba que la barrera sirva (Principio V).

### Alcance de la excepción, exacto

- Es **lectura**, nunca escritura.
- Es en **una sola dirección**: el backend lee un archivo del frontend. El frontend no lee nada del
  backend, y eso no cambia.
- Alcanza a **un solo archivo**: `frontend/src/api/tipos.ts`. Ningún test del backend puede leer
  otro archivo del frontend amparándose en este ADR.
- Vive en **un solo lugar**: `backend/GestionGastos.Api.Tests/Contrato/TiposDelFrontend.cs`. Ningún
  otro tipo abre ese archivo.
- Es **código de tests**. El proyecto de producción no lo alcanza.

## Consecuencias

**A favor**

- La clase de error entera queda cubierta por algo que se pone en rojo, en vez de por una
  convención que alguien tiene que recordar.
- El costo es cero en producción: no hay build step, ni generador, ni dependencia nueva.

**En contra, y asumido**

- Los tests del backend dependen de la **forma** del archivo del frontend, no sólo de su contenido.
  El parseo es deliberadamente simple —extrae nombres de campo y literales de una unión— y **falla
  ruidosamente** si el archivo deja de tener esa forma, en vez de aprobar de más. Un fallo así
  significa "vení a mirar", no "ajustá el regex".
- La suite de contrato necesita el repositorio completo en disco: no corre contra un paquete
  publicado del frontend. Hoy no hay tal paquete, y si lo hubiera este ADR habría que revisarlo.
- Mover o renombrar `tipos.ts` rompe los tests. Es intencional: ese archivo es el contrato.

## Alternativas consideradas

**Generar los tipos del frontend desde OpenAPI.** Elimina la clase de error entera por
construcción, que es mejor que detectarla. Descartada por ahora: agrega un generador y un paso de
build que `AGENTS.md` obliga a justificar, y convierte al backend en la fuente de verdad, que es la
dirección contraria a la que el equipo eligió. Es la alternativa a reconsiderar si el contrato
crece: la decisión de arquitectura excede a esta feature.

**Duplicar el contrato en un tercer archivo neutral** que ambos lean. Agrega una tercera copia que
también puede desalinearse, y ninguna de las dos primeras deja de existir.

**Confiar en `tsc`.** Es exactamente lo que no funciona, y es lo que este ADR documenta.
