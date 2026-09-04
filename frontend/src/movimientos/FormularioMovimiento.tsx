import type { Categoria, Moneda, NuevoMovimiento } from '../api/tipos';
import { CamposDelMovimiento } from './CamposDelMovimiento';

export interface PropsFormularioMovimiento {
  categorias: Categoria[];
  /**
   * El catálogo de monedas que alimenta el selector (FR-005).
   *
   * **Baja por props y no se pide acá**, igual que el de categorías y por la misma razón: vive en
   * la raíz para que esta pantalla y el acotado del listado miren la misma lista, y para que se
   * pida una sola vez (`PRD:NFR-02`, AC-12).
   */
  monedas: Moneda[];
  /** El día de hoy en formato `YYYY-MM-DD`. Entra por prop para que el test sea determinista. */
  hoy: string;
  onGuardar: (movimiento: NuevoMovimiento) => void | Promise<void>;
}

/**
 * El formulario de alta (FR-001, FR-002, FR-003, FR-005).
 *
 * **Los campos y las reglas viven en `CamposDelMovimiento`** desde la feature 009, compartidos con
 * la ventana de edición (D-08). Acá queda lo que es propio del alta: que al guardar se limpie todo
 * y el foco vuelva al primer campo, para poder encadenar cargas sin tocar el mouse (FR-014).
 */
export function FormularioMovimiento({
  categorias,
  monedas,
  hoy,
  onGuardar,
}: PropsFormularioMovimiento) {
  return (
    <CamposDelMovimiento
      categorias={categorias}
      monedas={monedas}
      hoy={hoy}
      etiquetaDeEnvio="Registrar"
      mensajeDeFallo="No se pudo registrar el movimiento. Volvé a intentarlo."
      reiniciarAlGuardar
      onGuardar={onGuardar}
    />
  );
}
