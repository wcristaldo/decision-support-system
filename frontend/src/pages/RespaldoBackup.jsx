import { useState, useEffect } from 'react'
import api from '../services/api'
import NotificationModal from '../components/NotificationModal'
import '../styles/Auditoria.css'

function formatearTamano(bytes) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

// RNF12: "El sistema debe contar con un mecanismo de respaldo periódico de la
// base de datos, configurable por el Administrador". El backend (
// DatabaseBackupService) revisa esta configuración cada 5 minutos y dispara
// un pg_dump real cuando corresponde; esta pantalla permite configurar el
// intervalo/carpeta, dispararlo manualmente y ver el historial de respaldos.
function RespaldoBackup() {
  const [config, setConfig] = useState(null)
  const [historial, setHistorial] = useState([])
  const [loading, setLoading] = useState(true)
  const [guardando, setGuardando] = useState(false)
  const [ejecutando, setEjecutando] = useState(false)
  const [notification, setNotification] = useState(null)

  const showNotification = (type, title, message) => setNotification({ type, title, message })
  const closeNotification = () => setNotification(null)

  const cargar = async () => {
    setLoading(true)
    try {
      const [cRes, hRes] = await Promise.all([
        api.get('/backup/configuracion'),
        api.get('/backup/historial'),
      ])
      setConfig(cRes.data)
      setHistorial(hRes.data)
    } catch {
      showNotification('error', 'Error', 'No se pudo cargar la configuración de respaldo.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { cargar() }, [])

  const guardar = async () => {
    setGuardando(true)
    try {
      await api.put('/backup/configuracion', {
        intervaloHoras: config.intervaloHoras,
        carpetaDestino: config.carpetaDestino,
        activo: config.activo,
      })
      showNotification('success', 'Guardado', 'La configuración de respaldo se actualizó correctamente.')
    } catch (err) {
      showNotification('error', 'Error', err.response?.data?.message || 'No se pudo guardar la configuración.')
    } finally {
      setGuardando(false)
    }
  }

  const ejecutarAhora = async () => {
    setEjecutando(true)
    try {
      const res = await api.post('/backup/ejecutar-ahora')
      showNotification('success', 'Respaldo generado', `Se generó el archivo ${res.data.archivo}.`)
      cargar()
    } catch (err) {
      showNotification('error', 'Error', err.response?.data?.message || 'No se pudo generar el respaldo.')
    } finally {
      setEjecutando(false)
    }
  }

  if (loading) return <div className="audit-page"><div className="audit-body">Cargando…</div></div>

  return (
    <div className="audit-page">
      <div className="audit-header">
        <div className="audit-header-inner">
          <h1 className="audit-title">Respaldo de base de datos</h1>
          <p className="audit-subtitle">
            RNF12 — mecanismo de respaldo periódico configurable, para preservar la integridad de los datos históricos.
          </p>
        </div>
      </div>

      <div className="audit-body">
        <div className="audit-filters-card">
          <div className="audit-filters-grid">
            <div>
              <label>Intervalo (horas)</label>
              <input
                type="number" min="1" className="audit-input"
                value={config.intervaloHoras}
                onChange={(e) => setConfig({ ...config, intervaloHoras: parseInt(e.target.value) || 1 })}
              />
            </div>
            <div>
              <label>Carpeta destino</label>
              <input
                type="text" className="audit-input"
                value={config.carpetaDestino}
                onChange={(e) => setConfig({ ...config, carpetaDestino: e.target.value })}
              />
            </div>
            <div>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: '1.6rem' }}>
                <input
                  type="checkbox"
                  checked={config.activo}
                  onChange={(e) => setConfig({ ...config, activo: e.target.checked })}
                />
                Respaldo automático activo
              </label>
            </div>
          </div>
          <div style={{ display: 'flex', gap: 10 }}>
            <button className="audit-btn audit-btn-primary" onClick={guardar} disabled={guardando}>
              {guardando ? 'Guardando…' : 'Guardar configuración'}
            </button>
            <button className="audit-btn audit-btn-secondary" onClick={ejecutarAhora} disabled={ejecutando}>
              {ejecutando ? 'Generando…' : 'Ejecutar ahora'}
            </button>
          </div>
          {config.fechaUltimaEjecucion && (
            <p className="audit-subtitle" style={{ marginTop: 10 }}>
              Último respaldo automático: {new Date(config.fechaUltimaEjecucion).toLocaleString('es-PY')}
            </p>
          )}
        </div>

        <div className="audit-filters-card">
          <h2 style={{ marginTop: 0 }}>Historial de respaldos</h2>
          {historial.length === 0 ? (
            <p>Todavía no se generó ningún respaldo.</p>
          ) : (
            <table className="audit-table">
              <thead>
                <tr><th>Archivo</th><th>Tamaño</th><th>Fecha</th></tr>
              </thead>
              <tbody>
                {historial.map((h) => (
                  <tr key={h.nombre}>
                    <td>{h.nombre}</td>
                    <td>{formatearTamano(h.tamanoBytes)}</td>
                    <td>{new Date(h.fecha).toLocaleString('es-PY')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
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

export default RespaldoBackup
