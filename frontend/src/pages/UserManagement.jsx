import { useState, useEffect, useRef } from 'react'
import api from '../services/api'
import NotificationModal from '../components/NotificationModal'
import ResizableTh from '../components/ResizableTh'
import FilterableTh from '../components/FilterableTh'
import Pagination from '../components/Pagination'
import { useResizableColumns } from '../hooks/useResizableColumns'
import RolesPermisosPanel from '../components/RolesPermisosPanel'
import { permisoLabel } from '../utils/permisos'
import { fmtFechaCompleta } from '../utils/fecha'
import { hasPermiso } from '../utils/auth'
import '../styles/UserManagement.css'

const UM_COLUMNS = [
  { key: 'nombre',   defaultWidth: 170, minWidth: 100 },
  { key: 'correo',   defaultWidth: 230, minWidth: 120 },
  { key: 'rol',      defaultWidth: 160, minWidth: 90  },
  { key: 'estado',   defaultWidth: 90,  minWidth: 70  },
  { key: 'creado',   defaultWidth: 150, minWidth: 100 },
  { key: 'acciones', defaultWidth: 200, minWidth: 120 },
]

// ── Icono ojo ─────────────────────────────────────────────────────────────────

const EyeIcon = ({ open }) =>
  open ? (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
      <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94"/>
      <path d="M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19"/>
      <line x1="1" y1="1" x2="23" y2="23"/>
    </svg>
  ) : (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
      <circle cx="12" cy="12" r="3"/>
    </svg>
  )

// Los roles se cargan dinámicamente desde /api/roles

// ── Validaciones ──────────────────────────────────────────────────────────────

function validateCreate(f) {
  const e = {}
  if (!f.nombre.trim()) e.nombre = 'El nombre es obligatorio.'
  else if (f.nombre.trim().length < 2) e.nombre = 'Mínimo 2 caracteres.'
  else if (f.nombre.trim().length > 100) e.nombre = 'Máximo 100 caracteres.'
  if (!f.email.trim()) e.email = 'El correo es obligatorio.'
  else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.email.trim())) e.email = 'Ingresá un correo válido.'
  if (!f.password) e.password = 'La contraseña es obligatoria.'
  else if (f.password.length < 8) e.password = 'Mínimo 8 caracteres.'
  if (!f.confirmar) e.confirmar = 'Confirmá la contraseña.'
  else if (f.password && f.confirmar && f.password !== f.confirmar) e.confirmar = 'Las contraseñas no coinciden.'
  if (!f.rol) e.rol = 'Seleccioná un rol.'
  return e
}

function validateEdit(f) {
  const e = {}
  if (!f.nombre.trim()) e.nombre = 'El nombre es obligatorio.'
  else if (f.nombre.trim().length < 2) e.nombre = 'Mínimo 2 caracteres.'
  else if (f.nombre.trim().length > 100) e.nombre = 'Máximo 100 caracteres.'
  if (!f.email.trim()) e.email = 'El correo es obligatorio.'
  else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.email.trim())) e.email = 'Ingresá un correo válido.'
  if (!f.rol) e.rol = 'Seleccioná un rol.'
  return e
}

function validatePasswordReset(nueva, confirmar) {
  const e = {}
  if (!nueva) e.nueva = 'La nueva contraseña es obligatoria.'
  else if (nueva.length < 8) e.nueva = 'Mínimo 8 caracteres.'
  if (!confirmar) e.confirmar = 'Confirmá la nueva contraseña.'
  else if (nueva && confirmar && nueva !== confirmar) e.confirmar = 'Las contraseñas no coinciden.'
  return e
}

// ── Vista previa de permisos del rol seleccionado (solo lectura) ──────────────
// Los permisos se editan únicamente desde la pestaña "Roles y permisos" (se
// asignan al rol, no a la persona) — esto es solo para que el Admin vea, al
// elegir un rol, qué le va a habilitar a ese usuario sin tener que adivinar.

