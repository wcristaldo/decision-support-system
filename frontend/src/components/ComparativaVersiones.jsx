import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../services/api'
import { normalizeRec, TIPO_RECOMENDACION, formatMetricValue, metricLabel } from '../pages/AnalisisVersion'

// RF10: comparación lado a lado de 2 o más versiones del mismo proyecto —
// reutiliza exactamente la misma lógica de semáforo y formateo de métricas
// que AnalisisVersion.jsx (misma prioridad NO_DESPLEGAR > REVISAR >
// DESPLEGAR) para que ambas pantallas nunca muestren un veredicto distinto
// para la misma versión.

function calcularSemaforo(recomendaciones) {
  if (!recomendaciones?.length) return null
  const tipos = recomendaciones.map(r => normalizeRec(r.tipoRecomendacion || r.tipo || ''))
  if (tipos.includes('NO_DESPLEGAR')) return 'NO_DESPLEGAR'
  if (tipos.includes('REVISAR'))      return 'REVISAR'
  if (tipos.includes('DESPLEGAR'))    return 'DESPLEGAR'
  return null
}

export default function ComparativaVersiones({ versiones, onClose }) {
  const navigate = useNavigate()
  const [datos, setDatos] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    let cancelado = false
    async function cargar() {
      setLoading(true)
      setError(null)
      try {
        const resultados = await Promise.all(
          versiones.map(async (v) => {
            const [mRes, rRes] = await Promise.all([
              api.get(`/metricas/version/${v.id}`),
              api.get(`/recomendaciones/version/${v.id}`),
            ])
            return {
              version: v,
              metricas: mRes.data,
              semaforoKey: calcularSemaforo(rRes.data),
            }
          })
        )
        if (!cancelado) setDatos(resultados)
      } catch {
        if (!cancelado) setError('No se pudieron cargar las métricas de una o más versiones.')
      } finally {
        if (!cancelado) setLoading(false)
      }
    }
    cargar()
    return () => { cancelado = true }
  }, [versiones])

  // Unión de todos los nombres de métrica presentes en cualquiera de las
  // versiones comparadas, para que la tabla tenga una fila por métrica aunque
  // alguna versión no tenga todavía un resultado cargado con esa métrica.
  const nombresMetricas = datos
    ? [...new Set(datos.flatMap(d => d.metricas.map(m => (m.nombreMetrica || m.nombre || '').toLowerCase().trim())))]
    : []

  const valorMetrica = (d, nombre) => d.metricas.find(m => (m.nombreMetrica || m.nombre || '').toLowerCase().trim() === nombre)

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-box modal-box-md" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 800 }}>
        <div className="modal-header">
          <h2 className="modal-title">Comparar versiones</h2>
          <button className="modal-close" onClick={onClose} aria-label="Cerrar">×</button>
        </div>

        <div className="modal-body">
          {loading ? (
            <div className="dp-loading"><span className="dp-spinner" /> Cargando comparación…</div>
          ) : error ? (
            <div className="proy-error"><span className="proy-error-icon">!</span>{error}</div>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table className="proy-table">
                <thead>
                  <tr>
                    <th>Criterio</th>
                    {datos.map(d => <th key={d.version.id}>v{d.version.numeroVersion}</th>)}
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td><strong>Recomendación</strong></td>
                    {datos.map(d => {
                      const tipo = d.semaforoKey ? TIPO_RECOMENDACION[d.semaforoKey] : null
                      return (
                        <td key={d.version.id}>
                          {tipo ? (
                            <span className={`av-semaforo ${tipo.cls}`} style={{ display: 'inline-block', padding: '4px 10px', fontSize: '0.85rem' }}>
                              {tipo.icon} {tipo.label}
                            </span>
                          ) : (
                            <span className="av-semaforo sem-sin-datos" style={{ display: 'inline-block', padding: '4px 10px', fontSize: '0.85rem' }}>
                              Sin datos
                            </span>
                          )}
                        </td>
                      )
                    })}
                  </tr>
                  {nombresMetricas.map(nombre => (
                    <tr key={nombre}>
                      <td>{metricLabel(nombre)}</td>
                      {datos.map(d => {
                        const m = valorMetrica(d, nombre)
                        return <td key={d.version.id}>{m ? formatMetricValue(m) : '—'}</td>
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
              <div style={{ display: 'flex', gap: 8, marginTop: 16, flexWrap: 'wrap' }}>
                {datos.map(d => (
                  <button key={d.version.id} className="btn-analizar"
                    onClick={() => navigate(`/versiones/${d.version.id}/analisis`)}>
                    Ver detalle de v{d.version.numeroVersion}
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>

        <div className="modal-footer">
          <button type="button" className="btn-cancel" onClick={onClose}>Cerrar</button>
        </div>
      </div>
    </div>
  )
}
