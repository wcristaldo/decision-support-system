import { useEffect } from 'react'
import '../styles/ChartHelp.css'

/**
 * Ayuda guiada de los gráficos de tendencia de calidad.
 * - <ChartHelpButton>: botón "?" que se coloca junto al título del gráfico.
 * - <ChartHelpPanel>: panel con la explicación paso a paso; se muestra debajo del título.
 * El texto describe exactamente lo que grafica el sistema (MetricsCalculationService):
 *   tasa de éxito = exitosas / total        cobertura = (exitosas + fallidas) / total
 * Por eso la cobertura (línea verde) nunca queda por debajo de la tasa de éxito (línea gris):
 * la distancia entre ambas es la tasa de fallo y la distancia de la verde al 100 % son las pruebas omitidas.
 */
export function ChartHelpButton({ open, onToggle, controls }) {
  return (
    <button
      type="button"
      className={`chart-help-btn ${open ? 'is-open' : ''}`}
      onClick={onToggle}
      aria-expanded={open}
      aria-controls={controls}
      aria-label="¿Cómo leer este gráfico?"
      title="¿Cómo leer este gráfico?"
    >
      ?
    </button>
  )
}

export function ChartHelpPanel({ id, alcance, onClose }) {
  useEffect(() => {
    const onKey = (e) => { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div className="chart-help-panel" id={id} role="region" aria-label="Cómo leer el gráfico de tendencia de calidad">
      <div className="chart-help-head">
        <strong>Cómo leer este gráfico</strong>
        <button type="button" className="chart-help-close" onClick={onClose} aria-label="Cerrar la ayuda">×</button>
      </div>

      <p className="chart-help-intro">
        Cada punto es una evaluación (un reporte de pruebas cargado), de la más antigua a la más reciente. {alcance}
      </p>

      <p className="chart-help-intro">
        Las pruebas de cada evaluación se reparten en tres grupos: <b>pasaron</b>, <b>fallaron</b> o <b>no se ejecutaron</b>.
        Las dos líneas se calculan con esos grupos:
      </p>

      <ol className="chart-help-steps">
        <li>
          <span className="chart-help-num">1</span>
          <div>
            <b><span className="chart-help-chip chart-help-chip--exito" /> Línea gris, tasa de éxito:</b> solo las pruebas que <b>pasaron</b>.
          </div>
        </li>
        <li>
          <span className="chart-help-num">2</span>
          <div>
            <b><span className="chart-help-chip chart-help-chip--cobertura" /> Línea verde, cobertura:</b> las pruebas que <b>pasaron
            más las que fallaron</b>, es decir, todas las que realmente se ejecutaron.
          </div>
        </li>
      </ol>

      <div className="chart-help-key">
        <b>Por qué se separan o se juntan</b>
        <ul>
          <li>La verde siempre está <b>igual o por encima</b> de la gris, porque suma además las que fallaron.</li>
          <li><b>Una sola línea</b> (se superponen): ninguna prueba falló, todo lo ejecutado pasó.</li>
          <li><b>Separadas por poco:</b> fallaron pocas pruebas. La distancia entre las dos líneas es el porcentaje que falló.</li>
          <li><b>La verde por debajo del 100 %:</b> hubo pruebas que no se ejecutaron (omitidas).</li>
        </ul>
      </div>

      <p className="chart-help-ejemplo">
        <b>Ejemplo con 20 pruebas:</b> 18 pasaron, 1 falló y 1 no se ejecutó. Gris = 18 ÷ 20 = 90 %. Verde = (18 + 1) ÷ 20 = 95 %.
        Las líneas quedan separadas por 5 puntos (la prueba que falló) y la verde queda 5 puntos bajo el 100 % (la que no se ejecutó).
      </p>

      <p className="chart-help-foot">
        Pasá el cursor sobre un punto para ver los valores exactos. La recomendación de despliegue (Desplegar, Revisar o No desplegar)
        sale de comparar estos valores con los umbrales de cada proyecto, con un margen de tolerancia de ±5 %.
      </p>
    </div>
  )
}
