import { useEffect, useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import api from '../services/api'
import '../styles/Dashboard.css'

// ── Módulos del sistema ───────────────────────────────────────────────────────

const BASE_MODULES = [
  {
    to:    '/proyectos',
    title: 'Proyectos',
    desc:  'Gestioná los proyectos de software y sus versiones registradas en el sistema.',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <rect x="2" y="3" width="20" height="14" rx="2"/>
        <path d="M8 21h8M12 17v4"/>
      </svg>
    ),
  },
  {
    to:    '/cargar-resultados',
    title: 'Cargar resultados',
    desc:  'Registrá archivos de resultados de pruebas automatizadas para una versión específica.',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>
        <polyline points="17 8 12 3 7 8"/>
        <line x1="12" y1="3" x2="12" y2="15"/>
      </svg>
    ),
  },
  {
    to:    '/proyectos',
    title: 'Análisis y métricas',
    desc:  'Visualizá métricas de calidad, indicadores de cobertura y tasas de éxito por versión.',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <line x1="18" y1="20" x2="18" y2="10"/>
        <line x1="12" y1="20" x2="12" y2="4"/>
        <line x1="6"  y1="20" x2="6"  y2="14"/>
      </svg>
    ),
  },
  {
    to:    '/proyectos',
    title: 'Recomendaciones',
    desc:  'Consultá las recomendaciones generadas por el motor de reglas del sistema de apoyo.',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <path d="M9 11l3 3L22 4"/>
        <path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/>
      </svg>
    ),
  },
]

const ADMIN_MODULE = {
  to:    '/usuarios',
  title: 'Gestión de usuarios',
  desc:  'Administrá cuentas de usuario, asignación de roles y permisos del sistema.',
  icon: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/>
      <circle cx="9" cy="7" r="4"/>
      <path d="M23 21v-2a4 4 0 0 0-3-3.87"/>
      <path d="M16 3.13a4 4 0 0 1 0 7.75"/>
    </svg>
  ),
}

// ── Dashboard ─────────────────────────────────────────────────────────────────

