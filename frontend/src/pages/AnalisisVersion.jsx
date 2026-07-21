import { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import api from '../services/api'
import NotificationModal from '../components/NotificationModal'
import '../styles/AnalisisVersion.css'

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
  Aprobado:  { text: 'Aprobado',  cls: 'dec-aprobado' },
  Rechazado: { text: 'Rechazado', cls: 'dec-rechazado' },
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

function metricColorClass(m) {
  const nombre = (m.nombreMetrica || '').toLowerCase()
  const v = getRawValue(m)
  if (isNaN(v)) return 'mc-neutral'
  if (nombre.includes('exito') || nombre.includes('éxito') || nombre.includes('cobertura')) {
    if (v >= 95) return 'mc-ok'
    if (v >= 80) return 'mc-warn'
    return 'mc-danger'
  }
  if (nombre.includes('fallo') || nombre.includes('error') || nombre.includes('fail')) {
    if (v <= 5)  return 'mc-ok'
    if (v <= 20) return 'mc-warn'
    return 'mc-danger'
  }
  // tiempo_ejecucion: umbral por defecto 120s
  if (nombre.includes('tiempo')) {
    if (v <= 120) return 'mc-ok'
    if (v <= 126) return 'mc-warn'  // dentro del margen de tolerancia ±5%
    return 'mc-danger'
  }
  return 'mc-neutral'
}

const METRIC_LABELS = {
  tasa_exito:       'Tasa de éxito',
  tasa_fallo:       'Tasa de fallo',
  total_pruebas:    'Total de pruebas',
  pruebas_exitosas: 'Pruebas exitosas',
  pruebas_fallidas: 'Pruebas fallidas',
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

function validateDecision(fields) {
  const errors = {}
  if (!fields.decision) {
    errors.decision = 'Seleccioná una decisión.'
  }
  const comentario = fields.comentario.trim()
  if (!comentario) {
    errors.comentario = 'La justificación es obligatoria.'
  } else if (comentario.length < 10) {
    errors.comentario = 'Debe tener al menos 10 caracteres.'
  } else if (comentario.length > 1000) {
    errors.comentario = 'No puede superar los 1000 caracteres.'
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
    const errs = validateDecision(fields)
    if (Object.keys(errs).length) { setErrors(errs); return }
    if (!recomendacionId) {
      showNotification('error', 'Error', 'No hay recomendación disponible para registrar la decisión.')
      return
    }
    setSaving(true)
    try {
      await api.post('/decisionesDespliegue', {
        recomendacionId,
        decisionFinal: fields.decision,
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
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
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
                placeholder="Explicá los motivos de esta decisión (mín. 10 caracteres)…"
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

  const [version,          setVersion]          = useState(null)
  const [metricas,         setMetricas]         = useState([])
  const [recomendaciones,  setRecomendaciones]  = useState([])
  const [decisiones,       setDecisiones]       = useState([])
  const [loading,          setLoading]          = useState(true)
  const [error,            setError]            = useState(null)
  const [showModal,        setShowModal]        = useState(false)

  const loadData = async () => {
    setLoading(true)
    setError(null)
    try {
      const [vRes, mRes, rRes, dRes] = await Promise.all([
        api.get(`/versiones/${id}`),
        api.get(`/metricas/version/${id}`),
        api.get(`/recomendaciones/version/${id}`),
        api.get(`/decisionesDespliegue/version/${id}`),
      ])
      setVersion(vRes.data)
      setMetricas(mRes.data)
      setRecomendaciones(rRes.data)
      setDecisiones(dRes.data)
    } catch {
      setError('No se pudo cargar la información de esta versión.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { loadData() }, [id])

  // Prioridad: NO_DESPLEGAR > REVISAR > DESPLEGAR
  const semaforo = (() => {
    if (!recomendaciones.length) return null
    const tipos = recomendaciones.map(r => (r.tipoRecomendacion || r.tipo || '').toUpperCase())
    if (tipos.includes('NO_DESPLEGAR')) return TIPO_RECOMENDACION.NO_DESPLEGAR
    if (tipos.includes('REVISAR'))      return TIPO_RECOMENDACION.REVISAR
    if (tipos.includes('DESPLEGAR'))    return TIPO_RECOMENDACION.DESPLEGAR
    return null
  })()

  const handleDecisionSaved = () => {
    setShowModal(false)
    loadData()
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
            <div className="mc-grid">
              {metricas.map((m, i) => (
                <div key={m.id ?? i} className={`mc-card ${metricColorClass(m)}`}>
                  <p className="mc-name">{metricLabel(m.nombreMetrica || m.nombre)}</p>
                  <p className="mc-value">{formatMetricValue(m)}</p>
                </div>
              ))}
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
            <div className="dec-list">
              {[...decisiones]
                .sort((a, b) => new Date(b.fechaDecision || b.fecha) - new Date(a.fechaDecision || a.fecha))
                .map((d, i) => {
                  const dm = DECISION_MAP[d.decisionFinal] || { text: d.decisionFinal, cls: '' }
                  return (
                    <div key={d.id ?? i} className="dec-item">
                      <div className="dec-item-top">
                        <span className={`dec-badge ${dm.cls}`}>{dm.text}</span>
                        <span className="dec-date">
                          {new Date(d.fechaDecision || d.fecha).toLocaleDateString('es-PY', {
                            day: '2-digit', month: 'long', year: 'numeric',
                          })}
                        </span>
                      </div>
                      {d.comentario && (
                        <p className="dec-comentario">{d.comentario}</p>
                      )}
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
    </div>
  )
}

export default AnalisisVersion
