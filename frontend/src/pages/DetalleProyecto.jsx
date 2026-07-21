import { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import api from '../services/api'
import NotificationModal from '../components/NotificationModal'
import '../styles/DetalleProyecto.css'

const SEMVER_RE = /^\d+\.\d+\.\d+$/

const ESTADO_VERSION = {
  pendiente:   { text: 'Pendiente',   cls: 'vest-pendiente' },
  en_revision: { text: 'En revisión', cls: 'vest-revision' },
  aprobada:    { text: 'Aprobada',    cls: 'vest-aprobada' },
  rechazada:   { text: 'Rechazada',   cls: 'vest-rechazada' },
}

const TIPO_MAP = {
  web:  'Aplicación Web',
  api:  'API / Microservicio',
  otro: 'Otro',
}

const ESTADO_PROY = {
  activo:    { text: 'Activo',    cls: 'badge-activo' },
  inactivo:  { text: 'Inactivo',  cls: 'badge-inactivo' },
  archivado: { text: 'Archivado', cls: 'badge-archivado' },
}

function validateVersion(fields) {
  const errors = {}
  const num = fields.numeroVersion.trim()
  if (!num) {
    errors.numeroVersion = 'El número de versión es obligatorio.'
  } else if (!SEMVER_RE.test(num)) {
    errors.numeroVersion = 'Usá formato semántico: mayor.menor.parche  (ej: 1.0.0)'
  }
  if (fields.descripcion && fields.descripcion.length > 300) {
    errors.descripcion = 'No puede superar los 300 caracteres.'
  }
  return errors
}

function VersionModal({ proyectoId, onClose, onSaved }) {
  const [fields, setFields] = useState({ numeroVersion: '', descripcion: '' })
  const [errors, setErrors] = useState({})
  const [saving, setSaving] = useState(false)
  const [notification, setNotification] = useState(null)
  const inputRef = useRef(null)

  const showNotification = (type, title, message) => {
    setNotification({ type, title, message })
  }
  const closeNotification = () => {
    setNotification(null)
  }

  useEffect(() => {
    inputRef.current?.focus()
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
    const errs = validateVersion(fields)
    if (Object.keys(errs).length) { setErrors(errs); return }
    setSaving(true)
    try {
      await api.post('/versiones', {
        proyectoId:    parseInt(proyectoId),
        numeroVersion: fields.numeroVersion.trim(),
        descripcion:   fields.descripcion.trim() || null,
      })
      onSaved()
    } catch (err) {
      const msg = err.response?.data?.message
        || err.response?.data?.title
        || 'No se pudo crear la versión. Intentá de nuevo.'
      showNotification('error', 'Error', msg)
    } finally {
      setSaving(false)
    }
  }

  const remaining = 300 - (fields.descripcion?.length || 0)

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2 className="modal-title">Nueva versión</h2>
          <button className="modal-close" onClick={onClose} aria-label="Cerrar">×</button>
        </div>

        <form onSubmit={handleSubmit} noValidate>
          <div className="modal-body">
            <div className={`proy-form-group ${errors.numeroVersion ? 'has-error' : ''}`}>
              <label htmlFor="v-num">
                Número de versión <span className="required">*</span>
              </label>
              <input
                id="v-num"
                ref={inputRef}
                type="text"
                value={fields.numeroVersion}
                onChange={set('numeroVersion')}
                placeholder="Ej: 1.0.0"
                autoComplete="off"
              />
              {errors.numeroVersion
                ? <span className="field-error">{errors.numeroVersion}</span>
                : <span className="field-hint">Formato semántico: mayor.menor.parche</span>}
            </div>

            <div className={`proy-form-group ${errors.descripcion ? 'has-error' : ''}`}>
              <label htmlFor="v-desc">
                Descripción
                <span className="char-count">{remaining} restantes</span>
              </label>
              <textarea
                id="v-desc"
                value={fields.descripcion}
                onChange={set('descripcion')}
                placeholder="Cambios incluidos en esta versión…"
                rows={3}
                maxLength={301}
              />
              {errors.descripcion && <span className="field-error">{errors.descripcion}</span>}
            </div>
          </div>

          <div className="modal-footer">
            <button type="button" className="btn-cancel" onClick={onClose} disabled={saving}>
              Cancelar
            </button>
            <button type="submit" className="btn-save" disabled={saving}>
              {saving
                ? <><span className="btn-spinner" /> Guardando…</>
                : 'Crear versión'}
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

// ── DetalleProyecto ───────────────────────────────────────────────────────────

function DetalleProyecto() {
  const { id } = useParams()
  const navigate = useNavigate()

  const [proyecto,  setProyecto]  = useState(null)
  const [versiones, setVersiones] = useState([])
  const [loading,   setLoading]   = useState(true)
  const [error,     setError]     = useState(null)
  const [showModal, setShowModal] = useState(false)

  const loadData = async () => {
    setLoading(true)
    setError(null)
    try {
      const [pRes, vRes] = await Promise.all([
        api.get(`/proyectos/${id}`),
        api.get(`/versiones/proyecto/${id}`),
      ])
      setProyecto(pRes.data)
      setVersiones(vRes.data)
    } catch {
      setError('No se pudo cargar el proyecto.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { loadData() }, [id])

  const handleVersionSaved = () => {
    setShowModal(false)
    loadData()
  }

  if (loading) {
    return (
      <div className="dp-loading">
        <span className="dp-spinner" /> Cargando proyecto…
      </div>
    )
  }

  if (error || !proyecto) {
    return (
      <div className="dp-page">
        <div className="dp-error-box">
          <p>{error || 'Proyecto no encontrado.'}</p>
          <button className="btn-back-link" onClick={() => navigate('/proyectos')}>
            ← Volver a proyectos
          </button>
        </div>
      </div>
    )
  }

  const estadoProy = ESTADO_PROY[proyecto.estado] || { text: proyecto.estado, cls: '' }
  const tipoLabel  = TIPO_MAP[proyecto.tipo] || proyecto.tipo || '-'

  return (
    <div className="dp-page">

      {/* ── Header ── */}
      <div className="dp-header">
        <div className="dp-header-inner">
          <button className="btn-back-link" onClick={() => navigate('/proyectos')}>
            ← Volver a proyectos
          </button>
          <div className="dp-header-main">
            <div className="dp-header-text">
              <h1 className="dp-title">{proyecto.nombre}</h1>
              <p className="dp-desc">{proyecto.descripcion || 'Sin descripción'}</p>
              <div className="dp-badges">
                <span className={`badge ${estadoProy.cls}`}>{estadoProy.text}</span>
                <span className="badge badge-tipo">{tipoLabel}</span>
              </div>
            </div>
            <button className="btn-nuevo" onClick={() => setShowModal(true)}>
              + Nueva versión
            </button>
          </div>
        </div>
      </div>

      {/* ── Versiones ── */}
      <div className="dp-body">
        <h2 className="dp-versions-title">
          Versiones
          <span className="dp-version-count">{versiones.length}</span>
        </h2>

        {versiones.length === 0 ? (
          <div className="dp-empty">
            <p>No hay versiones registradas para este proyecto.</p>
            <button className="btn-nuevo" onClick={() => setShowModal(true)}>
              + Crear primera versión
            </button>
          </div>
        ) : (
          <div className="dp-versions-grid">
            {versiones.map((v) => {
              const est = ESTADO_VERSION[v.estado] || { text: v.estado, cls: '' }
              return (
                <div key={v.id} className="dp-version-card">
                  <div className="dp-version-card-header">
                    <span className="dp-version-num">v{v.numeroVersion}</span>
                    <span className={`vest-badge ${est.cls}`}>{est.text}</span>
                  </div>
                  <p className="dp-version-desc">
                    {v.descripcion || 'Sin descripción'}
                  </p>
                  <div className="dp-version-card-footer">
                    <span className="dp-version-date">
                      {v.fechaCreacion
                        ? new Date(v.fechaCreacion).toLocaleDateString('es-PY', {
                            day: '2-digit', month: 'short', year: 'numeric',
                          })
                        : '-'}
                    </span>
                    <button
                      className="btn-analizar"
                      onClick={() => navigate(`/versiones/${v.id}/analisis`)}
                    >
                      Analizar →
                    </button>
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </div>

      {showModal && (
        <VersionModal
          proyectoId={id}
          onClose={() => setShowModal(false)}
          onSaved={handleVersionSaved}
        />
      )}
    </div>
  )
}

export default DetalleProyecto
