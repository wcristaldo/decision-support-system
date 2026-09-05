import { useState, useEffect, useRef } from 'react'
import api from '../services/api'
import NotificationModal from '../components/NotificationModal'
import { decodeJwtPayload } from '../utils/jwt'
import { permisoLabel } from '../utils/permisos'
import '../styles/MiPerfil.css'

function validatePassword(fields) {
  const errors = {}
  if (!fields.actual) errors.actual = 'Ingresá tu contraseña actual.'
  if (!fields.nueva) errors.nueva = 'Ingresá la nueva contraseña.'
  else if (fields.nueva.length < 8) errors.nueva = 'Mínimo 8 caracteres.'
  if (!fields.confirmar) errors.confirmar = 'Confirmá la nueva contraseña.'
  else if (fields.nueva && fields.confirmar !== fields.nueva) errors.confirmar = 'Las contraseñas no coinciden.'
  return errors
}

// ── Fila de cambio de contraseña (expandible) ────────────────────────────────

function CambiarPasswordRow() {
  const [abierto, setAbierto] = useState(false)
  const [fields, setFields] = useState({ actual: '', nueva: '', confirmar: '' })
  const [errors, setErrors] = useState({})
  const [saving, setSaving] = useState(false)
  const [notification, setNotification] = useState(null)
  const inputRef = useRef(null)

  useEffect(() => {
    if (abierto) inputRef.current?.focus()
  }, [abierto])

  const set = (k) => (e) => {
    setFields(prev => ({ ...prev, [k]: e.target.value }))
    setErrors(prev => { const n = { ...prev }; delete n[k]; return n })
  }

  const cancelar = () => {
    setAbierto(false)
    setFields({ actual: '', nueva: '', confirmar: '' })
    setErrors({})
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    const errs = validatePassword(fields)
    if (Object.keys(errs).length) { setErrors(errs); return }
    setSaving(true)
    try {
      await api.post('/auth/change-password', {
        currentPassword: fields.actual,
        newPassword:     fields.nueva,
        confirmPassword: fields.confirmar,
      })
      setNotification({ type: 'success', title: 'Listo', message: 'Tu contraseña se actualizó correctamente.' })
      cancelar()
    } catch (err) {
      setErrors({ actual: err.response?.data?.message || 'No se pudo cambiar la contraseña.' })
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <div className="mp-row">
        <div className="mp-row-main">
          <span className="mp-row-label">Contraseña</span>
          <span className="mp-row-value">••••••••</span>
        </div>
        {!abierto && (
          <button className="mp-row-action" onClick={() => setAbierto(true)}>Cambiar</button>
        )}
      </div>

      {abierto && (
        <form className="mp-pwd-form" onSubmit={handleSubmit}>
          <div className={`mp-field ${errors.actual ? 'has-error' : ''}`}>
            <label htmlFor="mp-actual">Contraseña actual</label>
            <input id="mp-actual" ref={inputRef} type="password" value={fields.actual} onChange={set('actual')} autoComplete="current-password" />
            {errors.actual && <span className="mp-field-error">{errors.actual}</span>}
          </div>
          <div className={`mp-field ${errors.nueva ? 'has-error' : ''}`}>
            <label htmlFor="mp-nueva">Nueva contraseña</label>
            <input id="mp-nueva" type="password" value={fields.nueva} onChange={set('nueva')} placeholder="Mínimo 8 caracteres" autoComplete="new-password" />
            {errors.nueva && <span className="mp-field-error">{errors.nueva}</span>}
          </div>
          <div className={`mp-field ${errors.confirmar ? 'has-error' : ''}`}>
            <label htmlFor="mp-confirmar">Confirmar nueva contraseña</label>
            <input id="mp-confirmar" type="password" value={fields.confirmar} onChange={set('confirmar')} autoComplete="new-password" />
            {errors.confirmar && <span className="mp-field-error">{errors.confirmar}</span>}
          </div>
          <div className="mp-pwd-actions">
            <button type="button" className="mp-btn-cancel" onClick={cancelar} disabled={saving}>Cancelar</button>
            <button type="submit" className="mp-btn-save" disabled={saving}>{saving ? 'Guardando…' : 'Guardar contraseña'}</button>
          </div>
        </form>
      )}

      <NotificationModal
        isOpen={!!notification}
        type={notification?.type}
        title={notification?.title}
        message={notification?.message}
        onClose={() => setNotification(null)}
      />
    </>
  )
}

// ── Página ────────────────────────────────────────────────────────────────────

function MiPerfil() {
  const inicial = decodeJwtPayload(sessionStorage.getItem('token')) || {}
  const [datos, setDatos] = useState({
    nombre:   inicial.name || 'Usuario',
    email:    inicial.email || '',
    roles:    inicial.role ? (Array.isArray(inicial.role) ? inicial.role : [inicial.role]) : [],
    permisos: inicial.permission ? (Array.isArray(inicial.permission) ? inicial.permission : [inicial.permission]) : [],
  })
  const [cargando, setCargando] = useState(true)

  useEffect(() => {
    api.get('/auth/me')
      .then(res => setDatos({
        nombre:   res.data.nombre || inicial.name || 'Usuario',
        email:    res.data.email || inicial.email || '',
        roles:    res.data.roles,
        permisos: res.data.permisos,
      }))
      .catch(() => { /* se queda con los datos del token */ })
      .finally(() => setCargando(false))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const { nombre, email, roles, permisos } = datos

  return (
    <div className="mp-page">
      <div className="mp-header">
        <div className="mp-header-inner">
          <div className="mp-avatar">{nombre.charAt(0).toUpperCase()}</div>
          <div>
            <h1 className="mp-title">Mi perfil</h1>
            <p className="mp-subtitle">Tus datos, tu rol y qué podés hacer con él.</p>
          </div>
        </div>
      </div>

      <div className="mp-body">

        <section className="mp-section">
          <h2 className="mp-section-title">Perfil</h2>

          <div className="mp-row">
            <div className="mp-row-main">
              <span className="mp-row-label">Nombre completo</span>
              <span className="mp-row-value">{nombre}</span>
            </div>
          </div>

          <div className="mp-row">
            <div className="mp-row-main">
              <span className="mp-row-label">Correo electrónico</span>
              <span className="mp-row-value">{email || '—'}</span>
            </div>
          </div>

          <div className="mp-row">
            <div className="mp-row-main">
              <span className="mp-row-label">Rol</span>
              <span className="mp-row-value">
                {roles.length > 0
                  ? roles.map(r => <span key={r} className="mp-role-badge">{r}</span>)
                  : <span className="mp-empty">Sin rol asignado</span>}
              </span>
            </div>
          </div>

          <CambiarPasswordRow />

          <p className="mp-hint">
            El nombre, el correo y el rol los administra el Administrador del sistema. Solo tu contraseña la podés
            cambiar vos mismo desde acá.
          </p>
        </section>

        <section className="mp-section">
          <h2 className="mp-section-title">Qué podés hacer con tu rol</h2>
          {cargando ? (
            <p className="mp-empty">Cargando permisos…</p>
          ) : permisos.length > 0 ? (
            <ul className="mp-permisos-list">
              {permisos.map(p => (
                <li key={p}>
                  <span className="mp-check">✓</span> {permisoLabel(p)}
                </li>
              ))}
            </ul>
          ) : (
            <p className="mp-empty">Tu rol no tiene permisos asignados todavía.</p>
          )}
        </section>
      </div>
    </div>
  )
}

export default MiPerfil