function RolPermisosPreview({ roles, nombreRol }) {
  const [permisos, setPermisos] = useState(null)
  const [loading, setLoading]   = useState(false)

  useEffect(() => {
    const rol = roles.find(r => r.nombreRol === nombreRol)
    if (!rol) { setPermisos(null); return }
    setLoading(true)
    api.get(`/roles/${rol.idRol}/permisos`)
      .then(res => setPermisos(res.data.permisos.filter(p => p.asignado)))
      .catch(() => setPermisos(null))
      .finally(() => setLoading(false))
  }, [nombreRol, roles])

  if (!nombreRol) return null

  return (
    <div className="um-rol-preview">
      <p className="um-rol-preview-title">Este rol incluye:</p>
      {loading ? (
        <span className="um-rol-preview-loading">Cargando permisos…</span>
      ) : !permisos || permisos.length === 0 ? (
        <span className="um-rol-preview-loading">Sin permisos asignados a este rol.</span>
      ) : (
        <ul className="um-rol-preview-list">
          {permisos.map(p => (
            <li key={p.idPermiso} title={p.nombrePermiso}>
              <span className="um-rol-preview-check">✓</span> {permisoLabel(p.nombrePermiso)}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

// ── Selector de proyectos asignados (RF13) ─────────────────────────────────────
// Administrador ve todos los proyectos sin necesidad de asignación explícita
// (ver ProyectoAccesoService.EsIrrestricto en el backend), así que este
// selector se oculta por completo cuando el rol elegido es "Administrador".

function ProyectosAsignadosField({ nombreRol, selected, onChange }) {
  const [proyectos, setProyectos] = useState(null)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (nombreRol === 'Administrador' || !nombreRol) return
    setLoading(true)
    api.get('/proyectos')
      .then(res => setProyectos(res.data))
      .catch(() => setProyectos([]))
      .finally(() => setLoading(false))
  }, [nombreRol])

  if (!nombreRol || nombreRol === 'Administrador') return null

  const toggle = (id) => {
    onChange(selected.includes(id) ? selected.filter(x => x !== id) : [...selected, id])
  }

  return (
    <div className="um-form-group">
      <label>Proyectos asignados</label>
      <p className="um-rol-preview-title" style={{ marginTop: 0 }}>
        Este usuario solo va a ver/operar los proyectos que marques acá.
      </p>
      {loading ? (
        <span className="um-rol-preview-loading">Cargando proyectos…</span>
      ) : !proyectos || proyectos.length === 0 ? (
        <span className="um-rol-preview-loading">No hay proyectos creados todavía.</span>
      ) : (
        <ul className="um-rol-preview-list" style={{ maxHeight: 180, overflowY: 'auto' }}>
          {proyectos.map(p => (
            <li key={p.id}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
                <input type="checkbox" checked={selected.includes(p.id)} onChange={() => toggle(p.id)} />
                {p.nombre}
              </label>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

// ── Modal: Crear usuario ──────────────────────────────────────────────────────

function CreateUserModal({ onClose, onSaved, roles }) {
  const [fields, setFields] = useState({ nombre: '', email: '', password: '', confirmar: '', rol: '' })
  const [proyectosAsignados, setProyectosAsignados] = useState([])
  const [errors, setErrors] = useState({})
  const [showPwd, setShowPwd] = useState(false)
  const [showConf, setShowConf] = useState(false)
  const [saving, setSaving] = useState(false)
  const [apiError, setApiError] = useState(null)
  const inputRef = useRef(null)

  useEffect(() => {
    inputRef.current?.focus()
    const handleKey = (e) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [onClose])

  const set = (k) => (e) => {
    setFields(prev => ({ ...prev, [k]: e.target.value }))
    setErrors(prev => { const n = { ...prev }; delete n[k]; return n })
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    const errs = validateCreate(fields)
    if (Object.keys(errs).length) { setErrors(errs); return }
    setSaving(true)
    setApiError(null)
    try {
      const res = await api.post('/usuarios', {
        nombre:   fields.nombre.trim(),
        email:    fields.email.trim(),
        password: fields.password,
        rol:      fields.rol,
      })
      if (fields.rol !== 'Administrador' && proyectosAsignados.length > 0) {
        await api.put(`/usuarios/${res.data.id}/proyectos`, { proyectoIds: proyectosAsignados })
      }
      onSaved('Usuario creado correctamente.')
    } catch (err) {
      setApiError(err.response?.data?.message || err.response?.data?.title || 'No se pudo crear el usuario.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="um-modal-overlay" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="um-modal" role="dialog" aria-modal="true">
        <div className="um-modal-header">
          <h2 className="um-modal-title">Nuevo usuario</h2>
          <button className="um-modal-close" onClick={onClose}>✕</button>
        </div>
        {apiError && (
          <div className="um-api-error">
            <span className="um-api-error-icon">!</span>{apiError}
          </div>
        )}
        <form onSubmit={handleSubmit} noValidate>
          <div className="um-modal-body">

            <div className={`um-form-group ${errors.nombre ? 'has-error' : ''}`}>
              <label htmlFor="cu-nombre">Nombre completo <span className="um-required">*</span></label>
              <input id="cu-nombre" ref={inputRef} type="text"
                value={fields.nombre} onChange={set('nombre')}
                placeholder="Ej: Carlos Gómez" autoComplete="off" />
              {errors.nombre && <span className="um-field-error">{errors.nombre}</span>}
            </div>

            <div className={`um-form-group ${errors.email ? 'has-error' : ''}`}>
              <label htmlFor="cu-email">Correo electrónico <span className="um-required">*</span></label>
              <input id="cu-email" type="email"
                value={fields.email} onChange={set('email')}
                placeholder="usuario@roshka.com" autoComplete="off" />
              {errors.email && <span className="um-field-error">{errors.email}</span>}
            </div>

            <div className={`um-form-group ${errors.rol ? 'has-error' : ''}`}>
              <label htmlFor="cu-rol">Rol <span className="um-required">*</span></label>
              <select id="cu-rol" value={fields.rol} onChange={set('rol')}>
                <option value="">Seleccionar rol…</option>
                {roles.map(r => <option key={r.idRol} value={r.nombreRol}>{r.nombreRol}</option>)}
              </select>
              {errors.rol && <span className="um-field-error">{errors.rol}</span>}
              <RolPermisosPreview roles={roles} nombreRol={fields.rol} />
            </div>

            <ProyectosAsignadosField nombreRol={fields.rol} selected={proyectosAsignados} onChange={setProyectosAsignados} />

            <div className={`um-form-group ${errors.password ? 'has-error' : ''}`}>
              <label htmlFor="cu-pwd">Contraseña <span className="um-required">*</span></label>
              <div className="um-input-wrap">
                <input id="cu-pwd" type={showPwd ? 'text' : 'password'}
                  value={fields.password} onChange={set('password')}
                  placeholder="Mínimo 8 caracteres" autoComplete="new-password" />
                <button type="button" className="um-btn-eye"
                  onClick={() => setShowPwd(v => !v)} aria-label="Mostrar/ocultar">
                  <EyeIcon open={showPwd} />
                </button>
              </div>
              {errors.password && <span className="um-field-error">{errors.password}</span>}
            </div>

            <div className={`um-form-group ${errors.confirmar ? 'has-error' : ''}`}>
              <label htmlFor="cu-conf">Confirmar contraseña <span className="um-required">*</span></label>
              <div className="um-input-wrap">
                <input id="cu-conf" type={showConf ? 'text' : 'password'}
                  value={fields.confirmar} onChange={set('confirmar')}
                  placeholder="Repetí la contraseña" autoComplete="new-password" />
                <button type="button" className="um-btn-eye"
                  onClick={() => setShowConf(v => !v)} aria-label="Mostrar/ocultar">
                  <EyeIcon open={showConf} />
                </button>
              </div>
              {errors.confirmar && <span className="um-field-error">{errors.confirmar}</span>}
            </div>

          </div>
          <div className="um-modal-footer">
            <button type="button" className="um-btn-cancel" onClick={onClose}>Cancelar</button>
            <button type="submit" className="um-btn-save" disabled={saving}>
              {saving ? <><span className="um-spinner" /> Creando…</> : 'Crear usuario'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ── Modal: Editar usuario ─────────────────────────────────────────────────────

function EditUserModal({ usuario, onClose, onSaved, roles }) {
  const uid = usuario.idUsuario ?? usuario.id
  const [fields, setFields] = useState({
    nombre: usuario.nombre || '',
    email:  usuario.email  || '',
    rol:    usuario.rol    || usuario.roles?.[0] || '',
  })
  const [errors, setErrors] = useState({})
  const [saving, setSaving] = useState(false)
  const [apiError, setApiError] = useState(null)
  const [proyectosAsignados, setProyectosAsignados] = useState([])
  const inputRef = useRef(null)

  useEffect(() => {
    inputRef.current?.focus()
    const handleKey = (e) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [onClose])

  useEffect(() => {
    api.get(`/usuarios/${uid}/proyectos`)
      .then(res => setProyectosAsignados(res.data.proyectoIds || []))
      .catch(() => setProyectosAsignados([]))
  }, [uid])

  const set = (k) => (e) => {
    setFields(prev => ({ ...prev, [k]: e.target.value }))
    setErrors(prev => { const n = { ...prev }; delete n[k]; return n })
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    const errs = validateEdit(fields)
    if (Object.keys(errs).length) { setErrors(errs); return }
    setSaving(true)
    setApiError(null)
    try {
      await api.put(`/usuarios/${uid}`, {
        nombre: fields.nombre.trim(),
        email:  fields.email.trim(),
        rol:    fields.rol,
      })
      if (fields.rol !== 'Administrador') {
        await api.put(`/usuarios/${uid}/proyectos`, { proyectoIds: proyectosAsignados })
      }
      onSaved('Usuario actualizado correctamente.')
    } catch (err) {
      setApiError(err.response?.data?.message || err.response?.data?.title || 'No se pudo actualizar el usuario.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="um-modal-overlay" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="um-modal" role="dialog" aria-modal="true">
        <div className="um-modal-header">
          <h2 className="um-modal-title">Editar usuario</h2>
          <button className="um-modal-close" onClick={onClose}>✕</button>
        </div>
        {apiError && (
          <div className="um-api-error">
            <span className="um-api-error-icon">!</span>{apiError}
          </div>
        )}
        <form onSubmit={handleSubmit} noValidate>
          <div className="um-modal-body">

            <div className={`um-form-group ${errors.nombre ? 'has-error' : ''}`}>
              <label htmlFor="eu-nombre">Nombre completo <span className="um-required">*</span></label>
              <input id="eu-nombre" ref={inputRef} type="text"
                value={fields.nombre} onChange={set('nombre')}
                placeholder="Nombre completo" autoComplete="off" />
              {errors.nombre && <span className="um-field-error">{errors.nombre}</span>}
            </div>

            <div className={`um-form-group ${errors.email ? 'has-error' : ''}`}>
              <label htmlFor="eu-email">Correo electrónico <span className="um-required">*</span></label>
              <input id="eu-email" type="email"
                value={fields.email} onChange={set('email')}
                placeholder="usuario@roshka.com" autoComplete="off" />
              {errors.email && <span className="um-field-error">{errors.email}</span>}
            </div>

            <div className={`um-form-group ${errors.rol ? 'has-error' : ''}`}>
              <label htmlFor="eu-rol">Rol <span className="um-required">*</span></label>
              <select id="eu-rol" value={fields.rol} onChange={set('rol')}>
                <option value="">Seleccionar rol…</option>
                {roles.map(r => <option key={r.idRol} value={r.nombreRol}>{r.nombreRol}</option>)}
              </select>
              {errors.rol && <span className="um-field-error">{errors.rol}</span>}
              <RolPermisosPreview roles={roles} nombreRol={fields.rol} />
            </div>

            <ProyectosAsignadosField nombreRol={fields.rol} selected={proyectosAsignados} onChange={setProyectosAsignados} />

          </div>
          <div className="um-modal-footer">
            <button type="button" className="um-btn-cancel" onClick={onClose}>Cancelar</button>
            <button type="submit" className="um-btn-save" disabled={saving}>
              {saving ? <><span className="um-spinner" /> Guardando…</> : 'Guardar cambios'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ── Modal: Resetear contraseña ────────────────────────────────────────────────

function ResetModal({ usuario, onClose, onSaved }) {
  const uid = usuario.idUsuario ?? usuario.id
  const [nueva, setNueva] = useState('')
  const [confirmar, setConfirmar] = useState('')
  const [showNueva, setShowNueva] = useState(false)
  const [showConfirmar, setShowConfirmar] = useState(false)
  const [errors, setErrors] = useState({})
  const [saving, setSaving] = useState(false)
  const [apiError, setApiError] = useState(null)
  const inputRef = useRef(null)

  useEffect(() => {
    inputRef.current?.focus()
    const handleKey = (e) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [onClose])

  const clearError = (field) =>
    setErrors(prev => { const n = { ...prev }; delete n[field]; return n })

  const handleSubmit = async (e) => {
    e.preventDefault()
    const errs = validatePasswordReset(nueva, confirmar)
    if (Object.keys(errs).length) { setErrors(errs); return }
    setSaving(true)
    setApiError(null)
    try {
      await api.post(`/auth/reset-password/${uid}`, { newPassword: nueva })
      onSaved(`Contraseña de ${usuario.nombre} actualizada correctamente.`)
    } catch (err) {
      setApiError(err.response?.data?.message || err.response?.data?.title || 'No se pudo actualizar la contraseña.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="um-modal-overlay" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="um-modal" role="dialog" aria-modal="true">
        <div className="um-modal-header">
          <h2 className="um-modal-title">Resetear contraseña</h2>
          <button className="um-modal-close" onClick={onClose}>✕</button>
        </div>
        {apiError && (
          <div className="um-api-error">
            <span className="um-api-error-icon">!</span>{apiError}
          </div>
        )}
        <form onSubmit={handleSubmit} noValidate>
          <div className="um-modal-body">
            <p className="um-modal-user">Usuario: <strong>{usuario.nombre}</strong></p>

            <div className={`um-form-group ${errors.nueva ? 'has-error' : ''}`}>
              <label htmlFor="rm-nueva">Nueva contraseña <span className="um-required">*</span></label>
              <div className="um-input-wrap">
                <input id="rm-nueva" ref={inputRef}
                  type={showNueva ? 'text' : 'password'} value={nueva}
                  onChange={(e) => { setNueva(e.target.value); clearError('nueva') }}
                  placeholder="Mínimo 8 caracteres" autoComplete="new-password" />
                <button type="button" className="um-btn-eye"
                  onClick={() => setShowNueva(v => !v)}>
                  <EyeIcon open={showNueva} />
                </button>
              </div>
              {errors.nueva && <span className="um-field-error">{errors.nueva}</span>}
            </div>

            <div className={`um-form-group ${errors.confirmar ? 'has-error' : ''}`}>
              <label htmlFor="rm-conf">Confirmar contraseña <span className="um-required">*</span></label>
              <div className="um-input-wrap">
                <input id="rm-conf"
                  type={showConfirmar ? 'text' : 'password'} value={confirmar}
                  onChange={(e) => { setConfirmar(e.target.value); clearError('confirmar') }}
                  placeholder="Repetí la nueva contraseña" autoComplete="new-password" />
                <button type="button" className="um-btn-eye"
                  onClick={() => setShowConfirmar(v => !v)}>
                  <EyeIcon open={showConfirmar} />
                </button>
              </div>
              {errors.confirmar && <span className="um-field-error">{errors.confirmar}</span>}
            </div>
          </div>
          <div className="um-modal-footer">
            <button type="button" className="um-btn-cancel" onClick={onClose}>Cancelar</button>
            <button type="submit" className="um-btn-save" disabled={saving}>
              {saving ? <><span className="um-spinner" /> Guardando…</> : 'Actualizar contraseña'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ── UserManagement ────────────────────────────────────────────────────────────

export default function UserManagement() {
  const [usuarios,      setUsuarios]      = useState([])
  const [roles,         setRoles]         = useState([])
  const [loading,       setLoading]       = useState(true)
  const [notification,  setNotification]  = useState(null)
  const [modal,         setModal]         = useState(null)
  const [target,        setTarget]        = useState(null)
  const [togglingId,    setTogglingId]    = useState(null)
  const [tab,           setTab]           = useState('usuarios')
  const { widths, startResize } = useResizableColumns('usuarios-lista', UM_COLUMNS)
  const puedeGestionar = hasPermiso('gestionar_usuarios')

  // ── Filtro por columna + paginación ──────────────────────────────────────
  const [filtros, setFiltros] = useState({ nombre: '', correo: '', rol: '', estado: '' })
  const [pagina, setPagina] = useState(1)
  const [porPagina, setPorPagina] = useState(5)

  const setFiltro = (columna) => (valor) => {
    setFiltros((prev) => ({ ...prev, [columna]: valor }))
    setPagina(1)
  }

  const showNotification = (type, title, message) => {
    setNotification({ type, title, message })
  }

  const closeNotification = () => {
    setNotification(null)
  }

  const fetchUsuarios = async () => {
    try {
      const res = await api.get('/usuarios')
      setUsuarios(res.data)
    } catch (err) {
      const errorMsg = err.response?.data?.message || 'Error al cargar los usuarios.'
      showNotification('error', 'Error', errorMsg)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchUsuarios()
    if (puedeGestionar) {
      api.get('/roles').then(res => setRoles(res.data)).catch(() =>
        showNotification('error', 'Error', 'No se pudieron cargar los roles disponibles.'))
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const openCreate = () => { setTarget(null); setModal('create') }
  const openEdit   = (u) => { setTarget(u);   setModal('edit')   }
  const openReset  = (u) => { setTarget(u);   setModal('reset')  }
  const closeModal = () => { setModal(null);  setTarget(null)    }

  const handleSaved = (msg) => {
    closeModal()
    showNotification('success', 'Éxito', msg)
    fetchUsuarios()
  }

  const toggleEstado = async (u) => {
    const uid = u.idUsuario ?? u.id
    const esActivo = u.estado === 'activo' || u.activo === true
    setTogglingId(uid)
    try {
      await api.patch(`/usuarios/${uid}/estado`, { activo: !esActivo })
      showNotification('success', 'Éxito', `Usuario ${esActivo ? 'inactivado' : 'activado'} correctamente.`)
      fetchUsuarios()
    } catch (err) {
      const errorMsg = err.response?.data?.message || 'No se pudo cambiar el estado del usuario.'
      showNotification('error', 'Error', errorMsg)
    } finally {
      setTogglingId(null)
    }
  }

  // Substring, sin distinguir mayúsculas/minúsculas (ej. "Log" encuentra "Login").
  const contiene = (texto, filtro) =>
    !filtro || (texto || '').toLowerCase().includes(filtro.trim().toLowerCase())

  const usuariosFiltrados = usuarios
    .filter((u) => {
      const rolLabel = u.rol || u.roles?.[0] || ''
      return (
        contiene(u.nombre, filtros.nombre) &&
        contiene(u.email, filtros.correo) &&
        contiene(rolLabel, filtros.rol) &&
        contiene(u.estado, filtros.estado)
      )
    })
    // Más nuevos primero: con paginación, un usuario recien creado tiene que
    // aparecer en la primera pagina, no quedar enterrado al final de la lista.
    .sort((a, b) => new Date(b.fechaCreacion) - new Date(a.fechaCreacion))

  const totalPaginas = Math.max(1, Math.ceil(usuariosFiltrados.length / porPagina))
  const paginaSegura = Math.min(pagina, totalPaginas)
  const usuariosPagina = usuariosFiltrados.slice(
    (paginaSegura - 1) * porPagina,
    paginaSegura * porPagina
  )

  return (
    <div className="um-page">

      {/* ── Header ── */}
      <div className="um-header">
        <div className="um-header-inner">
          <div>
            <h1 className="um-title">Gestión de Usuarios</h1>
            <p className="um-subtitle">
              {loading ? '' : `${usuarios.length} usuario${usuarios.length !== 1 ? 's' : ''} registrado${usuarios.length !== 1 ? 's' : ''}`}
            </p>
          </div>
          {tab === 'usuarios' && puedeGestionar && (
            <button className="um-btn-nuevo" onClick={openCreate}>
              + Nuevo usuario
            </button>
          )}
        </div>
      </div>

      <div className="um-tabs">
        {[
          { id: 'usuarios', label: 'Usuarios' },
          ...(puedeGestionar ? [{ id: 'roles', label: 'Roles y permisos' }] : []),
        ].map(t => (
          <button
            key={t.id}
            className={`um-tab ${tab === t.id ? 'active' : ''}`}
            onClick={() => setTab(t.id)}
          >{t.label}</button>
        ))}
      </div>

      {tab === 'roles' && puedeGestionar && (
        <div className="um-body">
          <RolesPermisosPanel roles={roles} />
        </div>
      )}

      {tab === 'usuarios' && (
      <div className="um-body">

        {loading ? (
          <div className="um-loading"><span className="um-spinner" /> Cargando usuarios…</div>
        ) : (
          <>
          <div className="um-table-wrap dss-table-wrap">
            <table className="um-table dss-resizable">
              <colgroup>
                <col style={{ width: widths.nombre }} />
                <col style={{ width: widths.correo }} />
                <col style={{ width: widths.rol }} />
                <col style={{ width: widths.estado }}  />
                <col style={{ width: widths.creado }} />
                <col style={{ width: widths.acciones }} />
              </colgroup>
              <thead>
                <tr>
                  <FilterableTh onResizeStart={startResize('nombre', 100)}
                    filterValue={filtros.nombre} onFilterChange={setFiltro('nombre')}>Nombre</FilterableTh>
                  <FilterableTh onResizeStart={startResize('correo', 120)}
                    filterValue={filtros.correo} onFilterChange={setFiltro('correo')}>Correo</FilterableTh>
                  <FilterableTh onResizeStart={startResize('rol', 90)}
                    filterValue={filtros.rol} onFilterChange={setFiltro('rol')}>Rol</FilterableTh>
                  <FilterableTh onResizeStart={startResize('estado', 70)}
                    filterValue={filtros.estado} onFilterChange={setFiltro('estado')}>Estado</FilterableTh>
                  <ResizableTh onResizeStart={startResize('creado', 100)}>Creado el</ResizableTh>
                  <ResizableTh onResizeStart={startResize('acciones', 120)}>Acciones</ResizableTh>
                </tr>
              </thead>
              <tbody>
                {usuarios.length === 0 && (
                  <tr>
                    <td colSpan={6} className="um-empty-row">No hay usuarios registrados.</td>
                  </tr>
                )}
                {usuarios.length > 0 && usuariosFiltrados.length === 0 && (
                  <tr>
                    <td colSpan={6} className="um-empty-row">Ningún usuario coincide con los filtros aplicados.</td>
                  </tr>
                )}
                {usuariosPagina.map((u) => {
                  const uid     = u.idUsuario ?? u.id
                  const esActivo = u.estado === 'activo' || u.activo === true
                  const rolLabel = u.rol || u.roles?.[0] || '-'
                  return (
                    <tr key={uid}>
                      <td className="um-td-name">{u.nombre}</td>
                      <td className="um-td-email">{u.email}</td>
                      <td className="um-td-rol">{rolLabel}</td>
                      <td>
                        <span className={`um-estado-badge um-estado-badge--${u.estado}`}>{u.estado}</span>
                      </td>
                      <td className="dss-td-fecha">
                        {fmtFechaCompleta(u.fechaCreacion)}
                      </td>
                      <td>
                        <div className="um-actions">
                          {puedeGestionar ? (
                            <>
                              <button className="um-btn-action um-btn-edit"
                                onClick={() => openEdit(u)} title="Editar usuario">
                                Editar
                              </button>
                              <button
                                className={`um-btn-action ${esActivo ? 'um-btn-inactivar' : 'um-btn-activar'}`}
                                onClick={() => toggleEstado(u)}
                                disabled={togglingId === uid}
                                title={esActivo ? 'Inactivar usuario' : 'Activar usuario'}
                              >
                                {togglingId === uid ? '…' : esActivo ? 'Inactivar' : 'Activar'}
                              </button>
                              <button className="um-btn-action um-btn-reset"
                                onClick={() => openReset(u)} title="Resetear contraseña">
                                Contraseña
                              </button>
                            </>
                          ) : (
                            <span className="um-readonly-note">Solo lectura</span>
                          )}
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>

          <Pagination
            page={paginaSegura}
            totalPages={totalPaginas}
            onPageChange={setPagina}
            pageSize={porPagina}
            onPageSizeChange={(n) => { setPorPagina(n); setPagina(1) }}
            totalItems={usuariosFiltrados.length}
          />
          </>
        )}
      </div>
      )}

      {modal === 'create' && (
        <CreateUserModal onClose={closeModal} onSaved={handleSaved} roles={roles} showNotification={showNotification} />
      )}
      {modal === 'edit' && target && (
        <EditUserModal usuario={target} onClose={closeModal} onSaved={handleSaved} roles={roles} showNotification={showNotification} />
      )}
      {modal === 'reset' && target && (
        <ResetModal usuario={target} onClose={closeModal} onSaved={handleSaved} showNotification={showNotification} />
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
