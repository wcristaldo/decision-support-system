import { useState, useEffect, useMemo } from 'react'
import { Link } from 'react-router-dom'
import api from '../services/api'
import NotificationModal from '../components/NotificationModal'
import '../styles/AnalisisMetricas.css'

// ── Helpers ──────────────────────────────────────────────────────────────────

function badgeClass(rec) {
  if (!rec) return 'am-badge--warn'
  const r = rec.toUpperCase()
  if (r === 'DESPLEGAR')    return 'am-badge--ok'
  if (r === 'NO_DESPLEGAR') return 'am-badge--danger'
  return 'am-badge--warn'
}

function badgeLabel(rec) {
  if (!rec) return '-'
  const r = rec.toUpperCase()
  if (r === 'DESPLEGAR')    return 'Desplegar'
  if (r === 'NO_DESPLEGAR') return 'No desplegar'
  return 'Revisar'
}

function metricColorClass(val, tipo) {
  if (val === null || val === undefined) return 'am-metric-mid'
  const n = Number(val)
  if (tipo === 'mayor') {
    if (n >= 90)  return 'am-metric-ok'
    if (n >= 75)  return 'am-metric-warn'
    return 'am-metric-danger'
  }
  // menor (tiempo_ejecucion): ≤120s ok, ≤126s warn, >126s danger
  if (n <= 120) return 'am-metric-ok'
  if (n <= 126) return 'am-metric-warn'
  return 'am-metric-danger'
}

function fmt(val, decimales = 1) {
  if (val === null || val === undefined) return '-'
  return Number(val).toFixed(decimales)
}

function fmtFecha(dateStr) {
  if (!dateStr) return '-'
  const d = new Date(dateStr)
  return d.toLocaleDateString('es-PY', { day: '2-digit', month: 'short', year: 'numeric' })
}

// ── Componente ────────────────────────────────────────────────────────────────

const FILTROS = ['Todos', 'DESPLEGAR', 'REVISAR', 'NO_DESPLEGAR']

