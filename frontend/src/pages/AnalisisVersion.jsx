import { useState, useEffect } from 'react'
import { useParams } from 'react-router-dom'
import api from '../services/api'
import '../styles/AnalisisVersion.css'

function AnalisisVersion() {
  const { id } = useParams()
  const [version, setVersion] = useState(null)
  const [metricas, setMetricas] = useState([])
  const [recomendaciones, setRecomendaciones] = useState([])
  const [decisiones, setDecisiones] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    fetchData()
  }, [id])

  const fetchData = async () => {
    try {
      const [versionRes, metricasRes, recomendacionesRes, decisionesRes] = await Promise.all([
        api.get(`/versiones/${id}`),
        api.get(`/metricas/version/${id}`),
        api.get(`/recomendaciones/version/${id}`),
        api.get(`/decisionesDespliegue/version/${id}`)
      ])

      setVersion(versionRes.data)
      setMetricas(metricasRes.data)
      setRecomendaciones(recomendacionesRes.data)
      setDecisiones(decisionesRes.data)
      setError(null)
    } catch (err) {
      setError('Error al cargar datos')
      console.error(err)
    } finally {
      setLoading(false)
    }
  }

  const handleTakeDecision = async (decision) => {
    try {
      await api.post('/decisionesDespliegue', {
        versionId: parseInt(id),
        decision,
        justificacion: 'Decision taken from dashboard'
      })
      fetchData()
    } catch (err) {
      setError('Error al registrar decisión')
    }
  }

  if (loading) return <div className="loading">Cargando análisis...</div>
  if (!version) return <div className="error-message">Versión no encontrada</div>

  const tasaExito = metricas.find(m => m.nombreMetrica === 'tasa_exito')
  const tasaFallo = metricas.find(m => m.nombreMetrica === 'tasa_fallo')
  const totalPruebas = metricas.find(m => m.nombreMetrica === 'total_pruebas')

  return (
    <div className="analisis-container">
      <h1>Análisis - Versión {version.numeroVersion}</h1>

      {error && <div className="error-message">{error}</div>}

      <section className="metricas-section">
        <h2>Métricas</h2>
        <div className="metricas-grid">
          {tasaExito && (
            <div className={`metrica-card ${tasaExito.valor >= 95 ? 'success' : tasaExito.valor >= 80 ? 'warning' : 'danger'}`}>
              <h4>Tasa de Éxito</h4>
              <p className="valor">{tasaExito.valor.toFixed(2)}%</p>
            </div>
          )}
          {totalPruebas && (
            <div className="metrica-card">
              <h4>Total de Pruebas</h4>
              <p className="valor">{totalPruebas.valor}</p>
            </div>
          )}
          {tasaFallo && (
            <div className={`metrica-card ${tasaFallo.valor > 20 ? 'danger' : 'info'}`}>
              <h4>Tasa de Fallo</h4>
              <p className="valor">{tasaFallo.valor.toFixed(2)}%</p>
            </div>
          )}
        </div>
      </section>

      <section className="recomendaciones-section">
        <h2>Recomendaciones</h2>
        <div className="recomendaciones-list">
          {recomendaciones.map((rec) => (
            <div key={rec.id} className={`recomendacion-card ${rec.tipoRecomendacion.toLowerCase()}`}>
              <div className="recom-header">
                <h4>{rec.tipoRecomendacion}</h4>
                <span className="confianza">Confianza: {rec.confianza}%</span>
              </div>
              <p>{rec.descripcion}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="decisiones-section">
        <h2>Decisiones de Despliegue</h2>
        <div className="decision-buttons">
          <button className="btn-success" onClick={() => handleTakeDecision('DESPLEGAR')}>
            Desplegar
          </button>
          <button className="btn-warning" onClick={() => handleTakeDecision('DESPLEGAR_CON_MONITOREO')}>
            Desplegar con Monitoreo
          </button>
          <button className="btn-danger" onClick={() => handleTakeDecision('NO_DESPLEGAR')}>
            No Desplegar
          </button>
        </div>

        <div className="decisiones-historial">
          {decisiones.length > 0 && (
            <>
              <h3>Historial de Decisiones</h3>
              {decisiones.map((dec) => (
                <div key={dec.id} className="decision-item">
                  <span className={`decision-badge ${dec.decision.toLowerCase()}`}>{dec.decision}</span>
                  <p>{dec.justificacion}</p>
                  <small>{new Date(dec.fechaDecision).toLocaleString()}</small>
                </div>
              ))}
            </>
          )}
        </div>
      </section>
    </div>
  )
}

export default AnalisisVersion
