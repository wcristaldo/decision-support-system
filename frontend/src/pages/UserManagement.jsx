import { useState, useEffect, useRef } from 'react'
import api from '../services/api'
import '../styles/UserManagement.css'

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

function validatePasswordReset(nueva, confirmar) {
  const errors = {}
  if (!nueva) {
    errors.nueva = 'La nueva contraseña es obligatoria.'
  } else if (nueva.length < 8) {
    errors.nueva = 'Debe tener al menos 8 caracteres.'
  } else if (nueva.length > 128) {
    errors.nueva = 'No puede superar 128 caracteres.'
  }
  if (!confirmar) {
    errors.confirmar = 'Confirmá la nueva contraseña.'
  } else if (nueva && confirmar && nueva !== confirmar) {
    errors.confirmar = 'Las contraseñas no coinciden.'
  }
  return errors
}

function ResetModal({ usuario, onClose, onSuccess }) {
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
    setErrors((prev) => { const n = { ...prev }; delete n[field]; return n })

  const handleSubmit = async (e) => {
    e.preventDefault()
    const errs = validatePasswordReset(nueva, confirmar)
    if (Object.keys(errs).length) { setErrors(errs); return }

    setSaving(true)
    setApiError(null)
    try {
      await api.post(`/auth/reset-password/${usuario.idUsuario ?? usuario.id}`, {
        newPassword: nueva,
      })
      onSuccess(`Contraseña de ${usuario.nombre} actualizada correctamente.`)
    } catch (err) {
      const msg = err.response?.data?.message
        || err.response?.data?.title
        || 'No se pudo actualizar la contraseña.'
      setApiError(msg)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="um-modal-overlay" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="um-modal" role="dialog" aria-modal="true" aria-labelledby="rm-title">
        <div className="um-modal-header">
          <h2 id="rm-title" className="um-modal-title">Resetear contraseña</h2>
          <button className="um-modal-close" onClick={onClose} aria-label="Cerrar">✕</button>
        </div>

        <form onSubmit={handleSubmit} noValidate>
          <div className="um-modal-body">
            <p className="um-modal-user">
              Usuario: <strong>{usuario.nombre}</strong>
            </p>

            {apiError && (
              <div className="um-api-error">
                <span className="um-api-error-icon">!</span>
                {apiError}
              </div>
            )}

            {/* Nueva contraseña */}
            <div className={`um-form-group ${errors.nueva ? 'has-error' : ''}`}>
              <label htmlFor="rm-nueva">
                Nueva contraseña <span className="um-required">*</span>
              </label>
              <div className="um-input-wrap">
                <input
                  id="rm-nueva"
                  ref={inputRef}
                  type={showNueva ? 'text' : 'password'}
                  value={nueva}
                  onChange={(e) => { setNueva(e.target.value); clearError('nueva') }}
                  placeholder="Mínimo 8 caracteres"
                  autoComplete="new-password"
                />
                <button
                  type="button"
                  className="um-btn-eye"
                  onClick={() => setShowNueva((v) => !v)}
                  aria-label={showNueva ? 'Ocultar' : 'Mostrar'}
                >
                  <EyeIcon open={showNueva} />
                </button>
              </div>
              {errors.nueva && <span className="um-field-error">{errors.nueva}</span>}
            </div>

            {/* Confirmar contraseña */}
            <div className={`um-form-group ${errors.confirmar ? 'has-error' : ''}`}>
              <label htmlFor="rm-confirmar">
                Confirmar contraseña <span className="um-required">*</span>
              </label>
              <div className="um-input-wrap">
                <input
                  id="rm-confirmar"
                  type={showConfirmar ? 'text' : 'password'}
                  value={confirmar}
                  onChange={(e) => { setConfirmar(e.target.value); clearError('confirmar') }}
                  placeholder="Repetí la nueva contraseña"
                  autoComplete="new-password"
                />
                <button
                  type="button"
                  className="um-btn-eye"
                  onClick={() => setShowConfirmar((v) => !v)}
                  aria-label={showConfirmar ? 'Ocultar' : 'Mostrar'}
                >
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

export default function UserManagement() {
  const [usuarios, setUsuarios] = useState([])
  const [loading, setLoading] = useState(true)
  const [toast, setToast] = useState(null)   // { msg, ok }
  const [resetTarget, setResetTarget] = useState(null)

  const showToast = (msg, ok = true) => {
    setToast({ msg, ok })
    setTimeout(() => setToast(null), 4000)
  }

  const fetchUsuarios = async () => {
    try {
      const res = await api.get('/usuarios')
      setUsuarios(res.data)
    } catch {
      showToast('Error al cargar usuarios.', false)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { fetchUsuarios() }, [])

  return (
    <div className="um-page">
      {/* Cabecera */}
      <div className="um-header">
        <div className="um-header-inner">
          <div>
            <h1 className="um-title">Gestión de Usuarios</h1>
            <p className="um-subtitle">
              {loading ? '' : `${usuarios.length} usuario${usuarios.length !== 1 ? 's' : ''} registrado${usuarios.length !== 1 ? 's' : ''}`}
            </p>
          </div>
        </div>
      </div>

      <div className="um-body">
        {/* Toast */}
        {toast && (
          <div className={`um-toast ${toast.ok ? 'um-toast-ok' : 'um-toast-err'}`}>
            <span>{toast.ok ? '✓' : '!'}</span>
            {toast.msg}
            <button onClick={() => setToast(null)}>✕</button>
          </div>
        )}

        {loading ? (
          <div className="um-loading"><span className="um-spinner" /> Cargando usuarios…</div>
        ) : (
          <div className="um-table-wrap">
            <table className="um-table">
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Correo</th>
                  <th>Estado</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {usuarios.map((u) => (
                  <tr key={u.idUsuario ?? u.id}>
                    <td className="um-td-name">{u.nombre}</td>
                    <td className="um-td-email">{u.email}</td>
                    <td>
                      <span className={`um-badge ${u.estado === 'activo' || u.activo ? 'um-badge-ok' : 'um-badge-off'}`}>
                        {u.estado === 'activo' || u.activo ? 'Activo' : 'Inactivo'}
                      </span>
                    </td>
                    <td>
                      <button
                        className="um-btn-reset"
                        onClick={() => setResetTarget(u)}
                      >
                        Resetear contraseña
                      </button>
                    </td>
                  </tr>
                ))}
                {usuarios.length === 0 && (
                  <tr>
                    <td colSpan={4} className="um-empty-row">No hay usuarios registrados.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {resetTarget && (
        <ResetModal
          usuario={resetTarget}
          onClose={() => setResetTarget(null)}
          onSuccess={(msg) => { setResetTarget(null); showToast(msg) }}
        />
      )}
    </div>
  )
}
