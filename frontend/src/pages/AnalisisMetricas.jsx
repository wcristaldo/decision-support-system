import { useState, useEffect, useMemo } from 'react'
import { Link } from 'react-router-dom'
import {
  ResponsiveContainer, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend,
} from 'recharts'
import api from '../services/api'
import NotificationModal from '../components/NotificationModal'
import ResizableTh from '../components/ResizableTh'
import { useResizableColumns } from '../hooks/useResizableColumns'
import { fmtFechaCorta, fmtFechaCompleta } from '../utils/fecha'
import '../styles/AnalisisMetricas.css'

// ── Helpers ──────────────────────────────────────────────────────────────────

const AM_COLUMNS = [
  { key: 'proyecto',    defaultWidth: 162, minWidth: 90 },
  { key: 'version',     defaultWidth: 81,  minWidth: 60 },
  { key: 'fecha',       defaultWidth: 130, minWidth: 90 },
  { key: 'responsable', defaultWidth: 140, minWidth: 90 },
  { key: 'exito',       defaultWidth: 81,  minWidth: 60 },
  { key: 'cobertura',   defaultWidth: 90,  minWidth: 60 },
  { key: 'tiempo',      defaultWidth: 81,  minWidth: 60 },
  { key: 'pruebas',     defaultWidth: 81,  minWidth: 60 },
  { key: 'rec',         defaultWidth: 150, minWidth: 100 },
  { key: 'accion',      defaultWidth: 90,  minWidth: 70 },
]

/** Normaliza valores de la DB a las constantes usadas en el frontend */
function normalizeRec(t) {
  if (!t) return ''
  const u = t.toUpperCase()
  if (u === 'DESPLEGAR_CON_OBSERVACIONES') return 'REVISAR'
  return u
}

function badgeClass(rec) {
  if (!rec) return 'am-badge--warn'
  const r = normalizeRec(rec)
  if (r === 'DESPLEGAR')    return 'am-badge--ok'
  if (r === 'NO_DESPLEGAR') return 'am-badge--danger'
  return 'am-badge--warn'
}

