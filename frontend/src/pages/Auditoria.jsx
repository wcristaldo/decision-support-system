import { useState, useEffect } from 'react'
import api from '../services/api'
import NotificationModal from '../components/NotificationModal'
import ResizableTh from '../components/ResizableTh'
import { useResizableColumns } from '../hooks/useResizableColumns'
import '../styles/Auditoria.css'

const AUDIT_COLUMNS = [
  { key: 'fecha',   defaultWidth: 130, minWidth: 100 },
  { key: 'usuario', defaultWidth: 190, minWidth: 100 },
  { key: 'accion',  defaultWidth: 150, minWidth: 90  },
  { key: 'entidad', defaultWidth: 170, minWidth: 90  },
  { key: 'idReg',   defaultWidth: 190, minWidth: 80  },
  { key: 'detalle', defaultWidth: 230, minWidth: 100 },
]

function Auditoria() {
  const [registros, setRegistros] = useState([])
  const [loading, setLoading] = useState(true)
  const [notification, setNotification] = useState(null)
  const [filtros, setFiltros] = useState({
    usuario: '',
    accion: '',
    entidad: '',
    fechaDesde: '',
    fechaHasta: ''
  })
  const [pagina, setPagina] = useState(1)
  const [totalRegistros, setTotalRegistros] = useState(0)
  const { widths, startResize } = useResizableColumns('auditoria-log', AUDIT_COLUMNS)

  const TIPO_ACCION = {
    'Create': { label: 'Crear', clase: 'audit-badge-create' },
    'Update': { label: 'Actualizar', clase: 'audit-badge-update' },
    'Delete': { label: 'Eliminar', clase: 'audit-badge-delete' },
    'Login': { label: 'Login', clase: 'audit-badge-login' },
    'Password Change': { label: 'Cambiar Contraseña', clase: 'audit-badge-password' },
    'Error': { label: 'Error', clase: 'audit-badge-delete' },
  }

  const fetchRegistros = async () => {
    setLoading(true)
    try {
      const params = new URLSearchParams()
      if (filtros.usuario) params.append('usuario', filtros.usuario)
      if (filtros.accion) params.append('accion', filtros.accion)
      if (filtros.entidad) params.append('entidad', filtros.entidad)
      if (filtros.fechaDesde) params.append('fechaDesde', filtros.fechaDesde)
      if (filtros.fechaHasta) params.append('fechaHasta', filtros.fechaHasta)
      params.append('pagina', pagina)
      params.append('limite', 50)

      const res = await api.get(`/auditoria?${params.toString()}`)
      setRegistros(res.data.registros || [])
      setTotalRegistros(res.data.total || 0)
    } catch (err) {
      const errorMsg = err.response?.data?.message || 'Error al cargar el historial de auditoría.'
      showNotification('error', 'Error', errorMsg)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchRegistros()
  }, [pagina])

  const showNotification = (type, title, message) => {
    setNotification({ type, title, message })
  }

  const closeNotification = () => {
    setNotification(null)
  }

  const handleFiltroChange = (campo, valor) => {
    setFiltros(prev => ({ ...prev, [campo]: valor }))
    setPagina(1)
  }

  const handleAplicarFiltros = () => {
    setPagina(1)
    fetchRegistros()
  }

  const handleLimpiarFiltros = () => {
    setFiltros({
      usuario: '',
      accion: '',
      entidad: '',
      fechaDesde: '',
      fechaHasta: ''
    })
    setPagina(1)
  }

  const formatearFecha = (fecha) => {
    if (!fecha) return '-'
    return new Intl.DateTimeFormat('es-PY', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    }).format(new Date(fecha))
  }

  const totalPaginas = Math.ceil(totalRegistros / 50)

  const VENTANA_PAGINAS = 5
  let inicioVentana = Math.max(1, pagina - Math.floor(VENTANA_PAGINAS / 2))
  const finVentana = Math.min(totalPaginas, inicioVentana + VENTANA_PAGINAS - 1)
  inicioVentana = Math.max(1, finVentana - VENTANA_PAGINAS + 1)
  const paginasVisibles = Array.from({ length: finVentana - inicioVentana + 1 }, (_, i) => inicioVentana + i)

  return (
    <div className="audit-page">
      {/* Header */}
      <div className="audit-header">
        <div className="audit-header-inner">
          <div>
            <h1 className="audit-title">Auditoría</h1>
            <p className="audit-subtitle">Historial de actividades del sistema</p>
          </div>
        </div>
      </div>

      <div className="audit-body">
        {/* Filtros */}
        <div className="audit-filters-card">
          <div className="audit-filters-grid">
            <div className="audit-filter-group">
              <label>Usuario</label>
              <input
                type="text"
                placeholder="Filtrar por usuario..."
                value={filtros.usuario}
                onChange={(e) => handleFiltroChange('usuario', e.target.value)}
                className="audit-input"
              />
            </div>

            <div className="audit-filter-group">
              <label>Tipo de Acción</label>
              <select
                value={filtros.accion}
                onChange={(e) => handleFiltroChange('accion', e.target.value)}
                className="audit-select"
              >
                <option value="">Todas</option>
                <option value="Login">Login</option>
                <option value="Create">Crear</option>
                <option value="Update">Actualizar</option>
                <option value="Delete">Eliminar</option>
                <option value="Password Change">Cambiar Contraseña</option>
              </select>
            </div>

            <div className="audit-filter-group">
              <label>Entidad</label>
              <select
                value={filtros.entidad}
                onChange={(e) => handleFiltroChange('entidad', e.target.value)}
                className="audit-select"
              >
                <option value="">Todas</option>
                <option value="Proyecto">Proyecto</option>
                <option value="Usuario">Usuario</option>
                <option value="Version">Versión</option>
                <option value="ResultadoPrueba">Resultado de Prueba</option>
                <option value="ReglaEvaluacion">Regla de evaluación (umbral)</option>
                <option value="DecisionDespliegue">Decisión de despliegue</option>
              </select>
            </div>

            <div className="audit-filter-group">
              <label>Desde</label>
              <input
                type="datetime-local"
                value={filtros.fechaDesde}
                onChange={(e) => handleFiltroChange('fechaDesde', e.target.value)}
                className="audit-input"
              />
            </div>

            <div className="audit-filter-group">
              <label>Hasta</label>
              <input
                type="datetime-local"
                value={filtros.fechaHasta}
                onChange={(e) => handleFiltroChange('fechaHasta', e.target.value)}
                className="audit-input"
              />
            </div>
          </div>

          <div className="audit-filters-actions">
            <button className="audit-btn audit-btn-primary" onClick={handleAplicarFiltros}>
              Aplicar Filtros
            </button>
            <button className="audit-btn audit-btn-secondary" onClick={handleLimpiarFiltros}>
              Limpiar
            </button>
          </div>
        </div>

        {/* Tabla */}
        {loading ? (
          <div className="audit-loading">
            <span className="audit-spinner" /> Cargando historial...
          </div>
        ) : registros.length === 0 ? (
          <div className="audit-empty">
            <p>No hay registros de auditoría</p>
          </div>
        ) : (
          <div className="audit-table-wrap dss-table-wrap">
            <table className="audit-table dss-resizable">
              <colgroup>
                <col style={{ width: widths.fecha }} />
                <col style={{ width: widths.usuario }} />
                <col style={{ width: widths.accion }} />
                <col style={{ width: widths.entidad }} />
                <col style={{ width: widths.idReg }} />
                <col style={{ width: widths.detalle }} />
              </colgroup>
              <thead>
                <tr>
                  <ResizableTh onResizeStart={startResize('fecha', 100)}>Fecha - Hora</ResizableTh>
                  <ResizableTh onResizeStart={startResize('usuario', 100)}>Usuario</ResizableTh>
                  <ResizableTh onResizeStart={startResize('accion', 90)}>Acción</ResizableTh>
                  <ResizableTh onResizeStart={startResize('entidad', 90)}>Entidad</ResizableTh>
                  <ResizableTh onResizeStart={startResize('idReg', 80)}>ID Registro</ResizableTh>
                  <ResizableTh onResizeStart={startResize('detalle', 100)}>Detalles</ResizableTh>
                </tr>
              </thead>
              <tbody>
                {registros.map((reg, idx) => {
                  const tipoAccion = TIPO_ACCION[reg.accion] || { label: reg.accion, clase: 'audit-badge-default' }
                  return (
                    <tr key={idx}>
                      <td className="audit-td-fecha">{formatearFecha(reg.fechaEvento)}</td>
                      <td className="audit-td-usuario">{reg.usuario || reg.usuarioId || '-'}</td>
                      <td>
                        <span className={`audit-badge ${tipoAccion.clase}`}>
                          {tipoAccion.label}
                        </span>
                      </td>
                      <td className="audit-td-entidad">{reg.entidadAfectada || '-'}</td>
                      <td className="audit-td-id">{reg.idRegistroAfectado || '-'}</td>
                      <td className="audit-td-detalle">{reg.detalle || '-'}</td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}

        {/* Paginación */}
        {totalPaginas > 1 && (
          <div className="audit-pagination">
            <span className="audit-info">
              Mostrando {(pagina - 1) * 50 + 1} al {Math.min(pagina * 50, totalRegistros)} de {totalRegistros} registros
            </span>
            <div className="audit-pages">
              <button
                className="audit-page-btn"
                disabled={pagina === 1}
                onClick={() => setPagina(p => Math.max(1, p - 1))}
              >
                Anterior
              </button>
              {paginasVisibles.map(p => (
                <button
                  key={p}
                  className={`audit-page-btn ${pagina === p ? 'active' : ''}`}
                  onClick={() => setPagina(p)}
                >
                  {p}
                </button>
              ))}
              <button
                className="audit-page-btn"
                disabled={pagina === totalPaginas}
                onClick={() => setPagina(p => Math.min(totalPaginas, p + 1))}
              >
                Siguiente
              </button>
            </div>
          </div>
        )}
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

export default Auditoria
