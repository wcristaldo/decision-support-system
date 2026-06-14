import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../services/api'
import '../styles/Proyectos.css'

const TIPO_OPCIONES = [
  { value: '', label: 'Sin especificar' },
  { value: 'web', label: 'Aplicación Web' },
  { value: 'api', label: 'API / Microservicio' },
  { value: 'otro', label: 'Otro' },
]

const ESTADO_LABEL = {
  activo:    { text: 'Activo',    cls: 'badge-activo' },
  inactivo:  { text: 'Inactivo',  cls: 'badge-inactivo' },
  archivado: { text: 'Archivado', cls: 'badge-archivado' },
}

const NOMBRE_RE = /^[a-zA-ZÀ-ÿ0-9][\w\s\-.:()À-ÿ]*$/

function validate(fields) {
  const errors = {}
  const nombre = fields.nombre.trim()
  if (!nombre) {
    errors.nombre = 'El nombre es obligatorio.'
  } else if (nombre.length < 3) {
    errors.nombre = 'Debe tener al menos 3 caracteres.'
  } else if (nombre.length > 100) {
    errors.nombre = 'No puede superar los 100 caracteres.'
  } else if (!NOMBRE_RE.test(nombre)) {
    errors.nombre = 'Debe iniciar con letra o número. No se permiten caracteres especiales al inicio.'
  }
  if (fields.descripcion && fields.descripcion.length > 500) {
    errors.descripcion = 'No puede superar los 500 caracteres.'
  }
  return errors
}

