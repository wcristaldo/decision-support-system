import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../services/api'
import NotificationModal from '../components/NotificationModal'
import '../styles/Proyectos.css'

// ── Constantes ────────────────────────────────────────────────────────────────

const TIPO_OPCIONES = [
  { value: 'web',  label: 'Aplicación Web' },
  { value: 'api',  label: 'API / Microservicio' },
]

const TIPO_LABEL = {
  web:  'Aplicación Web',
  api:  'API / Microservicio',
  otro: 'Otro',
}

const ESTADO_BADGE = {
  activo:    { text: 'Activo',    cls: 'badge-activo' },
  inactivo:  { text: 'Inactivo',  cls: 'badge-inactivo' },
  archivado: { text: 'Archivado', cls: 'badge-archivado' },
}

const NOMBRE_RE = /^[a-zA-ZÀ-ÿ0-9][\w\s\-.:()À-ÿ]*$/

// ── Validación ────────────────────────────────────────────────────────────────

const VERSION_RE = /^\d+\.\d+\.\d+$/

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
    errors.nombre = 'Debe iniciar con letra o número.'
  }
  if (fields.descripcion && fields.descripcion.length > 500) {
    errors.descripcion = 'No puede superar los 500 caracteres.'
  }
  if (fields.versionInicial && !VERSION_RE.test(fields.versionInicial.trim())) {
    errors.versionInicial = 'Formato inválido. Use: 1.0.0 (major.minor.patch)'
  }
  return errors
}

// ── Modal Crear ───────────────────────────────────────────────────────────────

