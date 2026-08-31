# Quickstart — Aislamiento entre cuentas verificado

Cómo comprobar que esta feature hace lo que dice. Tres niveles: la suite, la barrera, y a mano con
dos cuentas de verdad.

## Prerequisitos

- MySQL 8.4 corriendo en `localhost:3306`.
- `ConnectionStrings__Default` apuntando a **`gestiongastos_test`**. `BaseDeDatosFixture` sólo acepta
  ese nombre o `gestiongastos_migracion_test`: migra y limpia tablas, así que apuntarlo al esquema de
  desarrollo se lleva los datos puestos.
- .NET SDK 10.0.301.

No hay migración que aplicar: esta feature no cambia el esquema.

## 1 · La suite

```bash
dotnet test backend/GestionGastos.slnx --filter "FullyQualifiedName~Aislamiento"
```

Esperado: verde. Cubre los escenarios cruzados de las dos historias y los dos tests de la barrera.

Ojo con lo que este verde significa y lo que no. Todos estos tests verifican comportamiento **que ya
existía** antes de la feature, así que verde no prueba que sirvan: un test de aislamiento roto se ve
igual que uno que funciona. Para eso está el paso 2.

## 2 · La barrera, y la prueba de que sabe fallar

```bash
./backend/verificar-aislamiento.sh
```

Quita el acotado por cuenta de la consulta del listado, exige el **rojo**, lo restaura y exige el
verde. Es el Principio V de la constitución: una barrera que nunca se vio fallar no es una barrera.

Esperado, con esta forma exacta:

```text
== 1/5 · con el aislamiento puesto, la barrera tiene que estar en verde
   verde, como se esperaba
== 2/5 · sin el acotado por cuenta tiene que ponerse en ROJO
   rojo, como se esperaba
== 3/5 · con una lectura fuera del canal tiene que ponerse en ROJO
   rojo, como se esperaba
== 4/5 · con el alta asignando un propietario ajeno tiene que ponerse en ROJO
   rojo, como se esperaba
== 5/5 · restaurado tiene que volver al verde
   verde de nuevo

Barrera de aislamiento: EN PIE. Sabe detectar una consulta que no acota por cuenta.
```

Si alguno de los pasos 2, 3 o 4 da verde, la barrera no está mirando lo que cree mirar. Eso es un rojo aunque
la suite esté en verde.

## 3 · A mano, con dos cuentas

Lo que la suite hace, hecho por una persona. Sirve para convencerse de que los escenarios se parecen
a la realidad y no a sí mismos.

```bash
# Terminal 1
dotnet run --project backend/GestionGastos.Api
```

Con el servidor arriba, y guardando las cookies de cada cuenta por separado:

```bash
# Cuenta A: alta, sesión, y un movimiento
curl -c /tmp/a.txt -X POST localhost:5125/api/cuentas \
  -H 'Content-Type: application/json' \
  -d '{"email":"a@ejemplo.com","contrasena":"una frase larga de prueba"}'
curl -c /tmp/a.txt -X POST localhost:5125/api/sesion \
  -H 'Content-Type: application/json' \
  -d '{"email":"a@ejemplo.com","contrasena":"una frase larga de prueba"}'
curl -b /tmp/a.txt -X POST localhost:5125/api/movimientos \
  -H 'Content-Type: application/json' \
  -d '{"tipo":"gasto","monto":100,"categoriaId":1}'

# Cuenta B: lo mismo, con su propio frasco de cookies
# (repetir los tres comandos con b@ejemplo.com y /tmp/b.txt, monto 200)
```

Lo que hay que ver:

| Comando | Esperado |
|---|---|
| `curl -b /tmp/a.txt localhost:5125/api/movimientos` | Sólo el de 100. Ni rastro del de 200 |
| `curl -b /tmp/b.txt localhost:5125/api/movimientos` | Sólo el de 200 |
| Alta desde A con `"usuarioId": <id de B>` en el cuerpo | El movimiento aparece en el listado de **A**, y el de B no cambia |

Ese último es el que importa: el campo `usuarioId` no existe en el contrato del alta, así que viaja
y se descarta. Que el movimiento caiga en A es lo que dice que el dueño lo decide la sesión y no la
petición.

## Qué NO se puede comprobar todavía

Pedir, modificar o eliminar un movimiento ajeno **por su identificador**. Esos tres endpoints no
existen en este repositorio, junto con el resumen: son FEAT-001b y FEAT-001c. Los cinco criterios
del PRD que dependen de ellos están en la tabla de *Deuda registrada* de la [spec](./spec.md), con
el ticket que los va a cubrir.

## La puerta de cierre

Lo que hay que ver en verde antes de dar la feature por terminada — la de `AGENTS.md`, con la
barrera nueva sumada a las tres que ya estaban:

```bash
dotnet format backend/GestionGastos.slnx --verify-no-changes
dotnet build backend/GestionGastos.slnx -warnaserror
dotnet test backend/GestionGastos.slnx
dotnet test backend/GestionGastos.slnx --settings backend/cobertura.runsettings

./backend/verificar-contrato.sh        # el contrato NO cambió, y esto lo comprueba
./backend/verificar-autorizacion.sh
./backend/verificar-linter.sh
./backend/verificar-aislamiento.sh     # nueva

pnpm --dir frontend lint && pnpm --dir frontend format \
  && pnpm --dir frontend exec tsc --noEmit \
  && pnpm --dir frontend test && pnpm --dir frontend build
```

El frontend se corre aunque esta feature no lo toque: se corre para comprobar exactamente eso.