function Modal({ onClose, onSaved }) {
  const [fields, setFields] = useState({ nombre: '', descripcion: '', tipoSolucion: '' })
  const [errors, setErrors] = useState({})
  const [saving, setSaving] = useState(false)
  const [apiError, setApiError] = useState(null)
  const nombreRef = useRef(null)

  useEffect(() => {
    nombreRef.current?.focus()
    const handleKey = (e) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [onClose])

  const set = (k) => (e) => {
    setFields((prev) => ({ ...prev, [k]: e.target.value }))
    if (errors[k]) setErrors((prev) => { const n = { ...prev }; delete n[k]; return n })
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    const errs = validate(fields)
    if (Object.keys(errs).length) { setErrors(errs); return }

    setSaving(true)
    setApiError(null)
    try {
      await api.post('/proyectos', {
        nombre:       fields.nombre.trim(),
        descripcion:  fields.descripcion.trim() || null,
        tipoSolucion: fields.tipoSolucion || null,
      })
      onSaved()
    } catch (err) {
      const msg = err.response?.data?.message
        || err.response?.data?.title
        || 'No se pudo crear el proyecto. Intentá de nuevo.'
      setApiError(msg)
    } finally {
      setSaving(false)
    }
  }

  const remaining = 500 - (fields.descripcion?.length || 0)

  return (
    <div className="modal-overlay" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal-box" role="dialog" aria-modal="true" aria-labelledby="modal-title">
        <div className="modal-header">
          <h2 id="modal-title" className="modal-title">Nuevo proyecto</h2>
          <button className="modal-close" onClick={onClose} aria-label="Cerrar">✕</button>
        </div>

        <form onSubmit={handleSubmit} noValidate>
          <div className="modal-body">
            {apiError && (
              <div className="form-api-error">
                <span className="form-api-error-icon">!</span>
                {apiError}
              </div>
            )}

            {/* Nombre */}
            <div className={`proy-form-group ${errors.nombre ? 'has-error' : ''}`}>
              <label htmlFor="proy-nombre">
                Nombre <span className="required">*</span>
              </label>
              <input
                id="proy-nombre"
                ref={nombreRef}
                type="text"
                value={fields.nombre}
                onChange={set('nombre')}
                placeholder="Ej: Portal de clientes v2"
                maxLength={101}
                autoComplete="off"
              />
              {errors.nombre
                ? <span className="field-error">{errors.nombre}</span>
                : <span className="field-hint">Entre 3 y 100 caracteres.</span>}
            </div>

            {/* Tipo de solución */}
            <div className="proy-form-group">
              <label htmlFor="proy-tipo">Tipo de solución</label>
              <select
                id="proy-tipo"
                value={fields.tipoSolucion}
                onChange={set('tipoSolucion')}
              >
                {TIPO_OPCIONES.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </div>

            {/* Descripción */}
            <div className={`proy-form-group ${errors.descripcion ? 'has-error' : ''}`}>
              <label htmlFor="proy-desc">
                Descripción
                <span className="char-count">{remaining} restantes</span>
              </label>
              <textarea
                id="proy-desc"
                value={fields.descripcion}
                onChange={set('descripcion')}
                placeholder="Descripción breve del proyecto y su objetivo…"
                rows={4}
                maxLength={501}
              />
              {errors.descripcion && <span className="field-error">{errors.descripcion}</span>}
            </div>
          </div>

          <div className="modal-footer">
            <button type="button" className="btn-cancel" onClick={onClose}>Cancelar</button>
            <button type="submit" className="btn-save" disabled={saving}>
              {saving ? <><span className="btn-spinner" /> Guardando…</> : 'Crear proyecto'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

function Proyectos() {
  const navigate = useNavigate()
  const [proyectos, setProyectos] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [showModal, setShowModal] = useState(false)

  const fetchProyectos = async () => {
    try {
      const res = await api.get('/proyectos')
      setProyectos(res.data)
      setError(null)
    } catch {
      setError('No se pudieron cargar los proyectos. Verificá que el backend esté activo.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { fetchProyectos() }, [])

  const handleSaved = () => {
    setShowModal(false)
    fetchProyectos()
  }

  return (
    <div className="proy-page">
      {/* Cabecera */}
      <div className="proy-header">
        <div className="proy-header-inner">
          <div>
            <h1 className="proy-title">Proyectos</h1>
            <p className="proy-subtitle">
              {loading ? '' : `${proyectos.length} proyecto${proyectos.length !== 1 ? 's' : ''} registrado${proyectos.length !== 1 ? 's' : ''}`}
            </p>
          </div>
          <button className="btn-nuevo" onClick={() => setShowModal(true)}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" width="18" height="18">
              <line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>
            </svg>
            Nuevo proyecto
          </button>
        </div>
      </div>

      <div className="proy-body">
        {error && (
          <div className="proy-error">
            <span className="proy-error-icon">!</span>
            {error}
          </div>
        )}

        {loading ? (
          <div className="proy-loading">
            <span className="proy-spinner" /> Cargando proyectos…
          </div>
        ) : proyectos.length === 0 && !error ? (
          <div className="proy-empty">
            <div className="proy-empty-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
                <rect x="2" y="3" width="20" height="14" rx="2"/>
                <path d="M8 21h8M12 17v4"/>
              </svg>
            </div>
            <p>No hay proyectos aún.</p>
            <button className="btn-nuevo" onClick={() => setShowModal(true)}>Crear el primero</button>
          </div>
        ) : (
          <div className="proy-grid">
            {proyectos.map((p) => {
              const estado = ESTADO_LABEL[p.estado] || { text: p.estado, cls: 'badge-activo' }
              return (
                <div
                  key={p.id}
                  className="proy-card"
                  onClick={() => navigate(`/proyectos/${p.id}`)}
                  role="button"
                  tabIndex={0}
                  onKeyDown={(e) => e.key === 'Enter' && navigate(`/proyectos/${p.id}`)}
                >
                  <div className="proy-card-top">
                    <span className={`proy-badge ${estado.cls}`}>{estado.text}</span>
                    {p.tipoSolucion && (
                      <span className="proy-tipo">{TIPO_OPCIONES.find(o => o.value === p.tipoSolucion)?.label || p.tipoSolucion}</span>
                    )}
                  </div>
                  <h3 className="proy-card-name">{p.nombre}</h3>
                  {p.descripcion && (
                    <p className="proy-card-desc">{p.descripcion}</p>
                  )}
                  <div className="proy-card-footer">
                    <span className="proy-card-date">
                      {new Date(p.fechaCreacion).toLocaleDateString('es-PY', {
                        day: '2-digit', month: 'short', year: 'numeric'
                      })}
                    </span>
                    <span className="proy-card-arrow">→</span>
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </div>

      {showModal && <Modal onClose={() => setShowModal(false)} onSaved={handleSaved} />}
    </div>
  )
}

export default Proyectos
