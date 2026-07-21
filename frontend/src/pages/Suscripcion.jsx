import { useState, useEffect, useCallback } from 'react'
import api from '../services/api'
import '../styles/Suscripcion.css'

// ── Íconos SVG inline ───────────────────────────────────────────────────────

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
const IcCard = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
    <rect x="2" y="5" width="20" height="14" rx="2" />
    <line x1="2" y1="10" x2="22" y2="10" />
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

// ── Helpers ─────────────────────────────────────────────────────────────────

const formatGs = (n) =>
  new Intl.NumberFormat('es-PY', { style: 'currency', currency: 'PYG', maximumFractionDigits: 0 }).format(n)

const pct = (v, max) => (max == null ? 0 : Math.min(100, (v / max) * 100))

// ── Componente principal ────────────────────────────────────────────────────

export default function Suscripcion() {
  const [planes, setPlanes]       = useState([])
  const [actual, setActual]       = useState(null)
  const [pagos, setPagos]         = useState([])
  const [loading, setLoading]     = useState(true)
  const [error, setError]         = useState(null)
  const [tab, setTab]             = useState('estado') // 'estado' | 'planes' | 'pagos' | 'tarjeta'
  const [procesando, setProcesando] = useState(false)
  const [msg, setMsg]             = useState(null)     // { tipo: 'ok'|'err', texto }

  // Form para iniciar pago manual (checkout Pagopar)
  const [formPago, setFormPago] = useState({
    idPlan: '', nombre: '', email: '', documento: ''
  })

  // Form para pago recurrente
  const [formRec, setFormRec] = useState({
    nombre: '', email: '', documento: '', celular: '',
    usuarioId: 1
  })

  // Estado de iframe de tarjeta
  const [iframeUrl, setIframeUrl] = useState(null)

  // ── Cargar datos ─────────────────────────────────────────────────────────

  const cargar = useCallback(async () => {
    setLoading(true)
    setError(null)
    const errores = []

    // Llamadas independientes para no fallar todo por un error parcial
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

    if (errores.length > 0) {
      setError('Errores al cargar: ' + errores.join(' | '))
    }

    setLoading(false)
  }, [])

  useEffect(() => { cargar() }, [cargar])

  // Detectar retorno del iframe de tarjeta
  useEffect(() => {
    const params = new URLSearchParams(window.location.search)
    if (params.get('accion') === 'tarjeta-resultado') {
      const status = params.get('status')
      if (status === 'add_new_card_success') {
        setMsg({ tipo: 'ok', texto: 'Tarjeta catastrada exitosamente. Confirmando...' })
        confirmarTarjeta()
      } else if (status === 'add_new_card_fail') {
        setMsg({ tipo: 'err', texto: 'No se pudo catastrar la tarjeta. Intentá nuevamente.' })
      }
      window.history.replaceState({}, '', '/suscripcion')
    }
  }, [])

  // ── Acciones ─────────────────────────────────────────────────────────────

  const mostrarMsg = (tipo, texto) => {
    setMsg({ tipo, texto })
    setTimeout(() => setMsg(null), 6000)
  }

  const iniciarPago = async () => {
    if (!formPago.idPlan || !formPago.nombre || !formPago.email || !formPago.documento) {
      mostrarMsg('err', 'Completá todos los campos antes de continuar.')
      return
    }
    setProcesando(true)
    try {
      const { data } = await api.post('/suscripcion/iniciar-pago', {
        idPlan:            parseInt(formPago.idPlan),
        nombreComprador:   formPago.nombre,
        emailComprador:    formPago.email,
        documentoComprador: formPago.documento,
      })
      // Redirigir al checkout de Pagopar
      window.location.href = data.checkoutUrl
    } catch (e) {
      mostrarMsg('err', e.response?.data?.message ?? 'Error al iniciar el pago.')
    } finally {
      setProcesando(false)
    }
  }

  const registrarClienteYAbrirIframe = async () => {
    if (!formRec.nombre || !formRec.email || !formRec.documento || !formRec.celular) {
      mostrarMsg('err', 'Completá todos los campos.')
      return
    }
    setProcesando(true)
    try {
      // 1. Registrar cliente en Pagopar
      await api.post('/suscripcion/recurrente/registrar-cliente', {
        usuarioId:     formRec.usuarioId,
        nombreApellido: formRec.nombre,
        email:          formRec.email,
        celular:        formRec.celular,
      })
      // 2. Obtener token de iframe
      const { data } = await api.post('/suscripcion/recurrente/agregar-tarjeta', {
        usuarioId: formRec.usuarioId,
        proveedor: 'uPay',
      })
      setIframeUrl(data.iframeUrl)
      setTab('tarjeta')
    } catch (e) {
      mostrarMsg('err', e.response?.data?.message ?? 'Error al registrar cliente.')
    } finally {
      setProcesando(false)
    }
  }

  const confirmarTarjeta = async () => {
    try {
      await api.post('/suscripcion/recurrente/confirmar-tarjeta', {
        usuarioId: formRec.usuarioId
      })
      mostrarMsg('ok', 'Tarjeta confirmada. Ya podés realizar cobros recurrentes.')
      setIframeUrl(null)
      cargar()
    } catch (e) {
      mostrarMsg('err', 'Error al confirmar tarjeta.')
    }
  }

  const cobrarRecurrente = async () => {
    if (!formRec.nombre || !formRec.email || !formRec.documento) {
      mostrarMsg('err', 'Completá los datos del comprobante.')
      return
    }
    setProcesando(true)
    try {
      const { data } = await api.post('/suscripcion/recurrente/cobrar', {
        nombreComprador:    formRec.nombre,
        emailComprador:     formRec.email,
        documentoComprador: formRec.documento,
      })
      mostrarMsg('ok', data.message)
      cargar()
    } catch (e) {
      mostrarMsg('err', e.response?.data?.message ?? 'El cobro fue rechazado.')
    } finally {
      setProcesando(false)
    }
  }

  // ── Render ────────────────────────────────────────────────────────────────

  if (loading) return <div className="sus-loading">Cargando información de suscripción…</div>
  if (error)   return <div className="sus-error"><IcAlert />{error}</div>

  const planActual = actual?.activa ? actual.plan : null
  const uso = actual?.usoActual

  return (
    <div className="sus-page">
      {/* ── Encabezado ── */}
      <div className="sus-header">
        <div className="sus-header-icon"><IcCrown /></div>
        <div>
          <h1 className="sus-title">Suscripción</h1>
          <p className="sus-subtitle">
            Gestioná tu plan SAD-Roshka y los métodos de pago.
          </p>
        </div>
      </div>

      {/* ── Banner de notificación ── */}
      {msg && (
        <div className={`sus-banner sus-banner--${msg.tipo}`}>
          {msg.tipo === 'ok' ? <IcCheck /> : <IcAlert />}
          {msg.texto}
        </div>
      )}

      {/* ── Tabs ── */}
      <div className="sus-tabs">
        {[
          { id: 'estado',  label: 'Estado actual' },
          { id: 'planes',  label: 'Planes' },
          { id: 'pagos',   label: 'Historial de pagos' },
        ].map(t => (
          <button
            key={t.id}
            className={`sus-tab ${tab === t.id ? 'active' : ''}`}
            onClick={() => setTab(t.id)}
          >{t.label}</button>
        ))}
      </div>

      {/* ══ TAB: ESTADO ACTUAL ════════════════════════════════════════════ */}
      {tab === 'estado' && (
        <div className="sus-content">
          {!actual?.activa ? (
            <div className="sus-sin-plan">
              <IcShield />
              <h2>Sin suscripción activa</h2>
              <p>Seleccioná un plan en la pestaña <strong>Planes</strong> para activar el sistema.</p>
            </div>
          ) : (
            <>
              {/* ── Badge de plan ── */}
              <div className="sus-plan-badge">
                <span className="sus-plan-nombre">{planActual?.nombre}</span>
                <span className={`sus-estado sus-estado--${actual.estado}`}>{actual.estado}</span>
              </div>

              <p className="sus-plan-precio">{formatGs(planActual?.precioMensual)} / mes</p>

              {actual.fechaVencimiento && (
                <p className="sus-vencimiento">
                  Vence el {new Date(actual.fechaVencimiento).toLocaleDateString('es-PY', { day: '2-digit', month: 'long', year: 'numeric' })}
                </p>
              )}

              {/* ── Barras de uso ── */}
              <div className="sus-uso-grid">
                <UsoBar
                  label="Proyectos activos"
                  valor={uso?.proyectos}
                  maximo={uso?.maxProyectos}
                />
                <UsoBar
                  label="Usuarios activos"
                  valor={uso?.usuarios}
                  maximo={uso?.maxUsuarios}
                />
                <UsoBar
                  label="Evaluaciones este mes"
                  valor={uso?.evaluacionesMes}
                  maximo={uso?.maxEvaluacionesMes}
                />
              </div>

              {/* ── Cobro recurrente ── */}
              <div className="sus-recurrente">
                <h3 className="sus-section-title"><IcCard /> Pago recurrente con tarjeta</h3>

                {actual.tieneTarjeta ? (
                  <div className="sus-con-tarjeta">
                    <p>Hay una tarjeta catastrada. Podés renovar ahora o esperar el vencimiento.</p>
                    <div className="sus-form-row">
                      <input
                        placeholder="Nombre y apellido (comprobante)"
                        value={formRec.nombre}
                        onChange={e => setFormRec(f => ({ ...f, nombre: e.target.value }))}
                      />
                      <input
                        placeholder="Email"
                        type="email"
                        value={formRec.email}
                        onChange={e => setFormRec(f => ({ ...f, email: e.target.value }))}
                      />
                      <input
                        placeholder="Nro. cédula"
                        value={formRec.documento}
                        onChange={e => setFormRec(f => ({ ...f, documento: e.target.value }))}
                      />
                    </div>
                    <button
                      className="sus-btn sus-btn--primary"
                      onClick={cobrarRecurrente}
                      disabled={procesando}
                    >
                      {procesando ? 'Procesando…' : `Renovar (${formatGs(planActual?.precioMensual)})`}
                    </button>
                  </div>
                ) : (
                  <div className="sus-sin-tarjeta">
                    <p>Registrá una tarjeta para activar el cobro automático mensual.</p>
                    <div className="sus-form-row">
                      <input
                        placeholder="Nombre y apellido"
                        value={formRec.nombre}
                        onChange={e => setFormRec(f => ({ ...f, nombre: e.target.value }))}
                      />
                      <input
                        placeholder="Email"
                        type="email"
                        value={formRec.email}
                        onChange={e => setFormRec(f => ({ ...f, email: e.target.value }))}
                      />
                      <input
                        placeholder="Nro. cédula"
                        value={formRec.documento}
                        onChange={e => setFormRec(f => ({ ...f, documento: e.target.value }))}
                      />
                      <input
                        placeholder="Celular (ej: 0981000000)"
                        value={formRec.celular}
                        onChange={e => setFormRec(f => ({ ...f, celular: e.target.value }))}
                      />
                    </div>
                    <button
                      className="sus-btn sus-btn--secondary"
                      onClick={registrarClienteYAbrirIframe}
                      disabled={procesando}
                    >
                      {procesando ? 'Procesando…' : 'Agregar tarjeta (Pagopar)'}
                    </button>
                    <p className="sus-nota-staging">
                      🧪 <strong>Entorno de prueba (staging):</strong> tarjetas de crédito VISA/Mastercard, QR ueno y Pix disponibles en staging.
                    </p>
                  </div>
                )}
              </div>
            </>
          )}
        </div>
      )}

      {/* ══ TAB: IFRAME DE TARJETA ═══════════════════════════════════════ */}
      {tab === 'tarjeta' && (
        <div className="sus-content">
          <h3 className="sus-section-title"><IcCard /> Catastrar tarjeta en Pagopar</h3>
          {iframeUrl ? (
            <>
              <p className="sus-iframe-desc">
                Ingresá los datos de tu tarjeta. Pagopar procesará el catastro de forma segura.
              </p>
              <iframe
                src={iframeUrl}
                title="Pagopar — Agregar tarjeta"
                className="sus-iframe"
                width="100%"
                height="350"
                frameBorder="0"
              />
              <p className="sus-nota-staging">
                🧪 <strong>Staging:</strong> Usá una tarjeta de prueba provista por Pagopar.
              </p>
            </>
          ) : (
            <p>No hay formulario de tarjeta activo. Volvé al estado actual e iniciá el proceso.</p>
          )}
        </div>
      )}

      {/* ══ TAB: PLANES ══════════════════════════════════════════════════ */}
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
                    <LimitRow label="Proyectos activos"   valor={p.limites?.maxProyectos} />
                    <LimitRow label="Usuarios"            valor={p.limites?.maxUsuarios} />
                    <LimitRow label="Evaluaciones / mes"  valor={p.limites?.maxEvaluacionesMes} />
                    <LimitRow label="Tamaño máx. archivo" valor={p.limites?.maxTamanoArchivoMb != null ? `${p.limites.maxTamanoArchivoMb} MB` : 'Ilimitado'} />
                    <LimitRow label="Historial"           valor={p.limites?.historialDias} />
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
                      <h4 className="sus-contratar-title">Contratar vía Pagopar</h4>
                      <div className="sus-form-col">
                        <input
                          placeholder="Nombre y apellido"
                          value={formPago.nombre}
                          onChange={e => setFormPago(f => ({ ...f, nombre: e.target.value }))}
                        />
                        <input
                          placeholder="Email"
                          type="email"
                          value={formPago.email}
                          onChange={e => setFormPago(f => ({ ...f, email: e.target.value }))}
                        />
                        <input
                          placeholder="Nro. cédula"
                          value={formPago.documento}
                          onChange={e => setFormPago(f => ({ ...f, documento: e.target.value }))}
                        />
                        <button
                          className="sus-btn sus-btn--primary"
                          disabled={procesando}
                          onClick={() => {
                            setFormPago(f => ({ ...f, idPlan: p.id }))
                            setTimeout(iniciarPago, 50)
                          }}
                        >
                          {procesando ? 'Redirigiendo…' : `Contratar — ${formatGs(p.precioMensual)}/mes`}
                        </button>
                      </div>
                      <p className="sus-nota-staging">
                        🧪 Staging disponible: tarjeta de crédito, QR ueno, Pix.
                      </p>
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        </div>
      )}

      {/* ══ TAB: HISTORIAL DE PAGOS ══════════════════════════════════════ */}
      {tab === 'pagos' && (
        <div className="sus-content">
          {pagos.length === 0 ? (
            <p className="sus-empty">Sin pagos registrados aún.</p>
          ) : (
            <table className="sus-pagos-table">
              <thead>
                <tr>
                  <th>Fecha</th>
                  <th>Plan</th>
                  <th>Monto</th>
                  <th>Estado</th>
                  <th>Pedido Pagopar</th>
                </tr>
              </thead>
              <tbody>
                {pagos.map(p => (
                  <tr key={p.id}>
                    <td>{p.fechaCreacion ? new Date(p.fechaCreacion).toLocaleDateString('es-PY') : '—'}</td>
                    <td>{p.plan}</td>
                    <td>{formatGs(p.monto)}</td>
                    <td>
                      <span className={`sus-pago-estado sus-pago-estado--${p.estado}`}>{p.estado}</span>
                    </td>
                    <td className="sus-hash">{p.pagoparNumeroPedido ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  )
}

// ── Sub-componentes ─────────────────────────────────────────────────────────

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
          <div
            className={`sus-uso-fill ${alerta ? 'alerta' : ''}`}
            style={{ width: `${p}%` }}
          />
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
