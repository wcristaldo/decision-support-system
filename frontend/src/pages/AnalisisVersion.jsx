import { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate, useSearchParams } from 'react-router-dom'
import api from '../services/api'
import NotificationModal from '../components/NotificationModal'
import '../styles/AnalisisVersion.css'

// ── Helpers ───────────────────────────────────────────────────────────────────

/** Normaliza valores de la DB a las constantes del frontend */
function normalizeRec(t) {
  if (!t) return ''
  const u = t.toUpperCase()
  if (u === 'DESPLEGAR_CON_OBSERVACIONES') return 'REVISAR'
  return u
}

// ── Constantes ────────────────────────────────────────────────────────────────

const TIPO_RECOMENDACION = {
  DESPLEGAR: {
    label:    'Apto para despliegue',
    sublabel: 'Las métricas cumplen los umbrales de calidad establecidos.',
    cls:      'sem-desplegar',
    iconCls:  'sem-icon-ok',
    icon:     '✓',
  },
  REVISAR: {
    label:    'Requiere revisión',
    sublabel: 'Algunas métricas están por debajo de los umbrales esperados.',
    cls:      'sem-revisar',
    iconCls:  'sem-icon-warn',
    icon:     '⚠',
  },
  NO_DESPLEGAR: {
    label:    'No apto para despliegue',
    sublabel: 'Las métricas no alcanzan los umbrales mínimos de calidad.',
    cls:      'sem-no-desplegar',
    iconCls:  'sem-icon-no',
    icon:     '✕',
  },
}

const DECISION_MAP = {
  aprobado:   { text: 'Aprobado',    cls: 'dec-aprobado' },
  rechazado:  { text: 'Rechazado',   cls: 'dec-rechazado' },
  postergado: { text: 'Postergado',  cls: 'dec-rechazado' },
}

const MIN_LARGO_JUSTIFICACION_OVERRIDE = 20

/** Misma regla que el backend (DecisionesDespliegueController.CalcularEsOverride):
 *  aprobar algo "no apto" o rechazar algo "apto" contradice la recomendación. */