function badgeLabel(rec) {
  if (!rec) return '-'
  const r = normalizeRec(rec)
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

// ── Componente ────────────────────────────────────────────────────────────────

const FILTROS = ['Todos', 'DESPLEGAR', 'REVISAR', 'NO_DESPLEGAR']

function AnalisisMetricas() {
  const [historial,    setHistorial]    = useState([])
  const [loading,      setLoading]      = useState(true)
  const [notification, setNotification] = useState(null)
  const [filtro,       setFiltro]       = useState('Todos')
  const [usuarioFiltro, setUsuarioFiltro] = useState('')
  const [fechaDesde,    setFechaDesde]    = useState('')
  const [fechaHasta,    setFechaHasta]    = useState('')
  const { widths, startResize } = useResizableColumns('analisis-historial', AM_COLUMNS)
  const [exportando, setExportando] = useState(null) // 'pdf' | 'xlsx' | 'csv' | null

  const showNotification = (type, title, message) => {
    setNotification({ type, title, message })
  }
  const closeNotification = () => {
    setNotification(null)
  }

  const handleExport = async (tipo) => {
    setExportando(tipo)
    try {
      const url = tipo === 'pdf' ? '/analisis/historial/exportar-pdf' : `/analisis/historial/exportar?formato=${tipo}`
      const res = await api.get(url, { responseType: 'blob' })
      const blobUrl = window.URL.createObjectURL(res.data)
      const a = document.createElement('a')
      const ext = tipo === 'pdf' ? 'pdf' : tipo === 'xlsx' ? 'xlsx' : 'csv'
      a.href = blobUrl
      a.download = `roshka-dss-analisis-${new Date().toISOString().slice(0, 10)}.${ext}`
      document.body.appendChild(a)
      a.click()
      a.remove()
      window.URL.revokeObjectURL(blobUrl)
    } catch (err) {
      let msg = 'No se pudo generar el archivo. Intentá de nuevo.'
      if (err.response?.data instanceof Blob) {
        try {
          const text = await err.response.data.text()
          msg = JSON.parse(text)?.message || msg
        } catch { /* blob no era JSON, se usa el mensaje genérico */ }
      }
      showNotification('error', 'No disponible', msg)
    } finally {
      setExportando(null)
    }
  }

  useEffect(() => {
    api.get('/analisis/historial')
      .then(res => setHistorial(res.data))
      .catch(() => showNotification('error', 'Error', 'No se pudo cargar el historial de análisis.'))
      .finally(() => setLoading(false))
  }, [])

  const stats = useMemo(() => ({
    total:       historial.length,
    desplegar:   historial.filter(h => normalizeRec(h.recomendacion) === 'DESPLEGAR').length,
    revisar:     historial.filter(h => normalizeRec(h.recomendacion) === 'REVISAR').length,
    noDesplegar: historial.filter(h => normalizeRec(h.recomendacion) === 'NO_DESPLEGAR').length,
  }), [historial])

  // RF10: tendencia histórica de indicadores de calidad
  const tendencia = useMemo(() => {
    return [...historial]
      .filter(h => h.fechaCarga)
      .sort((a, b) => new Date(a.fechaCarga) - new Date(b.fechaCarga))
      .map(h => ({
        fecha:     fmtFechaCorta(h.fechaCarga),
        tasaExito: h.tasaExito != null ? Number(h.tasaExito) : null,
        cobertura: h.cobertura != null ? Number(h.cobertura) : null,
      }))
  }, [historial])

  // RF11: usuarios responsables disponibles para el filtro (derivados de los datos)
  const usuariosDisponibles = useMemo(() => (
    [...new Set(historial.map(h => h.usuarioCargaNombre).filter(Boolean))].sort()
  ), [historial])

  // RF11: historial filtrable por estado, fecha y usuario responsable
  const filas = useMemo(() => {
    return historial.filter(h => {
      if (filtro !== 'Todos' && normalizeRec(h.recomendacion) !== filtro) return false
      if (usuarioFiltro && h.usuarioCargaNombre !== usuarioFiltro) return false
      if (fechaDesde && h.fechaCarga && h.fechaCarga.slice(0, 10) < fechaDesde) return false
      if (fechaHasta && h.fechaCarga && h.fechaCarga.slice(0, 10) > fechaHasta) return false
      return true
    })
  }, [historial, filtro, usuarioFiltro, fechaDesde, fechaHasta])

  const hayFiltrosActivos = filtro !== 'Todos' || usuarioFiltro || fechaDesde || fechaHasta
  const limpiarFiltros = () => {
    setFiltro('Todos'); setUsuarioFiltro(''); setFechaDesde(''); setFechaHasta('')
  }

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

        {/* ── Tendencia histórica (RF10) ── */}
        {tendencia.length > 1 && (
          <div className="am-table-card am-chart-card">
            <div className="am-table-head">
              <h2 className="am-table-head-title">Tendencia histórica</h2>
            </div>
            <div className="am-chart-wrap">
              <ResponsiveContainer width="100%" height={260}>
                <LineChart data={tendencia} margin={{ top: 10, right: 24, left: 0, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#e9ecf1" />
                  <XAxis dataKey="fecha" tick={{ fontSize: 11, fill: '#4a647a' }} axisLine={{ stroke: '#d7e0e7' }} tickLine={false} />
                  <YAxis domain={[0, 100]} unit="%" tick={{ fontSize: 11, fill: '#4a647a' }} axisLine={{ stroke: '#d7e0e7' }} tickLine={false} width={42} />
                  <Tooltip
                    contentStyle={{ borderRadius: 10, border: '1px solid #e9ecf1', fontSize: '0.8rem' }}
                    formatter={(value) => [`${value}%`, undefined]}
                  />
                  <Legend wrapperStyle={{ fontSize: '0.8rem' }} />
                  <Line type="monotone" dataKey="tasaExito" name="Tasa de éxito" stroke="#7e9ab2" strokeWidth={2.5} dot={{ r: 3 }} connectNulls />
                  <Line type="monotone" dataKey="cobertura" name="Cobertura"    stroke="#1a7a4e" strokeWidth={2.5} dot={{ r: 3 }} connectNulls />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </div>
        )}

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

          {/* RF11: filtros adicionales por fecha y usuario responsable */}
          <div className="am-filters-row">
            <select
              className="am-filter-select"
              value={usuarioFiltro}
              onChange={(e) => setUsuarioFiltro(e.target.value)}
            >
              <option value="">Todos los responsables</option>
              {usuariosDisponibles.map(u => <option key={u} value={u}>{u}</option>)}
            </select>

            <label className="am-filter-date">
              Desde
              <input type="date" value={fechaDesde} onChange={(e) => setFechaDesde(e.target.value)} className="am-filter-select" />
            </label>
            <label className="am-filter-date">
              Hasta
              <input type="date" value={fechaHasta} onChange={(e) => setFechaHasta(e.target.value)} className="am-filter-select" />
            </label>

            {hayFiltrosActivos && (
              <button className="am-filter-clear" onClick={limpiarFiltros}>✕ Limpiar filtros</button>
            )}

            <div className="am-export-group">
              <button className="am-export-btn" disabled={!!exportando} onClick={() => handleExport('pdf')}>
                {exportando === 'pdf' ? <span className="am-spinner am-spinner-sm" /> : '📄'} PDF
              </button>
              <button className="am-export-btn" disabled={!!exportando} onClick={() => handleExport('xlsx')}>
                {exportando === 'xlsx' ? <span className="am-spinner am-spinner-sm" /> : '📊'} Excel
              </button>
              <button className="am-export-btn" disabled={!!exportando} onClick={() => handleExport('csv')}>
                {exportando === 'csv' ? <span className="am-spinner am-spinner-sm" /> : '📋'} CSV
              </button>
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
            <div className="am-table-wrap dss-table-wrap">
              <table className="am-table dss-resizable">
                <colgroup>
                  <col style={{ width: widths.proyecto }}  />
                  <col style={{ width: widths.version }}   />
                  <col style={{ width: widths.fecha }}     />
                  <col style={{ width: widths.responsable }} />
                  <col style={{ width: widths.exito }}     />
                  <col style={{ width: widths.cobertura }} />
                  <col style={{ width: widths.tiempo }}    />
                  <col style={{ width: widths.pruebas }}   />
                  <col style={{ width: widths.rec }}       />
                  <col style={{ width: widths.accion }}    />
                </colgroup>
                <thead>
                  <tr>
                    <ResizableTh onResizeStart={startResize('proyecto', 90)}>Proyecto</ResizableTh>
                    <ResizableTh onResizeStart={startResize('version', 60)}>Versión</ResizableTh>
                    <ResizableTh onResizeStart={startResize('fecha', 90)}>Fecha</ResizableTh>
                    <ResizableTh onResizeStart={startResize('responsable', 90)}>Responsable</ResizableTh>
                    <ResizableTh onResizeStart={startResize('exito', 60)}>% Éxito</ResizableTh>
                    <ResizableTh onResizeStart={startResize('cobertura', 60)}>Cobertura</ResizableTh>
                    <ResizableTh onResizeStart={startResize('tiempo', 60)}>Tiempo (s)</ResizableTh>
                    <ResizableTh onResizeStart={startResize('pruebas', 60)}>Pruebas</ResizableTh>
                    <ResizableTh onResizeStart={startResize('rec', 100)}>Recomendación</ResizableTh>
                    <ResizableTh onResizeStart={startResize('accion', 70)}>Detalle</ResizableTh>
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
                      <td className="am-td-fecha">{fmtFechaCompleta(h.fechaCarga)}</td>
                      <td className="am-td-fecha">{h.usuarioCargaNombre || '—'}</td>
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
                          to={`/versiones/${h.versionId}/analisis?resultado=${h.resultadoId}`}
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
