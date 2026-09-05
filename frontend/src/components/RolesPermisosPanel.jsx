import { useState, useEffect, useCallback, Fragment } from 'react'
import api from '../services/api'
import NotificationModal from './NotificationModal'
import '../styles/RolesPermisosPanel.css'

/**
 * RF14: matriz de roles y permisos. Los permisos se asignan al ROL, no a
 * usuarios individuales (RBAC — Sandhu et al., 1996, ya citado en la tesis).
 * Un usuario obtiene sus permisos únicamente a través del rol que tiene
 * asignado en la pestaña "Usuarios".
 */
function RolesPermisosPanel({ roles }) {
  const [rolSeleccionado, setRolSeleccionado] = useState(null)
  const [permisos, setPermisos]     = useState([])
  const [seleccionados, setSeleccionados] = useState(new Set())
  const [loading, setLoading]       = useState(false)
  const [guardando, setGuardando]   = useState(false)
  const [notification, setNotification] = useState(null)

  useEffect(() => {
    if (roles.length > 0 && rolSeleccionado == null) {
      setRolSeleccionado(roles[0].idRol)
    }
  }, [roles, rolSeleccionado])

  const cargarPermisos = useCallback(async (idRol) => {
    setLoading(true)
    try {
      const { data } = await api.get(`/roles/${idRol}/permisos`)
      setPermisos(data.permisos)
      setSeleccionados(new Set(data.permisos.filter(p => p.asignado).map(p => p.idPermiso)))
    } catch (err) {
      setNotification({ type: 'error', title: 'Error', message: err.response?.data?.message || 'No se pudieron cargar los permisos del rol.' })
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (rolSeleccionado != null) cargarPermisos(rolSeleccionado)
  }, [rolSeleccionado, cargarPermisos])

  const toggle = (idPermiso) => {
    setSeleccionados(prev => {
      const next = new Set(prev)
      if (next.has(idPermiso)) next.delete(idPermiso)
      else next.add(idPermiso)
      return next
    })
  }

  const guardar = async () => {
    const rol = roles.find(r => r.idRol === rolSeleccionado)
    const confirmado = window.confirm(
      `¿Guardar los permisos del rol "${rol?.nombreRol || ''}"? El cambio se aplica de inmediato a todos los usuarios con ese rol, incluso si ya tienen la sesión abierta.`
    )
    if (!confirmado) return

    setGuardando(true)
    try {
      const { data } = await api.put(`/roles/${rolSeleccionado}/permisos`, {
        permisoIds: Array.from(seleccionados),
      })
      setNotification({ type: 'success', title: 'Guardado', message: data.message })
      cargarPermisos(rolSeleccionado)
    } catch (err) {
      setNotification({ type: 'error', title: 'No se pudo guardar', message: err.response?.data?.message || 'Intentá de nuevo.' })
    } finally {
      setGuardando(false)
    }
  }

  const porModulo = permisos.reduce((acc, p) => {
    const key = p.modulo || 'General'
    if (!acc[key]) acc[key] = []
    acc[key].push(p)
    return acc
  }, {})

  return (
    <div className="rp-wrap">
      <p className="rp-hint">
        Los permisos se asignan al <strong>rol</strong>, no a cada usuario — así, cambiar un permiso actualiza a todos
        los usuarios que tengan ese rol de inmediato, incluso si ya tienen la sesión abierta.
      </p>

      <div className="rp-role-selector">
        {roles.map(r => (
          <button
            key={r.idRol}
            className={`rp-role-btn ${rolSeleccionado === r.idRol ? 'active' : ''}`}
            onClick={() => setRolSeleccionado(r.idRol)}
          >
            {r.nombreRol}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="rp-loading"><span className="um-spinner" /> Cargando permisos…</div>
      ) : (
        <>
          <div className="rp-list-card dss-table-wrap">
            <table className="rp-table">
              <thead>
                <tr>
                  <th className="rp-col-check"></th>
                  <th>Permiso</th>
                  <th>Descripción</th>
                </tr>
              </thead>
              <tbody>
                {Object.entries(porModulo).map(([modulo, items]) => (
                  <Fragment key={modulo}>
                    <tr className="rp-modulo-row">
                      <td colSpan={3}>{modulo}</td>
                    </tr>
                    {items.map(p => (
                      <tr key={p.idPermiso}>
                        <td className="rp-col-check">
                          <input
                            type="checkbox"
                            checked={seleccionados.has(p.idPermiso)}
                            onChange={() => toggle(p.idPermiso)}
                          />
                        </td>
                        <td className="rp-nombre-cell">{p.nombrePermiso}</td>
                        <td className="rp-desc-cell">{p.descripcion || '—'}</td>
                      </tr>
                    ))}
                  </Fragment>
                ))}
              </tbody>
            </table>
          </div>

          <button className="rp-guardar-btn" onClick={guardar} disabled={guardando}>
            {guardando ? 'Guardando…' : 'Guardar cambios'}
          </button>
        </>
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

export default RolesPermisosPanel