function CreateModal({ onClose, onSaved, showNotification }) {
  const [fields, setFields] = useState({ nombre: '', descripcion: '', tipoSolucion: '', versionInicial: '1.0.0' })
  const [errors, setErrors] = useState({})
  const [saving, setSaving] = useState(false)
  const nombreRef = useRef(null)

  useEffect(() => {
    nombreRef.current?.focus()
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
    const errs = validate(fields)
    if (Object.keys(errs).length) { setErrors(errs); return }
    setSaving(true)
    try {
      await api.post('/proyectos', {
        nombre:           fields.nombre.trim(),
        descripcion:      fields.descripcion.trim() || null,
        tipoSolucion:     fields.tipoSolucion || null,
        versionInicial:   fields.versionInicial.trim(),
      })
      onSaved('Proyecto creado correctamente.')
    } catch (err) {
      const errorMsg = err.response?.data?.message || err.response?.data?.title || 'No se pudo crear el proyecto. Intentá de nuevo.'
      showNotification('error', 'Error al crear proyecto', errorMsg)
    } finally {
      setSaving(false)
    }
  }

  const remaining = 500 - (fields.descripcion?.length || 0)

  return (
    <div className="modal-overlay" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal-box" role="dialog" aria-modal="true">
        <div className="modal-header">
          <h2 className="modal-title">Nuevo proyecto</h2>
          <button className="modal-close" onClick={onClose}>✕</button>
        </div>
        <form onSubmit={handleSubmit} noValidate>
          <div className="modal-body">
            <div className={`proy-form-group ${errors.nombre ? 'has-error' : ''}`}>
              <label htmlFor="cn-nombre">Nombre <span className="required">*</span></label>
              <input id="cn-nombre" ref={nombreRef} type="text"
                value={fields.nombre} onChange={set('nombre')}
                placeholder="Ej: Portal de Clientes v2" maxLength={101} autoComplete="off" />
              {errors.nombre
                ? <span className="field-error">{errors.nombre}</span>
                : <span className="field-hint">Entre 3 y 100 caracteres.</span>}
            </div>
            <div className="proy-form-group">
              <label htmlFor="cn-tipo">Tipo de solución</label>
              <select id="cn-tipo" value={fields.tipoSolucion} onChange={set('tipoSolucion')}>
                <option value="">Seleccionar tipo…</option>
                {TIPO_OPCIONES.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>
            <div className={`proy-form-group ${errors.versionInicial ? 'has-error' : ''}`}>
              <label htmlFor="cn-version">Versión inicial <span className="required">*</span></label>
              <input id="cn-version" type="text"
                value={fields.versionInicial} onChange={set('versionInicial')}
                placeholder="1.0.0" maxLength={20} autoComplete="off" />
              {errors.versionInicial
                ? <span className="field-error">{errors.versionInicial}</span>
                : <span className="field-hint">Formato semántico: major.minor.patch (ej: 1.0.0)</span>}
            </div>
            <div className={`proy-form-group ${errors.descripcion ? 'has-error' : ''}`}>
              <label htmlFor="cn-desc">
                Descripción <span className="char-count">{remaining} restantes</span>
              </label>
              <textarea id="cn-desc" value={fields.descripcion} onChange={set('descripcion')}
                placeholder="Descripción breve del proyecto…" rows={3} maxLength={501} />
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

// ── Modal Editar ──────────────────────────────────────────────────────────────

function EditModal({ proyecto, onClose, onSaved, showNotification }) {
  const [fields, setFields] = useState({
    nombre:       proyecto.nombre       || '',
    descripcion:  proyecto.descripcion  || '',
    tipoSolucion: proyecto.tipoSolucion || '',
    versionInicial: proyecto.versionInicial || '1.0.0',
  })
  const [errors, setErrors] = useState({})
  const [saving, setSaving] = useState(false)
  const nombreRef = useRef(null)

  useEffect(() => {
    nombreRef.current?.focus()
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
    const errs = validate(fields)
    if (Object.keys(errs).length) { setErrors(errs); return }
    setSaving(true)
    try {
      await api.put(`/proyectos/${proyecto.id}`, {
        nombre:           fields.nombre.trim(),
        descripcion:      fields.descripcion.trim() || null,
        tipoSolucion:     fields.tipoSolucion || null,
        versionInicial:   fields.versionInicial.trim(),
      })
      onSaved('Proyecto actualizado correctamente.')
    } catch (err) {
      const errorMsg = err.response?.data?.message || err.response?.data?.title || 'No se pudo actualizar el proyecto.'
      showNotification('error', 'Error al actualizar proyecto', errorMsg)
    } finally {
      setSaving(false)
    }
  }

  const remaining = 500 - (fields.descripcion?.length || 0)

  return (
    <div className="modal-overlay" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal-box" role="dialog" aria-modal="true">
        <div className="modal-header">
          <h2 className="modal-title">Editar proyecto</h2>
          <button className="modal-close" onClick={onClose}>✕</button>
        </div>
        <form onSubmit={handleSubmit} noValidate>
          <div className="modal-body">
            <div className={`proy-form-group ${errors.nombre ? 'has-error' : ''}`}>
              <label htmlFor="ep-nombre">Nombre <span className="required">*</span></label>
              <input id="ep-nombre" ref={nombreRef} type="text"
                value={fields.nombre} onChange={set('nombre')}
                maxLength={101} autoComplete="off" />
              {errors.nombre && <span className="field-error">{errors.nombre}</span>}
            </div>
            <div className="proy-form-group">
              <label htmlFor="ep-tipo">Tipo de solución</label>
              <select id="ep-tipo" value={fields.tipoSolucion} onChange={set('tipoSolucion')}>
                <option value="">Seleccionar tipo…</option>
                {TIPO_OPCIONES.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>
            <div className={`proy-form-group ${errors.versionInicial ? 'has-error' : ''}`}>
              <label htmlFor="ep-version">Versión <span className="required">*</span></label>
              <input id="ep-version" type="text"
                value={fields.versionInicial} onChange={set('versionInicial')}
                placeholder="1.0.0" maxLength={20} autoComplete="off" />
              {errors.versionInicial && <span className="field-error">{errors.versionInicial}</span>}
            </div>
            <div className={`proy-form-group ${errors.descripcion ? 'has-error' : ''}`}>
              <label htmlFor="ep-desc">
                Descripción <span className="char-count">{remaining} restantes</span>
              </label>
              <textarea id="ep-desc" value={fields.descripcion} onChange={set('descripcion')}
                rows={3} maxLength={501} />
              {errors.descripcion && <span className="field-error">{errors.descripcion}</span>}
            </div>
          </div>
          <div className="modal-footer">
            <button type="button" className="btn-cancel" onClick={onClose}>Cancelar</button>
            <button type="submit" className="btn-save" disabled={saving}>
              {saving ? <><span className="btn-spinner" /> Guardando…</> : 'Guardar cambios'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ── Modal Confirmar eliminación ───────────────────────────────────────────────

function DeleteModal({ proyecto, onClose, onDeleted, showNotification }) {
  const [deleting, setDeleting] = useState(false)

  useEffect(() => {
    const handleKey = (e) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [onClose])

  const handleDelete = async () => {
    setDeleting(true)
    try {
      await api.delete(`/proyectos/${proyecto.id}`)
      onDeleted(`Proyecto "${proyecto.nombre}" eliminado.`)
    } catch (err) {
      const errorMsg = err.response?.data?.message || 'No se pudo eliminar el proyecto.'
      showNotification('error', 'Error al eliminar proyecto', errorMsg)
    }
  }

  return (
    <div className="modal-overlay" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal-box modal-box-sm" role="dialog" aria-modal="true">
        <div className="modal-header">
          <h2 className="modal-title">Eliminar proyecto</h2>
          <button className="modal-close" onClick={onClose}>✕</button>
        </div>
        <div className="modal-body">
          <p style={{ fontSize: '0.9rem', color: 'var(--text-mid)', margin: 0 }}>
            ¿Eliminás el proyecto <strong style={{ color: 'var(--text-dark)' }}>"{proyecto.nombre}"</strong>?
            Esta acción no se puede deshacer.
          </p>
        </div>
        <div className="modal-footer">
          <button type="button" className="btn-cancel" onClick={onClose}>Cancelar</button>
          <button className="btn-delete" onClick={handleDelete} disabled={deleting}>
            {deleting ? <><span className="btn-spinner btn-spinner-red" /> Eliminando…</> : 'Eliminar'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Proyectos (tabla) ─────────────────────────────────────────────────────────

function Proyectos() {
  const navigate = useNavigate()
  const [proyectos,      setProyectos]      = useState([])
  const [loading,        setLoading]        = useState(true)
  const [error,          setError]          = useState(null)
  const [notification,   setNotification]   = useState(null)
  const [modal,          setModal]          = useState(null)
  const [target,         setTarget]         = useState(null)
  const [togglingId,     setTogglingId]     = useState(null)

  const showNotification = (type, title, message) => {
    setNotification({ type, title, message })
  }

  const closeNotification = () => {
    setNotification(null)
  }

  const fetchProyectos = async () => {
    try {
      const res = await api.get('/proyectos')
      setProyectos(res.data)
      setError(null)
    } catch {
      setError('No se pudieron cargar los proyectos.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { fetchProyectos() }, [])

  const closeModal = () => { setModal(null); setTarget(null) }

  const handleSaved = (msg) => {
    closeModal()
    showNotification('success', 'Éxito', msg)
    fetchProyectos()
  }

  const toggleEstado = async (p) => {
    setTogglingId(p.id)
    const nuevoEstado = p.estado === 'activo' ? 'inactivo' : 'activo'
    try {
      await api.put(`/proyectos/${p.id}`, {
        nombre:       p.nombre,
        descripcion:  p.descripcion,
        tipoSolucion: p.tipoSolucion,
        estado:       nuevoEstado,
      })
      showNotification('success', 'Éxito', `Proyecto ${nuevoEstado === 'activo' ? 'activado' : 'inactivado'} correctamente.`)
      fetchProyectos()
    } catch (err) {
      const errorMsg = err.response?.data?.message || err.response?.data?.title || 'No se pudo cambiar el estado del proyecto.'
      showNotification('error', 'Error', errorMsg)
    } finally {
      setTogglingId(null)
    }
  }

  return (
    <div className="proy-page">

      {/* ── Header ── */}
      <div className="proy-header">
        <div className="proy-header-inner">
          <div>
            <h1 className="proy-title">Proyectos</h1>
            <p className="proy-subtitle">
              {loading ? '' : `${proyectos.length} proyecto${proyectos.length !== 1 ? 's' : ''} registrado${proyectos.length !== 1 ? 's' : ''}`}
            </p>
          </div>
          <button className="btn-nuevo" onClick={() => setModal('create')}>
            + Nuevo proyecto
          </button>
        </div>
      </div>

      <div className="proy-body">

        {error && (
          <div className="proy-error">
            <span className="proy-error-icon">!</span>{error}
          </div>
        )}

        {loading ? (
          <div className="proy-loading">
            <span className="proy-spinner" /> Cargando proyectos…
          </div>
        ) : proyectos.length === 0 && !error ? (
          <div className="proy-empty">
            <p>No hay proyectos registrados.</p>
            <button className="btn-nuevo" onClick={() => setModal('create')}>Crear el primero</button>
          </div>
        ) : (
          <div className="proy-table-wrap">
            <table className="proy-table">
              <colgroup>
                <col style={{ width: '22%' }} />
                <col style={{ width: '24%' }} />
                <col style={{ width: '12%' }} />
                <col style={{ width: '10%' }} />
                <col style={{ width: '10%' }} />
                <col style={{ width: '22%' }} />
              </colgroup>
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Descripción</th>
                  <th>Tipo</th>
                  <th>Versión</th>
                  <th>Estado</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {proyectos.map((p) => {
                  const est = ESTADO_BADGE[p.estado] || { text: p.estado, cls: 'badge-activo' }
                  const esActivo = p.estado === 'activo'
                  return (
                    <tr key={p.id}>
                      <td>
                        <button
                          className="proy-td-link"
                          onClick={() => navigate(`/proyectos/${p.id}`)}
                          title="Ver versiones"
                        >
                          {p.nombre}
                        </button>
                      </td>
                      <td className="proy-td-desc">{p.descripcion || <span className="proy-td-empty">-</span>}</td>
                      <td className="proy-td-tipo">{TIPO_LABEL[p.tipoSolucion] || '-'}</td>
                      <td className="proy-td-version">{p.versionInicial || '1.0.0'}</td>
                      <td>
                        <span className={`proy-badge ${est.cls}`}>{est.text}</span>
                      </td>
                      <td>
                        <div className="proy-actions">
                          <button className="proy-btn-action proy-btn-edit"
                            onClick={() => { setTarget(p); setModal('edit') }}>
                            Editar
                          </button>
                          <button
                            className={`proy-btn-action ${esActivo ? 'proy-btn-inactivar' : 'proy-btn-activar'}`}
                            onClick={() => toggleEstado(p)}
                            disabled={togglingId === p.id}
                          >
                            {togglingId === p.id ? '…' : esActivo ? 'Inactivar' : 'Activar'}
                          </button>
                          <button className="proy-btn-action proy-btn-delete"
                            onClick={() => { setTarget(p); setModal('delete') }}
                            title="Eliminar proyecto">
                            ✕
                          </button>
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {modal === 'create' && (
        <CreateModal onClose={closeModal} onSaved={handleSaved} showNotification={showNotification} />
      )}
      {modal === 'edit' && target && (
        <EditModal proyecto={target} onClose={closeModal} onSaved={handleSaved} showNotification={showNotification} />
      )}
      {modal === 'delete' && target && (
        <DeleteModal proyecto={target} onClose={closeModal} onDeleted={handleSaved} showNotification={showNotification} />
      )}

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

export default Proyectos
