# Contrato: las monedas en el resumen

**Feature**: `008-monedas-como-dato` · **Fecha**: 2026-09-03

> **Este contrato no cambia en esta feature.** El documento existe para dejar escrito **por qué no
> cambia**, porque el PRD de la 4a pide cambiarlo y alguien que lea sólo el PRD va a intentarlo.

---

## Lo que ya devuelve `GET /api/resumen`

```jsonc
{
  "desde": "2026-09-01",
  "hasta": "2026-09-30",
  "monedas": [
    {
      "monedaId": 1,
      "monedaCodigo": "ARS",
      "totalIngresado": 450000.00,
      "totalGastado": 312500.50,
      "balance": 137499.50,
      "gastosPorCategoria": [
        { "categoriaId": 1, "categoriaNombre": "Comida", "total": 120000.00 }
      ]
    },
    {
      "monedaId": 2,
      "monedaCodigo": "USD",
      "totalIngresado": 0,
      "totalGastado": 0,
      "balance": 0,
      "gastosPorCategoria": []
    }
  ]
}
```

## Las tres reglas que esta feature conserva

### 1. Una entrada por cada moneda del catálogo, tenga o no movimientos

`monedas` **nunca viene vacío** y su largo es el del catálogo, no el de las monedas con actividad.
En el ejemplo, USD aparece en cero porque está en el catálogo, no porque haya pasado algo con ella.

**El PRD de la 4a pide lo contrario** en su AC-07 y su AC-08. Gana el AC-31 de la feature 006, cuya
razón está escrita: *"y no una respuesta vacía que obligue a quien la muestre a inventar los ceros"*.
Ver *De dónde sale esta spec* y la deuda **D8-04**.

**Consecuencia directa de esta regla, y es la que esta feature verifica**: una moneda agregada al
catálogo aparece en el resumen **sin que nadie toque el código**, porque la lista sale del catálogo.
Si las monedas salieran del agregado, agregar una al catálogo no se notaría hasta que alguien
registrara un movimiento con ella — y RF-032 dejaría de cumplirse por el camino.

### 2. El orden es por identificador, y es parte del contrato

`OrderBy(m => m.Id)`. Una moneda nueva entra **al final**. Sin orden explícito el motor las devuelve
como le conviene y la pantalla las reordenaría sola entre dos pedidos idénticos — el mismo motivo por
el que el desglose desempata por `categoriaId`.

### 3. Nada se suma nunca a través de dos entradas

Cada `ResumenPorMoneda` es un universo cerrado. **No hay conversión, no hay total consolidado y no
los va a haber**: PRD-001 lo excluye de forma explícita. Sumar `balance` entre dos monedas no da un
número aproximado, da un número sin significado.

---

## Lo que NO está en el contrato, y por qué

| Campo ausente | Motivo |
|---|---|
| `simbolo`, `decimales`, `nombre` de la moneda | El resumen todavía no se muestra (deuda D6-01). Cuando el ticket 6 los necesite para formatear, será un cambio de contrato con su propia justificación — no algo que se agregue por si acaso |
| `esPredeterminada` | Al cliente no le sirve: no elige moneda hasta 4b, y cuando elija, cuál viene marcada es una decisión del formulario, no del resumen |
| Un endpoint `GET /api/monedas` | Nadie lo necesita. El resumen ya trae las monedas con las que hay que informar, y el catálogo se administra por fuera de la aplicación. Agregarlo ahora sería una superficie pública sin consumidor |

---

## Verificación

La alineación entre este contrato y `frontend/src/api/tipos.ts` ya la sostiene
`verificar-contrato.sh`, y **esta feature no la modifica**. Que siga en verde al cierre es, en sí
mismo, la prueba de que el contrato no cambió — que es exactamente lo que FR-009 pide.
