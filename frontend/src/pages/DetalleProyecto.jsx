import { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import api from '../services/api'
import NotificationModal from '../components/NotificationModal'
import { isAdmin } from '../utils/auth'
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

// ── Umbrales de calidad por proyecto (RF07/RF08/CU-03) ─────────────────────────

const CRITERIO_LABEL = {
  tasa_exito:       { nombre: 'Tasa de éxito mínima',    unidad: '%',  tipo: '≥' },
  cobertura:        { nombre: 'Cobertura mínima',        unidad: '%',  tipo: '≥' },
  tasa_fallo:       { nombre: 'Tasa de fallo máxima',    unidad: '%',  tipo: '≤' },
  tiempo_ejecucion: { nombre: 'Tiempo de ejecución máx.', unidad: 's', tipo: '≤' },
}

function UmbralesPanel({ proyectoId, esAdmin }) {
  const [reglas,   setReglas]   = useState([])
  const [loading,  setLoading]  = useState(true)
  const [editando, setEditando] = useState(null)   // criterio en edición
  const [valor,    setValor]    = useState('')
  const [saving,   setSaving]   = useState(false)
  const [notification, setNotification] = useState(null)

  const showNotification = (type, title, message) => setNotification({ type, title, message })
  const closeNotification = () => setNotification(null)

  const loadReglas = () => {
    setLoading(true)
    api.get(`/reglaEvaluacion/proyecto/${proyectoId}`)
      .then(res => setReglas(res.data))
      .catch(() => showNotification('error', 'Error', 'No se pudieron cargar los umbrales del proyecto.'))
      .finally(() => setLoading(false))
  }

  useEffect(() => { loadReglas() }, [proyectoId])

  const startEdit = (r) => {
    setEditando(r.criterio)
    setValor(String(r.umbral ?? ''))
  }

  const cancelEdit = () => { setEditando(null); setValor('') }

  const guardarUmbral = async (criterio) => {
    const num = parseFloat(valor)
    if (isNaN(num) || num < 0) {
      showNotification('error', 'Valor inválido', 'Ingresá un número mayor o igual a 0.')
      return
    }
    setSaving(true)
    try {
      await api.put(`/reglaEvaluacion/proyecto/${proyectoId}`, { criterio, umbral: num })
      setEditando(null)
      loadReglas()
      showNotification('success', 'Umbral actualizado', 'El umbral personalizado del proyecto se guardó correctamente.')
    } catch (err) {
      showNotification('error', 'Error', err.response?.data?.message || 'No se pudo guardar el umbral.')
    } finally {
      setSaving(false)
    }
  }

  const restablecer = async (criterio) => {
    setSaving(true)
    try {
      await api.delete(`/reglaEvaluacion/proyecto/${proyectoId}/${criterio}`)
      loadReglas()
      showNotification('success', 'Restablecido', 'El proyecto vuelve a usar el umbral global para ese criterio.')
    } catch (err) {
      showNotification('error', 'Error', err.response?.data?.message || 'No se pudo restablecer el umbral.')
    } finally {
      setSaving(false)
    }
  }

  if (loading) return null
  if (reglas.length === 0) return null

  return (
    <div className="dp-umbrales-card">
      <h2 className="dp-section-title">
        Umbrales de calidad
        {!esAdmin && <span className="dp-umbrales-readonly">Solo lectura</span>}
      </h2>
      <p className="dp-umbrales-hint">
        Criterios que evalúa el motor de recomendación para las versiones de este proyecto.
        {esAdmin && ' Podés personalizarlos; si no, se usa el umbral global del sistema.'}
      </p>

      <div className="dp-umbrales-list">
        {reglas.map((r) => {
          const meta = CRITERIO_LABEL[r.criterio] || { nombre: r.criterio, unidad: '', tipo: '' }
          const enEdicion = editando === r.criterio
          return (
            <div key={r.criterio} className="dp-umbral-row">
              <div className="dp-umbral-info">
                <span className="dp-umbral-nombre">{meta.nombre}</span>
                <span className={`dp-umbral-tag ${r.esPersonalizado ? 'tag-personalizado' : 'tag-global'}`}>
                  {r.esPersonalizado ? 'Personalizado' : 'Global'}
                </span>
              </div>

              {enEdicion ? (
                <div className="dp-umbral-edit">
                  <span className="dp-umbral-op">{meta.tipo}</span>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    value={valor}
                    onChange={(e) => setValor(e.target.value)}
                    autoFocus
                  />
                  <span className="dp-umbral-unidad">{meta.unidad}</span>
                  <button className="btn-cancel dp-umbral-btn" onClick={cancelEdit} disabled={saving}>Cancelar</button>
                  <button className="btn-save dp-umbral-btn" onClick={() => guardarUmbral(r.criterio)} disabled={saving}>
                    {saving ? <span className="btn-spinner" /> : 'Guardar'}
                  </button>
                </div>
              ) : (
                <div className="dp-umbral-valor">
                  <span>{meta.tipo} {r.umbral} {meta.unidad}</span>
                  {esAdmin && (
                    <>
                      <button className="btn-analizar dp-umbral-btn" onClick={() => startEdit(r)}>Editar</button>
                      {r.esPersonalizado && (
                        <button className="btn-back-link dp-umbral-btn" onClick={() => restablecer(r.criterio)} disabled={saving}>
                          Restablecer a global
                        </button>
                      )}
                    </>
                  )}
                </div>
              )}
            </div>
          )
        })}
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
  const tipoLabel  = TIPO_MAP[proyecto.tipoSolucion] || proyecto.tipoSolucion || '-'
  const esAdmin    = isAdmin()

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
                <span className={`dp-badge ${estadoProy.cls}`}>{estadoProy.text}</span>
                <span className="dp-badge dp-badge-tipo">{tipoLabel}</span>
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
        <UmbralesPanel proyectoId={id} esAdmin={esAdmin} />

        <h2 className="dp-section-title">
          Versiones
          <span className="dp-count-badge">{versiones.length}</span>
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
