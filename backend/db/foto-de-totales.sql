-- La foto de totales: lo que SC-003 compara antes y despues de migrar.
--
-- Reproduce con SQL lo que `CalculoDelResumen` arma en memoria, para cada cuenta y cada periodo
-- (mes calendario) sobre el que hay algo que informar: total ingresado, total gastado, balance y
-- desglose por categoria (solo gastos, RF-19).
--
-- **Agrupa por nombre y tipo de categoria, NO por su identificador.** Es deliberado: la migracion
-- cambia los identificadores a proposito —cada cuenta recibe copias nuevas— y una foto por id
-- daria distinto por construccion sin que ningun numero se haya movido. Lo que FR-012 promete que
-- no cambia es la plata que suma cada categoria, y eso se sigue por su nombre y su tipo.
--
-- Tampoco filtra por `categoria.activa`: una categoria dada de baja sigue clasificando los
-- movimientos que ya la usaban y sigue sumando (es la barrera del desglose, verificar-desglose.sh).
--
-- Uso:
--   mysql -h127.0.0.1 -u<usuario> -p -N -B gestiongastos < backend/db/foto-de-totales.sql > antes.txt
--   # ... aplicar la migracion ...
--   mysql -h127.0.0.1 -u<usuario> -p -N -B gestiongastos < backend/db/foto-de-totales.sql > despues.txt
--   diff antes.txt despues.txt   # los bloques TOTALES y DESGLOSE tienen que salir identicos
--
-- El bloque 3 SI cambia, y tiene que cambiar: es la comprobacion de que la migracion hizo su
-- trabajo. Antes de migrar da las filas compartidas que quedaban; despues tiene que dar cero.

-- 1 · Totales por cuenta, periodo y moneda.
SELECT
    'TOTALES'                                                    AS bloque,
    m.usuario_id                                                 AS cuenta,
    DATE_FORMAT(m.fecha, '%Y-%m')                                AS periodo,
    mo.codigo                                                    AS moneda,
    SUM(CASE WHEN m.tipo = 1 THEN m.monto ELSE 0 END)            AS ingresado,
    SUM(CASE WHEN m.tipo = 0 THEN m.monto ELSE 0 END)            AS gastado,
    SUM(CASE WHEN m.tipo = 1 THEN m.monto ELSE -m.monto END)     AS balance
FROM movimiento m
JOIN moneda mo ON mo.id = m.moneda_id
GROUP BY m.usuario_id, DATE_FORMAT(m.fecha, '%Y-%m'), mo.codigo
ORDER BY cuenta, periodo, moneda;

-- 2 · Desglose de gastos por categoria, dentro de cada cuenta, periodo y moneda.
SELECT
    'DESGLOSE'                                                   AS bloque,
    m.usuario_id                                                 AS cuenta,
    DATE_FORMAT(m.fecha, '%Y-%m')                                AS periodo,
    mo.codigo                                                    AS moneda,
    c.nombre                                                     AS categoria,
    c.tipo                                                       AS tipo_categoria,
    SUM(m.monto)                                                 AS total
FROM movimiento m
JOIN moneda mo    ON mo.id = m.moneda_id
JOIN categoria c  ON c.id  = m.categoria_id
WHERE m.tipo = 0
GROUP BY m.usuario_id, DATE_FORMAT(m.fecha, '%Y-%m'), mo.codigo, c.nombre, c.tipo
ORDER BY cuenta, periodo, moneda, categoria, tipo_categoria;

-- 3 · Las dos comprobaciones que la migracion tiene que dejar en cero (FR-014, SC-004).
--     Este bloque NO es parte de la comparacion: antes de migrar da distinto de cero a proposito.
SELECT 'SIN_DUENO' AS bloque, COUNT(*) AS filas FROM categoria WHERE usuario_id IS NULL;

SELECT 'FUERA_DE_AMBITO' AS bloque, COUNT(*) AS filas
FROM movimiento m JOIN categoria c ON c.id = m.categoria_id
WHERE c.usuario_id <> m.usuario_id OR c.usuario_id IS NULL;
