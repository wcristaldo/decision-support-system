import { useState, useEffect } from 'react'
import api from '../services/api'
import NotificationModal from '../components/NotificationModal'
import ResizableTh from '../components/ResizableTh'
import FilterableTh from '../components/FilterableTh'
import Pagination from '../components/Pagination'
import { useResizableColumns } from '../hooks/useResizableColumns'
import '../styles/Auditoria.css'

function formatearTamano(bytes) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

const BACKUP_COLUMNS = [
  { key: 'archivo',  defaultWidth: 360, minWidth: 140 },
  { key: 'tamano',   defaultWidth: 120, minWidth: 80  },
  { key: 'fecha',    defaultWidth: 230, minWidth: 120 },
  { key: 'acciones', defaultWidth: 140, minWidth: 110 },
]

const contiene = (texto, filtro) =>
  !filtro || String(texto ?? '').toLowerCase().includes(filtro.trim().toLowerCase())

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
  const [filtros, setFiltros] = useState({ archivo: '', fecha: '' })
  const [pagina, setPagina] = useState(1)
  const [porPagina, setPorPagina] = useState(5)
  const [descargando, setDescargando] = useState(null) // nombre del archivo en descarga
  const { widths, startResize } = useResizableColumns('respaldo-historial', BACKUP_COLUMNS)

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

  const descargar = async (nombre) => {
    setDescargando(nombre)
    try {
      const res = await api.get(`/backup/descargar/${encodeURIComponent(nombre)}`, { responseType: 'blob' })
      const blobUrl = window.URL.createObjectURL(res.data)
      const a = document.createElement('a')
      a.href = blobUrl
      a.download = nombre
      document.body.appendChild(a)
      a.click()
      a.remove()
      window.URL.revokeObjectURL(blobUrl)
    } catch (err) {
      let msg = 'No se pudo descargar el respaldo.'
      if (err.response?.data instanceof Blob) {
        try { msg = JSON.parse(await err.response.data.text())?.message || msg } catch { /* no era JSON */ }
      }
      showNotification('error', 'Error', msg)
    } finally {
      setDescargando(null)
    }
  }

  const setFiltro = (columna) => (valor) => {
    setFiltros(prev => ({ ...prev, [columna]: valor }))
    setPagina(1)
  }

  const filas = historial.filter(h =>
    contiene(h.nombre, filtros.archivo) &&
    contiene(new Date(h.fecha).toLocaleString('es-PY'), filtros.fecha))
  const totalPaginas = Math.max(1, Math.ceil(filas.length / porPagina))
  const paginaSegura = Math.min(pagina, totalPaginas)
  const filasPagina = filas.slice((paginaSegura - 1) * porPagina, paginaSegura * porPagina)

  if (loading) return <div className="audit-page"><div className="audit-body">Cargando…</div></div>

  return (
    <div className="audit-page">
      <div className="audit-header">
        <div className="audit-header-inner">
          <h1 className="audit-title">Respaldo de base de datos</h1>
          <p className="audit-subtitle">
            Configurá el respaldo automático de la base de datos, generá uno cuando lo necesites y descargá los archivos.
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
          <div className="bk-acciones">
            <button className="audit-btn audit-btn-primary" onClick={guardar} disabled={guardando}>
              {guardando ? 'Guardando…' : 'Guardar configuración'}
            </button>
            <button className="audit-btn audit-btn-secondary" onClick={ejecutarAhora} disabled={ejecutando}>
              {ejecutando ? 'Generando…' : 'Ejecutar ahora'}
            </button>
          </div>
          {(() => {
            const ultima = config.fechaUltimaEjecucion ? new Date(config.fechaUltimaEjecucion) : null
            const proxima = ultima ? new Date(ultima.getTime() + config.intervaloHoras * 3600 * 1000) : null
            const vencido = config.activo && (!proxima || proxima.getTime() <= Date.now())
            return (
              <div className="bk-estado">
                <p className="audit-subtitle">
                  Último respaldo: {ultima ? ultima.toLocaleString('es-PY') : 'todavía no se generó ninguno'}
                  {' '}(automático o manual).
                </p>
                {config.activo && (
                  <p className="audit-subtitle">
                    {vencido
                      ? 'Respaldo automático pendiente: se generará en los próximos minutos si el sistema está en ejecución (se revisa cada 5 minutos).'
                      : `Próximo respaldo automático previsto: ${proxima.toLocaleString('es-PY')}.`}
                  </p>
                )}
                <p className="audit-subtitle bk-nota">
                  El respaldo automático se genera solo mientras el sistema está encendido. En un servidor que funciona las 24 horas
                  no requiere intervención; si el sistema estuvo apagado, el respaldo vencido se realiza apenas vuelve a iniciar.
                </p>
              </div>
            )
          })()}
        </div>

        <div className="audit-filters-card">
          <h2 style={{ marginTop: 0, marginBottom: '1rem' }}>Historial de respaldos</h2>
          {historial.length === 0 ? (
            <p>Todavía no se generó ningún respaldo.</p>
          ) : (
            <>
              <div className="audit-table-wrap dss-table-wrap">
                <table className="audit-table dss-resizable">
                  <colgroup>
                    <col style={{ width: widths.archivo }} />
                    <col style={{ width: widths.tamano }} />
                    <col style={{ width: widths.fecha }} />
                    <col style={{ width: widths.acciones }} />
                  </colgroup>
                  <thead>
                    <tr>
                      <FilterableTh onResizeStart={startResize('archivo', 140)}
                        filterValue={filtros.archivo} onFilterChange={setFiltro('archivo')}>Archivo</FilterableTh>
                      <ResizableTh onResizeStart={startResize('tamano', 80)}>Tamaño</ResizableTh>
                      <FilterableTh onResizeStart={startResize('fecha', 120)}
                        filterValue={filtros.fecha} onFilterChange={setFiltro('fecha')}>Fecha</FilterableTh>
                      <ResizableTh onResizeStart={startResize('acciones', 110)}>Acciones</ResizableTh>
                    </tr>
                  </thead>
                  <tbody>
                    {filas.length === 0 ? (
                      <tr>
                        <td colSpan={4} className="audit-td-sin-resultados">
                          Ningún respaldo coincide con los filtros aplicados.
                        </td>
                      </tr>
                    ) : filasPagina.map((h) => (
                      <tr key={h.nombre}>
                        <td className="audit-td-archivo">
                          <button
                            type="button"
                            className="audit-file-link"
                            title={`Descargar ${h.nombre}`}
                            onClick={() => descargar(h.nombre)}
                            disabled={descargando === h.nombre}
                          >
                            {h.nombre}
                          </button>
                        </td>
                        <td>{formatearTamano(h.tamanoBytes)}</td>
                        <td className="dss-td-fecha">{new Date(h.fecha).toLocaleString('es-PY')}</td>
                        <td>
                          <button
                            type="button"
                            className="audit-dl-btn"
                            onClick={() => descargar(h.nombre)}
                            disabled={descargando === h.nombre}
                          >
                            {descargando === h.nombre ? 'Descargando…' : 'Descargar'}
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <Pagination
                page={paginaSegura}
                totalPages={totalPaginas}
                onPageChange={setPagina}
                pageSize={porPagina}
                onPageSizeChange={(n) => { setPorPagina(n); setPagina(1) }}
                totalItems={filas.length}
              />
            </>
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
