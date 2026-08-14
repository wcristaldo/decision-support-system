import { useState, useEffect, useCallback } from 'react'
import api from '../services/api'
import NotificationModal from '../components/NotificationModal'
import PaymentModal from '../components/PaymentModal'
import '../styles/Suscripcion.css'

// ── Íconos SVG inline ────────────────────────────────────────────────────────

const IcCheck = () => (
  <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="2.2">
    <polyline points="4 10 8 14 16 6" />
  </svg>
)
const IcX = () => (
  <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="2.2">
    <line x1="5" y1="5" x2="15" y2="15" /><line x1="15" y1="5" x2="5" y2="15" />
  </svg>
)
const IcCrown = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
    <path d="M2 18h20L19 8l-5 5-2-6-2 6-5-5z" />
  </svg>
)
const IcShield = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
    <path d="M12 2l7 3v6c0 5-3.5 9.5-7 11-3.5-1.5-7-6-7-11V5z" />
    <polyline points="9 12 11 14 15 10" />
  </svg>
)
const IcAlert = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
    <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"/>
    <line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/>
  </svg>
)
const IcMail = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
    <rect x="2" y="4" width="20" height="16" rx="2"/>
    <polyline points="2,4 12,13 22,4"/>
  </svg>
)
const IcCalendar = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
    <rect x="3" y="4" width="18" height="18" rx="2"/>
    <line x1="3" y1="10" x2="21" y2="10"/>
    <line x1="8" y1="2" x2="8" y2="6"/>
    <line x1="16" y1="2" x2="16" y2="6"/>
  </svg>
)

// ── Helpers ──────────────────────────────────────────────────────────────────

const formatGs = (n) =>
  new Intl.NumberFormat('es-PY', { style: 'currency', currency: 'PYG', maximumFractionDigits: 0 }).format(n)

const fmtFecha = (f) =>
  f ? new Date(f).toLocaleDateString('es-PY', { day: '2-digit', month: '2-digit', year: 'numeric' }) : '—'

const fmtFechaLarga = (f) =>
  f ? new Date(f).toLocaleDateString('es-PY', { day: '2-digit', month: 'long', year: 'numeric' }) : '—'

const pct = (v, max) => (max == null ? 0 : Math.min(100, (v / max) * 100))

// ── Componente principal ─────────────────────────────────────────────────────