function AnalisisMetricas() {
  const [historial,    setHistorial]    = useState([])
  const [loading,      setLoading]      = useState(true)
  const [notification, setNotification] = useState(null)
  const [filtro,       setFiltro]       = useState('Todos')

  const showNotification = (type, title, message) => {
    setNotification({ type, title, message })
  }
  const closeNotification = () => {
    setNotification(null)
  }

  useEffect(() => {
    api.get('/analisis/historial')
      .then(res => setHistorial(res.data))
      .catch(() => showNotification('error', 'Error', 'No se pudo cargar el historial de análisis.'))
      .finally(() => setLoading(false))
  }, [])

  const stats = useMemo(() => ({
    total:       historial.length,
    desplegar:   historial.filter(h => h.recomendacion?.toUpperCase() === 'DESPLEGAR').length,
    revisar:     historial.filter(h => h.recomendacion?.toUpperCase() === 'REVISAR').length,
    noDesplegar: historial.filter(h => h.recomendacion?.toUpperCase() === 'NO_DESPLEGAR').length,
  }), [historial])

  const filas = useMemo(() => {
    if (filtro === 'Todos') return historial
    return historial.filter(h => h.recomendacion?.toUpperCase() === filtro)
  }, [historial, filtro])

  return (
    <div className="am-page">

      {/* ── Header ── */}
      <div className="am-header">
        <div className="am-header-inner">
          <h1 className="am-title">Análisis y Métricas</h1>
          <p className="am-subtitle">
            Historial de versiones evaluadas por el motor de recomendación - Roshka S.A.
          </p>
        </div>
      </div>

      {/* ── Body ── */}
      <div className="am-body">

        {/* ── Cards de resumen ── */}
        <div className="am-stats">
          <div className="am-stat">
            <div className="am-stat-icon am-stat-icon--total">📊</div>
            <div>
              <div className="am-stat-val">{loading ? '-' : stats.total}</div>
              <div className="am-stat-lbl">Total analizados</div>
            </div>
          </div>
          <div className="am-stat">
            <div className="am-stat-icon am-stat-icon--ok">✓</div>
            <div>
              <div className="am-stat-val">{loading ? '-' : stats.desplegar}</div>
              <div className="am-stat-lbl">Aptos para desplegar</div>
            </div>
          </div>
          <div className="am-stat">
            <div className="am-stat-icon am-stat-icon--warn">⚠</div>
            <div>
              <div className="am-stat-val">{loading ? '-' : stats.revisar}</div>
              <div className="am-stat-lbl">Requieren revisión</div>
            </div>
          </div>
          <div className="am-stat">
            <div className="am-stat-icon am-stat-icon--danger">✕</div>
            <div>
              <div className="am-stat-val">{loading ? '-' : stats.noDesplegar}</div>
              <div className="am-stat-lbl">No aptos</div>
            </div>
          </div>
        </div>

        {/* ── Tabla historial ── */}
        <div className="am-table-card">
          <div className="am-table-head">
            <h2 className="am-table-head-title">Historial de evaluaciones</h2>
            <div className="am-filter-group">
              {FILTROS.map(f => {
                const isActive = filtro === f
                let cls = 'am-filter-btn'
                if (isActive) {
                  if (f === 'DESPLEGAR')    cls += ' active-ok'
                  else if (f === 'REVISAR') cls += ' active-warn'
                  else if (f === 'NO_DESPLEGAR') cls += ' active-danger'
                  else cls += ' active'
                }
                return (
                  <button key={f} className={cls} onClick={() => setFiltro(f)}>
                    {f === 'Todos'         ? 'Todos'
                     : f === 'DESPLEGAR'  ? 'Desplegar'
                     : f === 'REVISAR'    ? 'Revisar'
                     : 'No desplegar'}
                  </button>
                )
              })}
            </div>
          </div>

          {loading ? (
            <div className="am-loading" style={{ padding: '3rem 1.5rem' }}>
              <span className="am-spinner" />
              Cargando historial…
            </div>
          ) : filas.length === 0 ? (
            <div className="am-empty">
              <div className="am-empty-icon">📋</div>
              <p className="am-empty-title">
                {filtro === 'Todos' ? 'Aún no hay versiones analizadas' : `Sin resultados para "${badgeLabel(filtro)}"`}
              </p>
              <p className="am-empty-sub">
                {filtro === 'Todos'
                  ? 'Cargá un archivo JSON de resultados para que el motor genere una evaluación.'
                  : 'Probá con otro filtro o cargá nuevos resultados.'}
              </p>
            </div>
          ) : (
            <div className="am-table-wrap">
              <table className="am-table">
                <colgroup>
                  <col className="am-col-proyecto"  />
                  <col className="am-col-version"   />
                  <col className="am-col-fecha"     />
                  <col className="am-col-exito"     />
                  <col className="am-col-cobertura" />
                  <col className="am-col-tiempo"    />
                  <col className="am-col-pruebas"   />
                  <col className="am-col-rec"       />
                  <col className="am-col-accion"    />
                </colgroup>
                <thead>
                  <tr>
                    <th>Proyecto</th>
                    <th>Versión</th>
                    <th>Fecha</th>
                    <th>% Éxito</th>
                    <th>Cobertura</th>
                    <th>Tiempo (s)</th>
                    <th>Pruebas</th>
                    <th>Recomendación</th>
                    <th>Detalle</th>
                  </tr>
                </thead>
                <tbody>
                  {filas.map((h, idx) => (
                    <tr key={`${h.versionId}-${h.recomendacionId}-${idx}`}>
                      <td>
                        <div className="am-td-proyecto">{h.proyectoNombre}</div>
                        {h.proyectoTipo && <div className="am-td-tipo">{h.proyectoTipo}</div>}
                      </td>
                      <td>
                        <span className="am-td-version">v{h.versionNumero}</span>
                      </td>
                      <td className="am-td-fecha">{fmtFecha(h.fechaEvaluacion)}</td>
                      <td>
                        <span className={`am-td-metric ${metricColorClass(h.tasaExito, 'mayor')}`}>
                          {fmt(h.tasaExito)}%
                        </span>
                      </td>
                      <td>
                        <span className={`am-td-metric ${metricColorClass(h.cobertura, 'mayor')}`}>
                          {fmt(h.cobertura)}%
                        </span>
                      </td>
                      <td>
                        <span className={`am-td-metric ${metricColorClass(h.tiempoEjecucion, 'menor')}`}>
                          {fmt(h.tiempoEjecucion, 2)}
                        </span>
                      </td>
                      <td className="am-td-metric am-metric-mid">
                        {h.totalPruebas != null ? Math.round(h.totalPruebas) : '-'}
                      </td>
                      <td>
                        <span className={`am-badge ${badgeClass(h.recomendacion)}`}>
                          <span className="am-badge-dot" />
                          {badgeLabel(h.recomendacion)}
                        </span>
                      </td>
                      <td>
                        <Link
                          to={`/versiones/${h.versionId}/analisis`}
                          className="am-btn-ver"
                        >
                          Ver →
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

      </div>

      <NotificationModal
        isOpen={!!notification}
        type={notification?.type}
        title={notification?.title}
        message={notification?.message}
        onClose={closeNotification}
      />
    </div>
  )
}

export default AnalisisMetricas