function Dashboard({ onLogout }) {
  const navigate = useNavigate()
  const [usuario, setUsuario]         = useState(null)
  const [totalProyectos, setTotal]    = useState(null)
  const [suscripcion, setSuscripcion] = useState(null)
  const roles = JSON.parse(localStorage.getItem('userRoles') || '[]')
  // Según RF10: solo el Administrador accede al módulo de gestión de usuarios
  const isAdmin = roles.includes('Administrador')

  // Decode JWT
  useEffect(() => {
    const token = localStorage.getItem('token')
    if (!token) { navigate('/login'); return }
    try {
      const payload = JSON.parse(atob(token.split('.')[1]))
      setUsuario({
        nombre:   payload.name  || payload.nombre || 'Usuario',
        email:    payload.email || '',
        roles:    payload.role
          ? (Array.isArray(payload.role) ? payload.role : [payload.role])
          : [],
        permisos: payload.permission
          ? (Array.isArray(payload.permission) ? payload.permission : [payload.permission])
          : [],
      })
    } catch {
      navigate('/login')
    }
  }, [navigate])

  // Fetch total proyectos
  useEffect(() => {
    api.get('/proyectos')
      .then(res => setTotal(res.data.length))
      .catch(() => setTotal(0))
  }, [])

  // Fetch suscripción (solo admin)
  useEffect(() => {
    if (!isAdmin) return
    api.get('/suscripcion/actual')
      .then(res => setSuscripcion(res.data))
      .catch(() => setSuscripcion(null))
  }, [isAdmin])

  if (!usuario) return (
    <div className="dash-loading">
      <span className="dash-spinner" /> Cargando...
    </div>
  )

  const modules = isAdmin ? [...BASE_MODULES, ADMIN_MODULE] : BASE_MODULES

  const primerNombre = usuario.nombre?.split(' ')[0] || usuario.nombre
  const rolDisplay   = usuario.roles.length > 0 ? usuario.roles[0] : 'Usuario'

  return (
    <div className="dash-page">

      {/* ── Bienvenida ── */}
      <div className="dash-hero">
        <div className="dash-hero-inner">
          <div>
            <h1 className="dash-name">¡Bienvenido, {primerNombre}!</h1>
            <p className="dash-subtitle">
              Sistema de Apoyo a la Toma de Decisiones - Roshka S.A.
            </p>
          </div>
          <div className="dash-hero-chips">
            <span className="dash-chip">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="14" height="14">
                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/>
                <circle cx="12" cy="7" r="4"/>
              </svg>
              {rolDisplay}
            </span>
            <span className="dash-chip">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="14" height="14">
                <rect x="2" y="3" width="20" height="14" rx="2"/>
                <path d="M8 21h8M12 17v4"/>
              </svg>
              {totalProyectos !== null ? `${totalProyectos} proyecto${totalProyectos !== 1 ? 's' : ''}` : '…'}
            </span>
            <span className="dash-chip">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="14" height="14">
                <circle cx="12" cy="12" r="10"/>
                <polyline points="12 6 12 12 16 14"/>
              </svg>
              {new Date().toLocaleDateString('es-PY', { weekday: 'long', day: '2-digit', month: 'long' })}
            </span>
          </div>
        </div>
      </div>

      {/* ── Módulos ── */}
      <div className="dash-body">
        <div className="dash-section-head">
          <h2 className="dash-section-title">Módulos del sistema</h2>
          <p className="dash-section-sub">
            Accedé a cada funcionalidad desde los accesos directos a continuación.
          </p>
        </div>

        <div className="dash-grid">
          {modules.map((m) => (
            <Link to={m.to} key={m.title} className="dash-card">
              <div className="dash-card-header">
                <span className="dash-card-icon">{m.icon}</span>
                <h3 className="dash-card-title">{m.title}</h3>
              </div>
              <div className="dash-card-body">
                <p className="dash-card-desc">{m.desc}</p>
                <span className="dash-card-link">
                  Ir al módulo
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="14" height="14">
                    <path d="M5 12h14M12 5l7 7-7 7"/>
                  </svg>
                </span>
              </div>
            </Link>
          ))}
        </div>
      </div>

      {/* ── Card suscripción (admin) ── */}
      {isAdmin && suscripcion !== null && (
        <div className="dash-body" style={{ paddingTop: 0 }}>
          <div className="dash-section-head">
            <h2 className="dash-section-title">Estado de suscripción</h2>
          </div>
          <Link to="/suscripcion" className="dash-sus-card">
            <div className="dash-sus-left">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" width="28" height="28">
                <path d="M2 18h20L19 8l-5 5-2-6-2 6-5-5z" />
              </svg>
              <div>
                <p className="dash-sus-plan">
                  {suscripcion.activa ? suscripcion.plan?.nombre : 'Sin plan activo'}
                </p>
                <p className="dash-sus-detalle">
                  {suscripcion.activa
                    ? `Vence: ${suscripcion.fechaVencimiento
                        ? new Date(suscripcion.fechaVencimiento).toLocaleDateString('es-PY')
                        : '—'} · ${suscripcion.usoActual?.proyectos ?? 0}/${suscripcion.plan?.maxProyectos ?? '∞'} proyectos`
                    : 'Contratá un plan para desbloquear todas las funciones'}
                </p>
              </div>
            </div>
            <span className={`dash-sus-badge ${suscripcion.activa ? 'activa' : 'inactiva'}`}>
              {suscripcion.activa ? suscripcion.estado : 'Sin suscripción'}
            </span>
          </Link>
        </div>
      )}

      <footer className="dash-footer">
        © 2026 Roshka S.A. - Proyecto de Tesis UNIDA
      </footer>
    </div>
  )
}

export default Dashboard
