import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../services/api'
import '../styles/Login.css'

/* ─────────────────────────────────────────────────────────────────────
   Contenido: Sobre el sistema
───────────────────────────────────────────────────────────────────── */
function AboutContent() {
  return (
    <div className="lm-body">
      <div className="lm-header">
        <div className="lm-header-icon">DSS</div>
        <div>
          <h2 className="lm-title">Sobre el sistema</h2>
          <p className="lm-subtitle">Decision Support System — Roshka DSS</p>
        </div>
      </div>

      <div className="lm-section">
        <h3>¿Qué es Roshka DSS?</h3>
        <p>Roshka DSS es una plataforma interna desarrollada para Roshka S.A. que asiste al equipo técnico en la evaluación de la calidad del software antes de autorizar su despliegue a producción, mediante el análisis automatizado de resultados de pruebas.</p>
      </div>

      <div className="lm-section">
        <h3>¿Cómo funciona?</h3>
        <p>El sistema analiza automáticamente los resultados de las pruebas generadas por el pipeline CI/CD y calcula métricas clave de calidad: porcentaje de pruebas exitosas, cobertura de código, tiempo de ejecución y tendencia histórica. Estas métricas se comparan contra umbrales configurables y el sistema emite una recomendación categorizada en tres estados:</p>
        <div className="lm-states">
          <div className="lm-state lm-state--green">
            <span className="lm-state-dot" />
            <div>
              <strong>Apto para despliegue</strong>
              <span>Todos los indicadores superan los umbrales configurados.</span>
            </div>
          </div>
          <div className="lm-state lm-state--yellow">
            <span className="lm-state-dot" />
            <div>
              <strong>Despliegue condicional</strong>
              <span>Uno o más indicadores están dentro del rango de tolerancia.</span>
            </div>
          </div>
          <div className="lm-state lm-state--red">
            <span className="lm-state-dot" />
            <div>
              <strong>No apto para despliegue</strong>
              <span>Indicadores críticos por debajo del umbral mínimo.</span>
            </div>
          </div>
        </div>
      </div>

      <div className="lm-section">
        <h3>Principio de diseño</h3>
        <p>El sistema asiste, no decide. La recomendación es siempre consultiva: la aprobación final del despliegue requiere la intervención y responsabilidad explícita de un Líder Técnico, quien registra su decisión junto con una justificación escrita. Este enfoque está alineado con los principios de los Sistemas de Apoyo a la Toma de Decisiones descritos por Power (2019) y con las directrices éticas de la ACM (2018).</p>
      </div>

      <div className="lm-section">
        <h3>Capacidades principales</h3>
        <div className="lm-caps-grid">
          <div className="lm-cap">
            <span className="lm-cap-icon">📊</span>
            <div>
              <strong>Análisis de métricas</strong>
              <span>Cobertura, tasa de éxito, tiempo de ejecución y tendencia histórica por versión.</span>
            </div>
          </div>
          <div className="lm-cap">
            <span className="lm-cap-icon">⚙️</span>
            <div>
              <strong>Reglas configurables</strong>
              <span>Umbrales de calidad ajustables por el administrador según los estándares del equipo.</span>
            </div>
          </div>
          <div className="lm-cap">
            <span className="lm-cap-icon">🔍</span>
            <div>
              <strong>Trazabilidad completa</strong>
              <span>Historial de decisiones con justificación escrita y registro de auditoría.</span>
            </div>
          </div>
          <div className="lm-cap">
            <span className="lm-cap-icon">🔒</span>
            <div>
              <strong>Control por roles</strong>
              <span>Acceso segmentado para Administrador, Analista QA, Líder Técnico y Gerente QA.</span>
            </div>
          </div>
        </div>
      </div>

      <div className="lm-section">
        <h3>Desarrollado por</h3>
        <p>Willian Samuel Cristaldo — Proyecto de Tesis de Grado, Universidad de la Integración de las Américas (UNIDA), Ingeniería en Sistemas, 2026.</p>
        <p style={{ marginTop: '0.4rem', color: 'var(--text-light)', fontSize: '0.875rem' }}>Empresa aliada: Roshka S.A. — Asunción, República del Paraguay.</p>
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────────────────────────────────
   Contenido: Planes
───────────────────────────────────────────────────────────────────── */
const PLANS = [
  {
    nombre: 'Básico',
    precio: 'Gs. 250.000',
    color: 'basic',
    features: [
      { label: '3 proyectos',             ok: true  },
      { label: '10 usuarios',             ok: true  },
      { label: '100 evaluaciones/mes',    ok: true  },
      { label: 'Archivos hasta 5 MB',     ok: true  },
      { label: 'Historial 30 días',       ok: true  },
      { label: 'Exportación PDF',         ok: false },
      { label: 'Dashboard avanzado',      ok: false },
      { label: 'Notificaciones email',    ok: false },
      { label: 'API pública / Webhooks',  ok: false },
      { label: 'Soporte prioritario',     ok: false },
    ],
  },
  {
    nombre: 'Profesional',
    precio: 'Gs. 500.000',
    color: 'pro',
    badge: 'Recomendado',
    features: [
      { label: '10 proyectos',            ok: true  },
      { label: '25 usuarios',             ok: true  },
      { label: '500 evaluaciones/mes',    ok: true  },
      { label: 'Archivos hasta 20 MB',    ok: true  },
      { label: 'Historial completo',      ok: true  },
      { label: 'Exportación PDF',         ok: true  },
      { label: 'Dashboard avanzado',      ok: true  },
      { label: 'Notificaciones email',    ok: true  },
      { label: 'API pública / Webhooks',  ok: false },
      { label: 'Soporte prioritario',     ok: false },
    ],
  },
  {
    nombre: 'Empresarial',
    precio: 'Gs. 900.000',
    color: 'enterprise',
    features: [
      { label: 'Proyectos ilimitados',       ok: true },
      { label: 'Usuarios ilimitados',        ok: true },
      { label: 'Evaluaciones ilimitadas',    ok: true },
      { label: 'Archivos hasta 100 MB',      ok: true },
      { label: 'Historial completo',         ok: true },
      { label: 'Exportación PDF + Excel',    ok: true },
      { label: 'Dashboard avanzado',         ok: true },
      { label: 'Notif. email + Slack',       ok: true },
      { label: 'API + CI/CD + Webhooks',     ok: true },
      { label: 'Soporte prioritario',        ok: true },
    ],
  },
]

function PlansContent() {
  return (
    <div className="lm-body lm-body--plans">
      <h2 className="lm-title">Planes disponibles</h2>
      <p className="lm-subtitle">Elegí el plan que mejor se adapte a las necesidades de tu equipo.</p>

      <div className="lm-plans-grid">
        {PLANS.map(plan => (
          <div key={plan.nombre} className={`lm-plan-card lm-plan-card--${plan.color}`}>
            {plan.badge && <span className="lm-plan-badge">{plan.badge}</span>}
            <div className="lm-plan-name">{plan.nombre}</div>
            <div className="lm-plan-price">
              <span className="lm-plan-amount">{plan.precio}</span>
              <span className="lm-plan-period">/mes</span>
            </div>
            <ul className="lm-plan-features">
              {plan.features.map((f, i) => (
                <li key={i} className={f.ok ? 'feat-ok' : 'feat-no'}>
                  <span className="feat-icon">{f.ok ? '✓' : '—'}</span>
                  <span>{f.label}</span>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>

      <p className="lm-plans-note">Para contratar o cambiar de plan, contactá al administrador del sistema.</p>
    </div>
  )
}

/* ─────────────────────────────────────────────────────────────────────
   Contenido: Política de privacidad
───────────────────────────────────────────────────────────────────── */
function PrivacyContent() {
  return (
    <div className="lm-body lm-body--privacy">
      <h2 className="lm-title">Política de Privacidad</h2>
      <p className="lm-subtitle">Roshka DSS — Versión 1.0 · Vigente desde enero de 2026</p>

      <div className="lm-privacy-meta">
        <div><strong>Responsable:</strong> Roshka S.A.</div>
        <div><strong>Domicilio:</strong> Asunción, República del Paraguay</div>
        <div><strong>Contacto:</strong> privacidad@roshka.com.py</div>
        <div><strong>Marco normativo:</strong> Ley N.º 7593/2025 de Protección de Datos Personales — Paraguay</div>
      </div>

      <div className="lm-privacy-sections">
        <section>
          <h3>1. Objeto</h3>
          <p>La presente Política de Privacidad regula el tratamiento de los datos personales de los usuarios de Roshka DSS, plataforma interna de Roshka S.A. destinada al apoyo a la toma de decisiones para el despliegue de software mediante el análisis automatizado de resultados de pruebas.</p>
          <p>Roshka S.A. actúa como responsable del tratamiento y se compromete a tratar los datos con pleno respeto a la Ley N.º 7593/2025 "De Protección de Datos Personales" de la República del Paraguay. El acceso y uso del Sistema implica la aceptación de esta Política.</p>
        </section>

        <section>
          <h3>2. Responsable del Tratamiento</h3>
          <p><strong>Empresa:</strong> Roshka S.A. &nbsp;|&nbsp; <strong>Domicilio:</strong> Asunción, República del Paraguay &nbsp;|&nbsp; <strong>Contacto:</strong> privacidad@roshka.com.py</p>
        </section>

        <section>
          <h3>3. Datos Personales Recopilados</h3>
          <p><strong>Datos de identificación y acceso:</strong> nombre completo del usuario, correo electrónico institucional, contraseña almacenada mediante hashing bcrypt (factor mínimo 10, nunca en texto plano) y rol asignado (Administrador, Analista QA, Líder Técnico o Gerente QA).</p>
          <p><strong>Registros operativos:</strong> registros de auditoría (acción realizada, fecha, hora, usuario) y decisiones de despliegue (Aprobar / Rechazar / Posponer) con su justificación escrita.</p>
          <p><strong>No se recopilan:</strong> número de documento de identidad, teléfono, fecha de nacimiento, datos biométricos ni geolocalización. Los reportes JSON contienen métricas técnicas de software y no constituyen datos personales.</p>
        </section>

        <section>
          <h3>4. Finalidad del Tratamiento</h3>
          <p>Los datos se tratan exclusivamente para gestionar el registro, autenticación e identificación de los usuarios; controlar el acceso por rol (RBAC); ejecutar el análisis automatizado de resultados de pruebas; registrar las decisiones de despliegue con trazabilidad; y mantener registros de auditoría. No se utilizarán con fines comerciales, publicitarios ni de perfilado.</p>
        </section>

        <section>
          <h3>5. Base Jurídica del Tratamiento</h3>
          <p>El tratamiento se sustenta en el consentimiento del titular (otorgado al registrarse), la ejecución de la relación laboral, el interés legítimo en garantizar trazabilidad y control interno, y el cumplimiento de obligaciones legales aplicables, conforme a la Ley N.º 7593/2025.</p>
        </section>

        <section>
          <h3>6. Período de Conservación</h3>
          <p>Los datos de usuario se conservan mientras la cuenta esté activa. Al darse de baja, se procede a la supresión o anonimización, salvo obligación legal. Los registros de auditoría y decisiones de despliegue se conservan indefinidamente como historial trazable del Sistema.</p>
        </section>

        <section>
          <h3>7. Comunicación a Terceros</h3>
          <p>Roshka S.A. no comunicará datos personales a terceros, salvo obligación legal o requerimiento de autoridad competente, necesidad técnica para el funcionamiento del Sistema (encargados sujetos a las mismas obligaciones de confidencialidad), o consentimiento expreso del titular. Los reportes JSON no serán comunicados fuera del entorno del Sistema bajo ninguna circunstancia.</p>
        </section>

        <section>
          <h3>8. Medidas de Seguridad de la Información</h3>
          <p><strong>Técnicas:</strong> autenticación JWT con tiempo de expiración, control de acceso RBAC, contraseñas con bcrypt (mínimo 10 iteraciones), comunicaciones HTTPS/TLS, registros de auditoría, despliegue en contenedores Docker aislados con Nginx como proxy inverso, y gestión de credenciales mediante variables de entorno seguras.</p>
          <p><strong>Organizativas:</strong> acceso por principio de mínimo privilegio, revisión periódica de roles y capacitación del personal en materia de protección de datos.</p>
        </section>

        <section>
          <h3>9. Derechos del Titular</h3>
          <p>Conforme a la Ley N.º 7593/2025, el titular puede ejercer los derechos de <strong>acceso</strong>, <strong>rectificación</strong>, <strong>supresión</strong>, <strong>oposición</strong>, <strong>revocación del consentimiento</strong> y <strong>portabilidad</strong>. Las solicitudes deben dirigirse a <strong>privacidad@roshka.com.py</strong>. Roshka S.A. responderá en los plazos establecidos por la ley.</p>
        </section>

        <section>
          <h3>10. Consentimiento</h3>
          <p>Al registrarse en el Sistema, el usuario es informado sobre esta Política y presta su consentimiento de forma activa mediante la aceptación de una casilla de verificación no marcada por defecto. El Sistema registra evidencia del consentimiento: usuario, fecha y hora, versión de la Política y finalidad autorizada.</p>
        </section>

        <section>
          <h3>11. Gestión de Sesiones</h3>
          <p>El Sistema no utiliza cookies de rastreo ni tecnologías de seguimiento de terceros. La sesión se gestiona mediante tokens JWT con vigencia de 24 horas, invalidados automáticamente al cerrar sesión o al expirar el período de inactividad configurado.</p>
        </section>

        <section>
          <h3>12. Actualizaciones de Esta Política</h3>
          <p>Roshka S.A. podrá actualizar esta Política cuando resulte necesario por cambios normativos, funcionales o tecnológicos. Toda modificación será publicada en el Sistema con indicación de la fecha de la nueva versión. El uso continuado implica la aceptación de los cambios.</p>
        </section>

        <section>
          <h3>13. Contacto</h3>
          <p><strong>Roshka S.A. — Área de Privacidad y Protección de Datos</strong><br />
          Correo electrónico: privacidad@roshka.com.py<br />
          Ubicación: Asunción, República del Paraguay</p>
        </section>
      </div>

      <p className="lm-privacy-footer">© 2026 Roshka S.A. — Todos los derechos reservados. Este documento tiene carácter vinculante para todos los usuarios de Roshka DSS.</p>
    </div>
  )
}

/* ─────────────────────────────────────────────────────────────────────
   Modal wrapper
───────────────────────────────────────────────────────────────────── */
function InfoModal({ type, onClose }) {
  useEffect(() => {
    const handleKey = (e) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [onClose])

  return (
    <div className="lm-overlay" onClick={onClose}>
      <div className="lm-modal" onClick={e => e.stopPropagation()}>
        <button className="lm-close" onClick={onClose} aria-label="Cerrar">×</button>
        {type === 'about'   && <AboutContent />}
        {type === 'plans'   && <PlansContent />}
        {type === 'privacy' && <PrivacyContent />}
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────────────────────────────────
   Login principal
───────────────────────────────────────────────────────────────────── */
function Login({ onLogin }) {
  const [email, setEmail]           = useState('')
  const [password, setPassword]     = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [loading, setLoading]       = useState(false)
  const [error, setError]           = useState(null)
  const [activeModal, setActiveModal] = useState(null)
  const navigate = useNavigate()

  const handleSubmit = async (e) => {
    e.preventDefault()
    setLoading(true)
    setError(null)

    try {
      const response = await api.post('/auth/login', { email, password })
      localStorage.setItem('token', response.data.token)
      localStorage.setItem('userRoles', JSON.stringify(response.data.usuario.roles))
      onLogin()
      navigate('/')
    } catch (err) {
      setError(err.response?.data?.message || 'Credenciales inválidas. Verificá tu correo y contraseña.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-page">
      {/* Burbujas decorativas */}
      <div className="bubble bubble-1" />
      <div className="bubble bubble-2" />
      <div className="bubble bubble-3" />
      <div className="bubble bubble-4" />

      <div className="login-split">
        {/* Panel izquierdo */}
        <div className="login-panel-left">
          <div className="brand">
            <div className="brand-icon">DSS</div>
            <h1>Decision Support System</h1>
            <p>Sistema de apoyo a la toma de decisiones para el despliegue de software en Roshka S.A.</p>
          </div>

          <div className="features">
            <div className="feature-item">
              <div className="feature-dot" />
              <span>Análisis de resultados de pruebas automatizadas</span>
            </div>
            <div className="feature-item">
              <div className="feature-dot" />
              <span>Métricas e indicadores de calidad por versión</span>
            </div>
            <div className="feature-item">
              <div className="feature-dot" />
              <span>Recomendaciones basadas en reglas configurables</span>
            </div>
            <div className="feature-item">
              <div className="feature-dot" />
              <span>Trazabilidad completa del proceso de decisión</span>
            </div>
            <div className="feature-item">
              <div className="feature-dot" />
              <span>Control de acceso por roles</span>
            </div>
          </div>

          {/* Nav de información */}
          <nav className="lp-nav">
            <button className="lp-nav-btn" onClick={() => setActiveModal('about')}>
              Sobre el sistema
            </button>
            <span className="lp-nav-sep">·</span>
            <button className="lp-nav-btn" onClick={() => setActiveModal('plans')}>
              Planes
            </button>
            <span className="lp-nav-sep">·</span>
            <button className="lp-nav-btn" onClick={() => setActiveModal('privacy')}>
              Política de privacidad
            </button>
          </nav>

          <div className="panel-footer">
            <span>© 2026 Roshka S.A. - Proyecto de Tesis UNIDA</span>
          </div>
        </div>

        {/* Panel derecho */}
        <div className="login-panel-right">
          <div className="login-card">
            <div className="card-header">
              <h2>Iniciar sesión</h2>
              <p>Ingresá con tu cuenta institucional</p>
            </div>

            <form onSubmit={handleSubmit} className="login-form">
              <div className="form-group">
                <label htmlFor="email">Correo electrónico</label>
                <input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  placeholder="usuario@roshka.com"
                  className="form-input"
                  autoComplete="email"
                />
              </div>

              <div className="form-group">
                <label htmlFor="password">Contraseña</label>
                <div className="input-password-wrap">
                  <input
                    id="password"
                    type={showPassword ? 'text' : 'password'}
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                    placeholder="••••••••"
                    className="form-input"
                    autoComplete="current-password"
                  />
                  <button
                    type="button"
                    className="btn-eye"
                    onClick={() => setShowPassword((v) => !v)}
                    aria-label={showPassword ? 'Ocultar contraseña' : 'Mostrar contraseña'}
                  >
                    {showPassword ? (
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
                    )}
                  </button>
                </div>
              </div>

              <div className="form-options">
                <label className="remember-me">
                  <input type="checkbox" name="remember" />
                  <span>Recordarme</span>
                </label>
                <a href="#forgot" className="forgot-link">¿Olvidaste tu contraseña?</a>
              </div>

              {error && (
                <div className="error-box">
                  <span className="error-icon">!</span>
                  <span>{error}</span>
                </div>
              )}

              <button type="submit" disabled={loading} className="btn-login">
                {loading ? (
                  <><span className="spinner" /> Verificando...</>
                ) : (
                  'Ingresar al sistema'
                )}
              </button>
            </form>
          </div>
        </div>
      </div>

      {/* Modal de información */}
      {activeModal && (
        <InfoModal type={activeModal} onClose={() => setActiveModal(null)} />
      )}
    </div>
  )
}

export default Login