export default function Suscripcion() {
  const [planes, setPlanes]           = useState([])
  const [actual, setActual]           = useState(null)
  const [pagos, setPagos]             = useState([])
  const [loading, setLoading]         = useState(true)
  const [error, setError]             = useState(null)
  const [tab, setTab]                 = useState('estado')
  const [procesando, setProcesando]   = useState(false)
  const [msg, setMsg]                 = useState(null)
  const [modal, setModal]             = useState(null)
  // Modal de selección de pasarela
  const [payModal, setPayModal]       = useState(null)   // { id, nombre, precioMensual }

  // ── Historial: filtros + paginación ──────────────────────────────────────
  const [paginaActual, setPaginaActual] = useState(1)
  const [porPagina, setPorPagina]       = useState(10)
  const [filtroEstado, setFiltroEstado] = useState('')
  const [filtroPlan, setFiltroPlan]     = useState('')
  const [filtroDesde, setFiltroDesde]   = useState('')
  const [filtroHasta, setFiltroHasta]   = useState('')

  const roles   = JSON.parse(localStorage.getItem('userRoles') || '[]')
  const esAdmin = roles.includes('Administrador')

  // ── Cargar datos ──────────────────────────────────────────────────────────

  const cargar = useCallback(async () => {
    setLoading(true)
    setError(null)
    const errores = []

    try {
      const r = await api.get('/suscripcion/planes')
      setPlanes(r.data)
    } catch (e) {
      errores.push(`/planes: ${e.response?.status ?? 'network'} ${e.response?.data?.message ?? e.message}`)
    }

    try {
      const r = await api.get('/suscripcion/actual')
      setActual(r.data)
    } catch (e) {
      errores.push(`/actual: ${e.response?.status ?? 'network'} ${e.response?.data?.message ?? e.message}`)
    }

    try {
      const r = await api.get('/suscripcion/pagos')
      setPagos(r.data)
    } catch (e) {
      errores.push(`/pagos: ${e.response?.status ?? 'network'} ${e.response?.data?.message ?? e.message}`)
    }

    if (errores.length > 0) setError('Errores al cargar: ' + errores.join(' | '))
    setLoading(false)
  }, [])

  useEffect(() => { cargar() }, [cargar])

  // ── Detectar retorno desde AdamsPay ──────────────────────────────────────
  useEffect(() => {
    const params = new URLSearchParams(window.location.search)
    if (params.get('intent') === 'pay-debt') {
      window.history.replaceState({}, '', '/suscripcion')
      setMsg({ tipo: 'ok', texto: 'Pago recibido por AdamsPay. Verificando estado de tu suscripción…' })
      setTab('pagos')
      setTimeout(() => cargar(), 2500)
    }
  }, [cargar])

  // ── Detectar retorno desde PayPal ─────────────────────────────────────────
  useEffect(() => {
    const params  = new URLSearchParams(window.location.search)
    const ppStatus = params.get('pp_status')
    const ppToken  = params.get('token')      // PayPal agrega ?token=ORDER_ID al retornar
    const ppPlanId = params.get('pp_planId')

    if (ppStatus === 'success' && ppToken) {
      window.history.replaceState({}, '', '/suscripcion')
      setTab('pagos')
      setMsg({ tipo: 'ok', texto: 'Confirmando pago con PayPal…' })

      api.post('/suscripcion/paypal-capture', { orderId: ppToken, planId: Number(ppPlanId) })
        .then(() => {
          setMsg({ tipo: 'ok', texto: '¡Pago PayPal confirmado! Tu suscripción está activa.' })
          cargar()
        })
        .catch((e) => {
          setModal({
            type: 'error',
            title: 'Error al confirmar el pago PayPal',
            message: e.response?.data?.message ?? 'No se pudo confirmar el pago. Contactá al soporte.'
          })
        })
    }

    if (ppStatus === 'cancel') {
      window.history.replaceState({}, '', '/suscripcion')
      setMsg({ tipo: 'error', texto: 'Pago con PayPal cancelado. Podés intentarlo de nuevo.' })
    }
  }, [cargar])

  // ── Acciones ──────────────────────────────────────────────────────────────

  const mostrarMsg = (tipo, texto) => {
    setMsg({ tipo, texto })
    setTimeout(() => setMsg(null), 7000)
  }

  // Abre el modal selector de pasarela
  const contratarPlan = (plan) => {
    setPayModal(plan)
  }

  // Pago con AdamsPay (flujo original)
  const pagarConAdamsPay = async (idPlan) => {
    setPayModal(null)
    setProcesando(true)
    try {
      const { data } = await api.post('/suscripcion/iniciar-pago', { idPlan })
      window.location.href = data.payUrl
    } catch (e) {
      setModal({
        type: 'error',
        title: 'Error al iniciar el pago con AdamsPay',
        message: e.response?.data?.message ?? 'No se pudo conectar con AdamsPay. Intentá nuevamente.'
      })
    } finally {
      setProcesando(false)
    }
  }

  // Pago con PayPal — redirige al checkout de PayPal sandbox
  const pagarConPayPal = async (idPlan) => {
    setPayModal(null)
    setProcesando(true)
    try {
      const frontendUrl = window.location.origin
      const { data } = await api.post('/suscripcion/iniciar-pago-paypal', { idPlan, frontendUrl })
      window.location.href = data.approvalUrl
    } catch (e) {
      setModal({
        type: 'error',
        title: 'Error al iniciar el pago con PayPal',
        message: e.response?.data?.message ?? 'No se pudo conectar con PayPal. Intentá nuevamente.'
      })
    } finally {
      setProcesando(false)
    }
  }

  // ── Render ────────────────────────────────────────────────────────────────

  if (loading) return <div className="sus-loading">Cargando información de suscripción…</div>
  if (error)   return <div className="sus-error"><IcAlert />{error}</div>

  const planActual = actual?.activa ? actual.plan : null
  const uso = actual?.usoActual

  // ── Historial derivado ───────────────────────────────────────────────────
  const planesHistorial  = [...new Set(pagos.map(p => p.plan).filter(Boolean))].sort()
  const estadosHistorial = [...new Set(pagos.map(p => p.estado).filter(Boolean))].sort()

  const pagosFiltrados = pagos.filter(p => {
    const okEstado = !filtroEstado || p.estado === filtroEstado
    const okPlan   = !filtroPlan   || p.plan   === filtroPlan
    const fecha    = p.fechaPago ? new Date(p.fechaPago) : null
    const okDesde  = !filtroDesde || (fecha && fecha >= new Date(filtroDesde))
    const okHasta  = !filtroHasta || (fecha && fecha <= new Date(filtroHasta + 'T23:59:59'))
    return okEstado && okPlan && okDesde && okHasta
  })

  const totalPaginas = Math.max(1, Math.ceil(pagosFiltrados.length / porPagina))
  const pagosPagina  = pagosFiltrados.slice(
    (paginaActual - 1) * porPagina,
    paginaActual * porPagina
  )

  const paginasVisibles = (() => {
    if (totalPaginas <= 7) return Array.from({ length: totalPaginas }, (_, i) => i + 1)
    const pages = [1]
    const left  = Math.max(2, paginaActual - 1)
    const right = Math.min(totalPaginas - 1, paginaActual + 1)
    if (left > 2) pages.push('…')
    for (let i = left; i <= right; i++) pages.push(i)
    if (right < totalPaginas - 1) pages.push('…')
    pages.push(totalPaginas)
    return pages
  })()

  return (
    <div className="sus-page">

      {/* ── Encabezado ── */}
      <div className="sus-header">
        <div className="sus-header-icon"><IcCrown /></div>
        <div>
          <h1 className="sus-title">Suscripción</h1>
          <p className="sus-subtitle">Gestioná tu plan Roshka DSS y los métodos de pago.</p>
        </div>
      </div>

      {/* ── Banner de notificación ── */}
      {msg && (
        <div className={`sus-banner sus-banner--${msg.tipo}`}>
          {msg.tipo === 'ok' ? <IcCheck /> : <IcAlert />}
          {msg.texto}
        </div>
      )}

      {/* ── Modal de error ── */}
      <NotificationModal
        isOpen={!!modal}
        type={modal?.type}
        title={modal?.title}
        message={modal?.message}
        onClose={() => setModal(null)}
      />

      {/* ── Modal selector de pasarela de pago ── */}
      <PaymentModal
        isOpen={!!payModal}
        plan={payModal}
        cargando={procesando}
        onClose={() => setPayModal(null)}
        onAdamsPay={pagarConAdamsPay}
        onPayPal={pagarConPayPal}
      />

      {/* ── Tabs ── */}
      <div className="sus-tabs">
        {[
          { id: 'estado', label: 'Estado actual' },
          { id: 'planes', label: 'Planes' },
          { id: 'pagos',  label: 'Historial de pagos' },
        ].map(t => (
          <button
            key={t.id}
            className={`sus-tab ${tab === t.id ? 'active' : ''}`}
            onClick={() => setTab(t.id)}
          >{t.label}</button>
        ))}
      </div>

      {/* ══ TAB: ESTADO ACTUAL ═══════════════════════════════════════════════ */}
      {tab === 'estado' && (
        <div className="sus-content">
          {!actual?.activa ? (
            <div className="sus-sin-plan">
              <IcShield />
              <h2>Sin suscripción activa</h2>
              <p>Seleccioná un plan en la pestaña <strong>Planes</strong> para activar el sistema.</p>
              <button className="sus-btn sus-btn--primary" onClick={() => setTab('planes')}>
                Ver planes disponibles
              </button>
            </div>
          ) : (
            <>
              {/* ── Tarjeta de plan ── */}
              <div className="sus-plan-hero">
                <div className="sus-plan-hero-left">
                  <span className="sus-plan-nombre">{planActual?.nombre}</span>
                  <span className={`sus-estado sus-estado--${actual.estado}`}>{actual.estado.toUpperCase()}</span>
                </div>
                <div className="sus-plan-hero-right">
                  <span className="sus-plan-precio">{formatGs(planActual?.precioMensual)}</span>
                  <span className="sus-plan-periodo">/ mes</span>
                </div>
              </div>

              {/* ── Fechas de vigencia ── */}
              {actual.fechaVencimiento && (
                <div className="sus-vigencia-row">
                  <div className="sus-vigencia-item">
                    <IcCalendar />
                    <div>
                      <span className="sus-vigencia-label">Inicio de vigencia</span>
                      <span className="sus-vigencia-val">{fmtFechaLarga(actual.fechaInicio)}</span>
                    </div>
                  </div>
                  <div className="sus-vigencia-sep" />
                  <div className="sus-vigencia-item">
                    <IcCalendar />
                    <div>
                      <span className="sus-vigencia-label">Vencimiento</span>
                      <span className="sus-vigencia-val">{fmtFechaLarga(actual.fechaVencimiento)}</span>
                    </div>
                  </div>
                  {actual.diasRestantes != null && (
                    <div className="sus-vigencia-sep" />
                  )}
                  {actual.diasRestantes != null && (
                    <div className={`sus-dias-restantes ${actual.diasRestantes <= 7 ? 'alerta' : ''}`}>
                      <span className="sus-dias-num">{actual.diasRestantes}</span>
                      <span className="sus-dias-label">días restantes</span>
                    </div>
                  )}
                </div>
              )}

              {/* ── Barras de uso ── */}
              <div className="sus-uso-grid">
                <UsoBar label="Proyectos activos"   valor={uso?.proyectos}       maximo={uso?.maxProyectos} />
                <UsoBar label="Usuarios activos"    valor={uso?.usuarios}         maximo={uso?.maxUsuarios} />
                <UsoBar label="Evaluaciones este mes" valor={uso?.evaluacionesMes} maximo={uso?.maxEvaluacionesMes} />
              </div>

              {/* ── Acción renovar ── */}
              {esAdmin && (
                <div className="sus-renovar">
                  <p>¿Querés cambiar o renovar tu plan?</p>
                  <button className="sus-btn sus-btn--secondary" onClick={() => setTab('planes')}>
                    Ver planes disponibles
                  </button>
                </div>
              )}
            </>
          )}
        </div>
      )}

      {/* ══ TAB: PLANES ══════════════════════════════════════════════════════ */}
      {tab === 'planes' && (
        <div className="sus-content">
          <div className="sus-planes-grid">
            {planes.map((p) => {
              const esCurrent = planActual?.id === p.id
              return (
                <div key={p.id} className={`sus-plan-card ${esCurrent ? 'current' : ''}`}>
                  {esCurrent && <span className="sus-plan-current-badge">Tu plan actual</span>}
                  <h2 className="sus-plan-card-nombre">{p.nombre}</h2>
                  <p className="sus-plan-card-precio">{formatGs(p.precioMensual)}<span>/mes</span></p>

                  <div className="sus-plan-limites">
                    <LimitRow label="Proyectos activos"    valor={p.limites?.maxProyectos} />
                    <LimitRow label="Usuarios"             valor={p.limites?.maxUsuarios} />
                    <LimitRow label="Evaluaciones / mes"   valor={p.limites?.maxEvaluacionesMes} />
                    <LimitRow label="Tamaño máx. archivo"  valor={p.limites?.maxTamanoArchivoMb != null ? `${p.limites.maxTamanoArchivoMb} MB` : 'Ilimitado'} />
                    <LimitRow label="Historial"            valor={p.limites?.historialDias} />
                  </div>

                  <div className="sus-plan-features">
                    <FeatRow label="Dashboard avanzado"         ok={p.funcionalidades?.dashboardAvanzado} />
                    <FeatRow label="Exportar PDF"               ok={p.funcionalidades?.exportarPdf} />
                    <FeatRow label="Exportar Excel/CSV"         ok={p.funcionalidades?.exportarExcel} />
                    <FeatRow label="Alertas por email"          ok={p.funcionalidades?.notificacionesEmail} />
                    <FeatRow label="Alertas Slack/Teams"        ok={p.funcionalidades?.notificacionesSlack} />
                    <FeatRow label="API REST pública"           ok={p.funcionalidades?.apiPublica} />
                    <FeatRow label="Integración CI/CD nativa"   ok={p.funcionalidades?.integracionCicd} />
                    <FeatRow label="Webhooks"                   ok={p.funcionalidades?.webhooks} />
                    <FeatRow label="Auditoría detallada"        ok={p.funcionalidades?.auditoriaDetallada} />
                    <FeatRow label="Soporte prioritario (24 h)" ok={p.funcionalidades?.soportePrioritario} />
                  </div>

                  {!esCurrent && (
                    <div className="sus-contratar">
                      {esAdmin ? (
                        <button
                          className="sus-btn sus-btn--primary sus-btn--full"
                          onClick={() => contratarPlan(p)}
                          disabled={procesando}
                        >
                          {procesando ? 'Procesando…' : `Contratar — ${formatGs(p.precioMensual)}/mes`}
                        </button>
                      ) : (
                        <p className="sus-nota-admin">
                          Contactá al administrador del sistema para contratar este plan.
                        </p>
                      )}
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        </div>
      )}

      {/* ══ TAB: HISTORIAL DE PAGOS ══════════════════════════════════════════ */}
      {tab === 'pagos' && (
        <div className="sus-content">
          <div className="sus-recibo-nota">
            <IcMail />
            <span>Los recibos de pago aprobados se envían automáticamente al correo del administrador.</span>
          </div>

          {pagos.length === 0 ? (
            <p className="sus-empty">Sin pagos registrados aún.</p>
          ) : (
            <>
              {/* ── Barra de filtros ── */}
              <div className="sus-hist-filtros">
                <div className="sus-hist-filtros-left">
                  <select
                    value={filtroEstado}
                    onChange={e => { setFiltroEstado(e.target.value); setPaginaActual(1) }}
                    className="sus-hist-select"
                  >
                    <option value="">Todos los estados</option>
                    {estadosHistorial.map(e => (
                      <option key={e} value={e}>
                        {e.charAt(0).toUpperCase() + e.slice(1)}
                      </option>
                    ))}
                  </select>

                  <select
                    value={filtroPlan}
                    onChange={e => { setFiltroPlan(e.target.value); setPaginaActual(1) }}
                    className="sus-hist-select"
                  >
                    <option value="">Todos los planes</option>
                    {planesHistorial.map(p => (
                      <option key={p} value={p}>{p}</option>
                    ))}
                  </select>

                  <label style={{fontSize:'0.8rem',color:'#5d6d7e',display:'flex',alignItems:'center',gap:'0.3rem'}}>
                    Desde <input type="date" value={filtroDesde} onChange={e=>{setFiltroDesde(e.target.value);setPaginaActual(1)}} className="sus-hist-select" style={{paddingLeft:'0.5rem'}} />
                  </label>
                  <label style={{fontSize:'0.8rem',color:'#5d6d7e',display:'flex',alignItems:'center',gap:'0.3rem'}}>
                    Hasta <input type="date" value={filtroHasta} onChange={e=>{setFiltroHasta(e.target.value);setPaginaActual(1)}} className="sus-hist-select" style={{paddingLeft:'0.5rem'}} />
                  </label>
                  {(filtroEstado || filtroPlan || filtroDesde || filtroHasta) && (
                    <button
                      className="sus-hist-clear"
                      onClick={() => { setFiltroEstado(''); setFiltroPlan(''); setFiltroDesde(''); setFiltroHasta(''); setPaginaActual(1) }}
                    >
                      ✕ Limpiar filtros
                    </button>
                  )}
                </div>

                <span className="sus-hist-info">
                  {pagosFiltrados.length > 0
                    ? `Mostrando ${Math.min((paginaActual - 1) * porPagina + 1, pagosFiltrados.length)}–${Math.min(paginaActual * porPagina, pagosFiltrados.length)} de ${pagosFiltrados.length} registro${pagosFiltrados.length !== 1 ? 's' : ''}`
                    : 'Sin resultados'}
                </span>
              </div>

              {pagosFiltrados.length === 0 ? (
                <p className="sus-empty">No hay pagos que coincidan con los filtros.</p>
              ) : (
                <>
                  <table className="sus-pagos-table">
                    <thead>
                      <tr>
                        <th>Fecha de pago</th>
                        <th>Plan</th>
                        <th>Monto</th>
                        <th>Estado</th>
                        <th>Vence el</th>
                        <th>Referencia</th>
                      </tr>
                    </thead>
                    <tbody>
                      {pagosPagina.map(p => (
                        <tr key={p.id}>
                          <td>{p.fechaPago ? fmtFecha(p.fechaPago) : <span className="sus-nd">—</span>}</td>
                          <td>{p.plan}</td>
                          <td><strong>{formatGs(p.monto)}</strong></td>
                          <td>
                            <span className={`sus-pago-estado sus-pago-estado--${p.estado}`}>
                              {p.estado}
                            </span>
                          </td>
                          <td>{fmtFecha(p.fechaVencimiento)}</td>
                          <td className="sus-hash">{p.referencia ?? '—'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>

                  {/* ── Paginación ── */}
                  <div className="sus-paginacion">
                    <button
                      className="sus-pag-btn"
                      onClick={() => setPaginaActual(p => Math.max(1, p - 1))}
                      disabled={paginaActual === 1}
                    >
                      ← Anterior
                    </button>

                    <div className="sus-pag-nums">
                      {paginasVisibles.map((n, i) =>
                        n === '…' ? (
                          <span key={`e${i}`} className="sus-pag-ellipsis">…</span>
                        ) : (
                          <button
                            key={n}
                            className={`sus-pag-btn sus-pag-num ${paginaActual === n ? 'active' : ''}`}
                            onClick={() => setPaginaActual(n)}
                          >
                            {n}
                          </button>
                        )
                      )}
                    </div>

                    <button
                      className="sus-pag-btn"
                      onClick={() => setPaginaActual(p => Math.min(totalPaginas, p + 1))}
                      disabled={paginaActual === totalPaginas}
                    >
                      Siguiente →
                    </button>

                    <select
                      value={porPagina}
                      onChange={e => { setPorPagina(Number(e.target.value)); setPaginaActual(1) }}
                      className="sus-hist-select sus-pag-size"
                    >
                      {[5, 10, 15, 20].map(n => (
                        <option key={n} value={n}>{n} por página</option>
                      ))}
                    </select>
                  </div>
                </>
              )}
            </>
          )}
        </div>
      )}
    </div>
  )
}

// ── Sub-componentes ───────────────────────────────────────────────────────────

function UsoBar({ label, valor, maximo }) {
  const ilimitado = maximo == null
  const p = ilimitado ? 0 : pct(valor, maximo)
  const alerta = !ilimitado && p >= 80

  return (
    <div className="sus-uso-item">
      <div className="sus-uso-label">
        <span>{label}</span>
        <span className={`sus-uso-val ${alerta ? 'alerta' : ''}`}>
          {valor ?? 0} / {ilimitado ? '∞' : maximo}
        </span>
      </div>
      {!ilimitado && (
        <div className="sus-uso-track">
          <div className={`sus-uso-fill ${alerta ? 'alerta' : ''}`} style={{ width: `${p}%` }} />
        </div>
      )}
    </div>
  )
}

function LimitRow({ label, valor }) {
  return (
    <div className="sus-limit-row">
      <span className="sus-limit-label">{label}</span>
      <span className="sus-limit-val">{valor ?? 'Ilimitado'}</span>
    </div>
  )
}

function FeatRow({ label, ok }) {
  return (
    <div className={`sus-feat-row ${ok ? 'ok' : 'no'}`}>
      <span className="sus-feat-icon">{ok ? <IcCheck /> : <IcX />}</span>
      <span>{label}</span>
    </div>
  )
}