function esOverrideCliente(decisionOpt, tipoRecomendacionNormalizado) {
  if (decisionOpt === 'Aprobado' && tipoRecomendacionNormalizado === 'NO_DESPLEGAR') return true
  if (decisionOpt === 'Rechazado' && tipoRecomendacionNormalizado === 'DESPLEGAR') return true
  return false
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function getRawValue(m) {
  const v = m.valorMetrica ?? m.valor
  return parseFloat(v)
}

function formatMetricValue(m) {
  const v = getRawValue(m)
  const unit = (m.unidad || '').toLowerCase()
  if (isNaN(v)) return String(m.valorMetrica ?? m.valor ?? '-')
  if (unit === '%' || unit === 'porcentaje' || unit === 'percent') {
    return `${v.toFixed(2)}%`
  }
  if (Number.isInteger(v)) return m.unidad ? `${v} ${m.unidad}` : `${v}`
  return m.unidad ? `${v.toFixed(3)} ${m.unidad}` : `${v.toFixed(3)}`
}

const VEREDICTO_A_CLASE = {
  cumple:    'mc-ok',
  revisar:   'mc-warn',
  no_cumple: 'mc-danger',
}

/**
 * Colorea cada métrica según el veredicto REAL que aplicó el motor de
 * recomendación (tabla evaluacion_regla) contra el umbral configurado —
 * no una escala fija propia del frontend. Las métricas que no son criterio
 * de evaluación (conteos como "pruebas exitosas", "total de pruebas")
 * quedan siempre neutrales: no tiene sentido pintarlas de rojo/verde.
 */
function metricColorClass(m, reglasPorCriterio) {
  const nombre = (m.nombreMetrica || m.nombre || '').toLowerCase().trim()
  const veredicto = reglasPorCriterio?.[nombre]
  return VEREDICTO_A_CLASE[veredicto] || 'mc-neutral'
}

const METRIC_LABELS = {
  tasa_exito:       'Tasa de éxito',
  tasa_fallo:       'Tasa de fallo',
  total_pruebas:    'Total de pruebas',
  pruebas_exitosas: 'Pruebas exitosas',
  pruebas_fallidas: 'Pruebas fallidas',
  pruebas_omitidas: 'Pruebas omitidas',
  cobertura:        'Cobertura',
  cobertura_codigo: 'Cobertura de código',
  tiempo_ejecucion: 'Tiempo de ejecución',
  tiempo_promedio:  'Tiempo promedio',
  duracion_total:   'Duración total',
}

function metricLabel(nombre) {
  const k = (nombre || '').toLowerCase().replace(/ /g, '_')
  return METRIC_LABELS[k] || nombre
}

// ── Validación decisión ───────────────────────────────────────────────────────

function validateDecision(fields, esOverride) {
  const errors = {}
  if (!fields.decision) {
    errors.decision = 'Seleccioná una decisión.'
  }
  const comentario = fields.comentario.trim()
  if (!comentario) {
    errors.comentario = 'La justificación es obligatoria.'
  } else if (comentario.length > 1000) {
    errors.comentario = 'No puede superar los 1000 caracteres.'
  } else if (esOverride && comentario.length < MIN_LARGO_JUSTIFICACION_OVERRIDE) {
    errors.comentario = `Esta decisión contradice la recomendación del sistema: la justificación debe tener al menos ${MIN_LARGO_JUSTIFICACION_OVERRIDE} caracteres.`
  }
  return errors
}

// ── DecisionModal ─────────────────────────────────────────────────────────────

function DecisionModal({ recomendaciones, onClose, onSaved }) {
  const [fields, setFields] = useState({ decision: '', comentario: '' })
  const [errors, setErrors] = useState({})
  const [saving, setSaving]   = useState(false)
  const [notification, setNotification] = useState(null)
  const textRef = useRef(null)

  const recomendacionId = recomendaciones?.[0]?.id ?? null
  const tipoRecomendacionNorm = normalizeRec(recomendaciones?.[0]?.tipoRecomendacion || recomendaciones?.[0]?.tipo || '')
  const esOverride = esOverrideCliente(fields.decision, tipoRecomendacionNorm)

  const showNotification = (type, title, message) => {
    setNotification({ type, title, message })
  }
  const closeNotification = () => {
    setNotification(null)
  }

  useEffect(() => {
    textRef.current?.focus()
    const handleKey = (e) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [onClose])

  const set = (k) => (e) => {
    setFields(prev => ({ ...prev, [k]: e.target.value }))
    if (errors[k]) setErrors(prev => { const n = { ...prev }; delete n[k]; return n })
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    const errs = validateDecision(fields, esOverride)
    if (Object.keys(errs).length) { setErrors(errs); return }
    if (!recomendacionId) {
      showNotification('error', 'Error', 'No hay recomendación disponible para registrar la decisión.')
      return
    }
    setSaving(true)
    try {
      await api.post('/decisionesDespliegue', {
        recomendacionId,
        decisionFinal: fields.decision.toLowerCase(),
        comentario:    fields.comentario.trim(),
      })
      onSaved()
    } catch (err) {
      const msg = err.response?.data?.message
        || err.response?.data?.title
        || 'No se pudo registrar la decisión. Intentá de nuevo.'
      showNotification('error', 'Error', msg)
    } finally {
      setSaving(false)
    }
  }

  const remaining = 1000 - (fields.comentario?.length || 0)

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-box modal-box-md" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2 className="modal-title">Registrar decisión de despliegue</h2>
          <button className="modal-close" onClick={onClose} aria-label="Cerrar">×</button>
        </div>

        <form onSubmit={handleSubmit} noValidate>
          <div className="modal-body">
            <div className={`proy-form-group ${errors.decision ? 'has-error' : ''}`}>
              <label>Decisión <span className="required">*</span></label>
              <div className="dec-options">
                {['Aprobado', 'Rechazado'].map(opt => (
                  <label
                    key={opt}
                    className={`dec-option ${opt === 'Aprobado' ? 'dec-option-ok' : 'dec-option-no'} ${fields.decision === opt ? 'dec-option-selected' : ''}`}
                  >
                    <input
                      type="radio"
                      name="decision"
                      value={opt}
                      checked={fields.decision === opt}
                      onChange={set('decision')}
                    />
                    {opt === 'Aprobado' ? '✓ Aprobado' : '✕ Rechazado'}
                  </label>
                ))}
              </div>
              {errors.decision && <span className="field-error">{errors.decision}</span>}
            </div>

            {esOverride && (
              <div className="dec-override-warning">
                <span className="dec-override-warning-icon">⚠</span>
                <span>
                  Esta decisión va <strong>en contra</strong> de la recomendación del sistema.
                  Detallá con claridad el motivo (mínimo {MIN_LARGO_JUSTIFICACION_OVERRIDE} caracteres) —
                  quedará marcada como excepción en el historial.
                </span>
              </div>
            )}

            <div className={`proy-form-group ${errors.comentario ? 'has-error' : ''}`}>
              <label htmlFor="dec-comentario">
                Justificación <span className="required">*</span>
                <span className="char-count">{remaining} restantes</span>
              </label>
              <textarea
                id="dec-comentario"
                ref={textRef}
                value={fields.comentario}
                onChange={set('comentario')}
                placeholder="Explicá los motivos de esta decisión…"
                rows={4}
                maxLength={1001}
              />
              {errors.comentario && <span className="field-error">{errors.comentario}</span>}
            </div>
          </div>

          <div className="modal-footer">
            <button type="button" className="btn-cancel" onClick={onClose} disabled={saving}>
              Cancelar
            </button>
            <button type="submit" className="btn-save" disabled={saving}>
              {saving
                ? <><span className="btn-spinner" /> Registrando…</>
                : 'Confirmar decisión'}
            </button>
          </div>
        </form>

        <NotificationModal
          isOpen={!!notification}
          type={notification?.type}
          title={notification?.title}
          message={notification?.message}
          onClose={closeNotification}
        />
      </div>
    </div>
  )
}

// ── AnalisisVersion ───────────────────────────────────────────────────────────

function AnalisisVersion() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const resultadoId = searchParams.get('resultado')

  const [version,          setVersion]          = useState(null)
  const [metricas,         setMetricas]         = useState([])
  const [reglasPorCriterio, setReglasPorCriterio] = useState({})
  const [recomendaciones,  setRecomendaciones]  = useState([])
  const [decisiones,       setDecisiones]       = useState([])
  const [archivoInfo,      setArchivoInfo]      = useState(null)
  const [loading,          setLoading]          = useState(true)
  const [error,            setError]            = useState(null)
  const [showModal,        setShowModal]        = useState(false)
  const [descargandoActaId, setDescargandoActaId] = useState(null)
  const [notification,     setNotification]     = useState(null)

  const loadData = async () => {
    setLoading(true)
    setError(null)
    setArchivoInfo(null)
    try {
      // Si venimos desde el historial con un resultadoId específico, cargamos
      // datos de ESE resultado. Si no, cargamos el último resultado de la versión.
      const [vRes, mRes, rRes, dRes] = await Promise.all([
        api.get(`/versiones/${id}`),
        resultadoId
          ? api.get(`/metricas/resultado/${resultadoId}`)
          : api.get(`/metricas/version/${id}`),
        resultadoId
          ? api.get(`/recomendaciones/resultado/${resultadoId}`)
          : api.get(`/recomendaciones/version/${id}`),
        resultadoId
          ? api.get(`/decisionesDespliegue/resultado/${resultadoId}`)
          : api.get(`/decisionesDespliegue/version/${id}`),
      ])
      setVersion(vRes.data)
      setMetricas(mRes.data)
      setRecomendaciones(rRes.data)
      setDecisiones(dRes.data)

      // Detalles del archivo de origen (RF06): el resultadoId real viene de las
      // propias métricas si no vino por query string.
      const rid = resultadoId || mRes.data[0]?.resultadoId
      if (rid) {
        try {
          const aRes = await api.get(`/resultadosprueba/${rid}`)
          setArchivoInfo(aRes.data)
        } catch { /* no crítico si falla */ }

        try {
          const reglasRes = await api.get(`/recomendaciones/resultado/${rid}/reglas`)
          const lookup = {}
          for (const r of reglasRes.data) {
            if (r.criterio) lookup[r.criterio.toLowerCase().trim()] = r.resultadoRegla
          }
          setReglasPorCriterio(lookup)
        } catch { /* sin veredicto real, las tarjetas quedan neutrales */ }
      }
    } catch {
      setError('No se pudo cargar la información de esta versión.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { loadData() }, [id, resultadoId])

  // Prioridad: NO_DESPLEGAR > REVISAR > DESPLEGAR
  const semaforo = (() => {
    if (!recomendaciones.length) return null
    const tipos = recomendaciones.map(r => normalizeRec(r.tipoRecomendacion || r.tipo || ''))
    if (tipos.includes('NO_DESPLEGAR')) return TIPO_RECOMENDACION.NO_DESPLEGAR
    if (tipos.includes('REVISAR'))      return TIPO_RECOMENDACION.REVISAR
    if (tipos.includes('DESPLEGAR'))    return TIPO_RECOMENDACION.DESPLEGAR
    return null
  })()

  const handleDecisionSaved = () => {
    setShowModal(false)
    loadData()
  }

  const descargarActa = async (decisionId) => {
    setDescargandoActaId(decisionId)
    try {
      const res = await api.get(`/decisionesDespliegue/${decisionId}/acta`, { responseType: 'blob' })
      const blobUrl = window.URL.createObjectURL(res.data)
      const a = document.createElement('a')
      a.href = blobUrl
      a.download = `Acta-Despliegue-${String(decisionId).padStart(6, '0')}.pdf`
      document.body.appendChild(a)
      a.click()
      a.remove()
      window.URL.revokeObjectURL(blobUrl)
    } catch {
      setNotification({ type: 'error', title: 'Error', message: 'No se pudo generar el acta en PDF. Intentá de nuevo.' })
    } finally {
      setDescargandoActaId(null)
    }
  }

  const backPath = version?.proyectoId ? `/proyectos/${version.proyectoId}` : '/proyectos'

  if (loading) {
    return (
      <div className="av-loading">
        <span className="av-spinner" /> Cargando análisis…
      </div>
    )
  }

  if (error) {
    return (
      <div className="av-page">
        <div className="av-error-box">
          <p>{error}</p>
          <button className="btn-back-link" onClick={() => navigate('/proyectos')}>
            ← Volver a proyectos
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="av-page">

      {/* ── Header ── */}
      <div className="av-header">
        <div className="av-header-inner">
          <button className="btn-back-link" onClick={() => navigate(backPath)}>
            ← Volver al proyecto
          </button>
          <div className="av-header-text">
            <h1 className="av-title">Análisis - v{version?.numeroVersion}</h1>
            <p className="av-subtitle">{version?.descripcion || 'Sin descripción'}</p>
          </div>
        </div>
      </div>

      <div className="av-body">

        {/* ── Semáforo ── */}
        <section className="av-section">
          <h2 className="av-section-title">Recomendación del sistema</h2>
          {semaforo ? (
            <div className={`semaforo-card ${semaforo.cls}`}>
              <div className={`sem-icon ${semaforo.iconCls}`}>{semaforo.icon}</div>
              <div className="sem-text">
                <p className="sem-label">{semaforo.label}</p>
                <p className="sem-sublabel">{semaforo.sublabel}</p>
              </div>
            </div>
          ) : (
            <div className="semaforo-card sem-sin-datos">
              <div className="sem-icon sem-icon-neutral">-</div>
              <div className="sem-text">
                <p className="sem-label">Sin recomendación</p>
                <p className="sem-sublabel">No hay métricas evaluadas para esta versión aún.</p>
              </div>
            </div>
          )}
        </section>

        {/* ── Métricas ── */}
        {metricas.length > 0 && (
          <section className="av-section">
            <h2 className="av-section-title">Métricas de calidad</h2>
            <div className="av-metrics-grid">
              {metricas.map((m, i) => (
                <div key={m.id ?? i} className={`av-metric-card ${metricColorClass(m, reglasPorCriterio)}`}>
                  <p className="av-metric-name">{metricLabel(m.nombreMetrica || m.nombre)}</p>
                  <p className="av-metric-value">{formatMetricValue(m)}</p>
                </div>
              ))}
            </div>
          </section>
        )}

        {/* ── Archivo de origen ── */}
        {archivoInfo && (
          <section className="av-section">
            <h2 className="av-section-title">Archivo de origen</h2>
            <div className="av-file-info">
              <div className="av-file-row">
                <span className="av-file-label">Archivo</span>
                <span className="av-file-value">{archivoInfo.nombreArchivo || '—'}</span>
              </div>
              <div className="av-file-row">
                <span className="av-file-label">Cargado el</span>
                <span className="av-file-value">
                  {archivoInfo.fechaCarga
                    ? new Date(archivoInfo.fechaCarga).toLocaleString('es-PY', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
                    : '—'}
                </span>
              </div>
              {archivoInfo.observaciones && (
                <div className="av-file-row av-file-row-full">
                  <span className="av-file-label">Observaciones</span>
                  <span className="av-file-value">{archivoInfo.observaciones}</span>
                </div>
              )}
            </div>
          </section>
        )}

        {/* ── Decisiones ── */}
        <section className="av-section av-section-dec">
          <div className="av-dec-header">
            <h2 className="av-section-title">Decisiones de despliegue</h2>
            {recomendaciones.length > 0 && (
              <button className="btn-nuevo" onClick={() => setShowModal(true)}>
                + Registrar decisión
              </button>
            )}
          </div>

          {decisiones.length === 0 ? (
            <p className="av-empty">No hay decisiones registradas para esta versión.</p>
          ) : (
            <div className="av-dec-list">
              {[...decisiones]
                .sort((a, b) => new Date(b.fechaDecision || b.fecha) - new Date(a.fechaDecision || a.fecha))
                .map((d, i) => {
                  const dm = DECISION_MAP[d.decisionFinal?.toLowerCase()] || { text: d.decisionFinal, cls: '' }
                  const fechaDecision = new Date(d.fechaDecision || d.fecha)
                  const fechaStr = fechaDecision.toLocaleDateString('es-PY', {
                    day: '2-digit', month: 'long', year: 'numeric',
                  })
                  const horaStr = fechaDecision.toLocaleTimeString('es-PY', {
                    hour: '2-digit', minute: '2-digit',
                  })
                  return (
                    <div key={d.id ?? i} className={`av-dec-item ${d.esOverride ? 'av-dec-item--override' : ''}`}>
                      <div className="av-dec-header">
                        <div className="av-dec-badges">
                          <span className={`av-dec-badge ${dm.cls}`}>{dm.text}</span>
                          {d.esOverride && (
                            <span className="av-dec-badge av-dec-badge--override" title="Esta decisión fue en contra de la recomendación del sistema">
                              ⚠ Contradice la recomendación
                            </span>
                          )}
                        </div>
                        <span className="av-dec-date">{fechaStr} · {horaStr}</span>
                      </div>
                      {d.comentario && (
                        <p className="av-dec-comentario">{d.comentario}</p>
                      )}
                      <div className="av-dec-footer">
                        <button
                          className="av-dec-acta-btn"
                          onClick={() => descargarActa(d.id)}
                          disabled={descargandoActaId === d.id}
                        >
                          {descargandoActaId === d.id
                            ? <><span className="btn-spinner" /> Generando…</>
                            : '⬇ Descargar acta (PDF)'}
                        </button>
                      </div>
                    </div>
                  )
                })}
            </div>
          )}
        </section>
      </div>

      {showModal && (
        <DecisionModal
          recomendaciones={recomendaciones}
          onClose={() => setShowModal(false)}
          onSaved={handleDecisionSaved}
        />
      )}

      <NotificationModal
        isOpen={!!notification}
        type={notification?.type}
        title={notification?.title}
        message={notification?.message}
        onClose={() => setNotification(null)}
      />
    </div>
  )
}

export default AnalisisVersion
