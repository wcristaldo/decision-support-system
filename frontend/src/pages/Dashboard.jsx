import { useEffect, useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import '../styles/Dashboard.css'

const MODULE_CARDS = [
  {
    to: '/proyectos',
    title: 'Proyectos',
    desc: 'Gestioná los proyectos de software y sus versiones registradas.',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <rect x="2" y="3" width="20" height="14" rx="2"/>
        <path d="M8 21h8M12 17v4"/>
      </svg>
    ),
  },
  {
    to: '/cargar-resultados',
    title: 'Cargar Resultados',
    desc: 'Cargá archivos de resultados de pruebas para una versión.',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>
        <polyline points="17 8 12 3 7 8"/>
        <line x1="12" y1="3" x2="12" y2="15"/>
      </svg>
    ),
  },
  {
    to: '/proyectos',
    title: 'Análisis y Métricas',
    desc: 'Visualizá métricas de calidad e indicadores por versión de proyecto.',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <line x1="18" y1="20" x2="18" y2="10"/>
        <line x1="12" y1="20" x2="12" y2="4"/>
        <line x1="6"  y1="20" x2="6"  y2="14"/>
      </svg>
    ),
  },
  {
    to: '/proyectos',
    title: 'Recomendaciones',
    desc: 'Consultá las recomendaciones generadas por el motor de reglas.',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <circle cx="12" cy="12" r="10"/>
        <path d="M12 16v-4M12 8h.01"/>
      </svg>
    ),
  },
]

function Dashboard({ onLogout }) {
  const navigate = useNavigate()
  const [usuario, setUsuario] = useState(null)
  const roles = JSON.parse(localStorage.getItem('userRoles') || '[]')
  const isAdmin = roles.includes('ADMIN') || roles.includes('Administrador')

  useEffect(() => {
    const token = localStorage.getItem('token')
    if (!token) { navigate('/login'); return }
    try {
      const payload = JSON.parse(atob(token.split('.')[1]))
      setUsuario({
        nombre:   payload.name,
        email:    payload.email,
        roles:    payload.role       ? (Array.isArray(payload.role)       ? payload.role       : [payload.role])       : [],
        permisos: payload.permission ? (Array.isArray(payload.permission) ? payload.permission : [payload.permission]) : [],
      })
    } catch {
      navigate('/login')
    }
  }, [navigate])

  if (!usuario) return <div className="dash-loading"><span className="dash-spinner" /> Cargando...</div>

  const cards = isAdmin
    ? [...MODULE_CARDS, {
        to: '/usuarios',
        title: 'Gestión de Usuarios',
        desc: 'Administrá cuentas, roles y permisos de los usuarios del sistema.',
        icon: (
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
            <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/>
            <circle cx="9" cy="7" r="4"/>
            <path d="M23 21v-2a4 4 0 0 0-3-3.87"/>
            <path d="M16 3.13a4 4 0 0 1 0 7.75"/>
          </svg>
        ),
      }]
    : MODULE_CARDS

  return (
    <div className="dash-page">
      {/* ── Burbujas decorativas ── */}
      <div className="dash-bubble dash-bubble-1" />
      <div className="dash-bubble dash-bubble-2" />

      {/* ── Bienvenida ── */}
      <section className="dash-hero">
        <div className="dash-hero-inner">
          <div className="dash-hero-text">
            <p className="dash-greeting">Buenos días,</p>
            <h1 className="dash-title">{usuario.nombre}</h1>
            <p className="dash-subtitle">
              Bienvenido al Sistema de Apoyo a la Toma de Decisiones de Roshka S.A.
            </p>
          </div>
          <div className="dash-hero-meta">
            <div className="meta-badge">
              <span className="meta-label">Correo</span>
              <span className="meta-value">{usuario.email}</span>
            </div>
            <div className="meta-badge">
              <span className="meta-label">Rol</span>
              <span className="meta-value">
                {usuario.roles.length > 0 ? usuario.roles.join(', ') : 'Sin rol asignado'}
              </span>
            </div>
            <div className="meta-badge">
              <span className="meta-label">Permisos</span>
              <span className="meta-value">{usuario.permisos.length} asignados</span>
            </div>
          </div>
        </div>
      </section>

      {/* ── Módulos ── */}
      <section className="dash-modules">
        <div className="dash-modules-inner">
          <h2 className="dash-section-title">Módulos del sistema</h2>
          <p className="dash-section-sub">
            Accedé rápidamente a cada módulo del sistema de apoyo a decisiones.
          </p>

          <div className="dash-grid">
            {cards.map((card) => (
              <Link to={card.to} key={card.title} className="dash-card">
                <div className="dash-card-icon">{card.icon}</div>
                <div className="dash-card-body">
                  <h3 className="dash-card-title">{card.title}</h3>
                  <p className="dash-card-desc">{card.desc}</p>
                </div>
                <div className="dash-card-arrow">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <path d="M5 12h14M12 5l7 7-7 7"/>
                  </svg>
                </div>
              </Link>
            ))}
          </div>
        </div>
      </section>

      <footer className="dash-footer">
        © 2026 Roshka S.A. — Proyecto de Tesis UNIDA
      </footer>
    </div>
  )
}

export default Dashboard
