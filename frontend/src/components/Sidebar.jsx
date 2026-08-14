import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import '../styles/Sidebar.css'

/* ── SVG Icons ── */
const IcHome = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none"
    stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <rect x="3" y="3" width="7" height="7" rx="1"/>
    <rect x="14" y="3" width="7" height="7" rx="1"/>
    <rect x="14" y="14" width="7" height="7" rx="1"/>
    <rect x="3" y="14" width="7" height="7" rx="1"/>
  </svg>
)

const IcProyectos = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none"
    stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <rect x="2" y="7" width="20" height="14" rx="2"/>
    <path d="M16 7V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v2"/>
  </svg>
)

const IcCargar = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none"
    stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>
    <polyline points="17 8 12 3 7 8"/>
    <line x1="12" y1="3" x2="12" y2="15"/>
  </svg>
)

const IcAnalisis = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none"
    stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <line x1="18" y1="20" x2="18" y2="10"/>
    <line x1="12" y1="20" x2="12" y2="4"/>
    <line x1="6"  y1="20" x2="6"  y2="14"/>
    <line x1="2"  y1="20" x2="22" y2="20"/>
  </svg>
)

const IcSuscripcion = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none"
    stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <rect x="1" y="4" width="22" height="16" rx="2" ry="2"/>
    <line x1="1" y1="10" x2="23" y2="10"/>
  </svg>
)

const IcAdmin = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none"
    stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
  </svg>
)

const IcChevronLeft = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none"
    stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="15 18 9 12 15 6"/>
  </svg>
)

const IcLogout = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none"
    stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/>
    <polyline points="16 17 21 12 16 7"/>
    <line x1="21" y1="12" x2="9" y2="12"/>
  </svg>
)

/* ────────────────────────────────────────────────────────────────
   Nav items
   - path  → click navega directamente
   - items → click abre sub-panel
   ──────────────────────────────────────────────────────────────── */
const NAV_ITEMS = [
  { id: 'dashboard',   label: 'Inicio',       icon: <IcHome />,        path: '/' },
  { id: 'proyectos',   label: 'Proyectos',    icon: <IcProyectos />,   path: '/proyectos' },
  { id: 'cargar',      label: 'Resultados',   icon: <IcCargar />,      path: '/cargar-resultados' },
  { id: 'analisis',    label: 'Análisis',     icon: <IcAnalisis />,    path: '/analisis' },
  { id: 'suscripcion', label: 'Suscripción',  icon: <IcSuscripcion />, path: '/suscripcion' },
  {
    id: 'admin',
    label: 'Admin',
    icon: <IcAdmin />,
    adminOnly: true,
    items: [
      { label: 'Gestión de Usuarios', path: '/usuarios' },
      { label: 'Auditoría',           path: '/auditoria' },
    ],
  },
]

function Sidebar({ onLogout }) {
  const location  = useLocation()
  const navigate  = useNavigate()
  const [panelId, setPanelId] = useState(null)

  const roles   = JSON.parse(localStorage.getItem('userRoles') || '[]')
  const isAdmin = roles.includes('Administrador')

  let nombreUsuario = 'Usuario'
  try {
    const token   = localStorage.getItem('token')
    const payload = JSON.parse(atob(token.split('.')[1]))
    nombreUsuario = payload.name || 'Usuario'
  } catch { /* ignore */ }

  const handleLogout = () => {
    localStorage.removeItem('token')
    localStorage.removeItem('userRoles')
    onLogout()
    navigate('/login')
  }

  const isActive = (path) =>
    path === '/' ? location.pathname === '/' : location.pathname.startsWith(path)

  const isItemActive = (item) => {
    if (item.path)  return isActive(item.path)
    if (item.items) return item.items.some(s => isActive(s.path))
    return false
  }

  const visibleItems = NAV_ITEMS.filter(i => !i.adminOnly || isAdmin)

  const handleRailClick = (item) => {
    if (item.path) {
      setPanelId(null)
      navigate(item.path)
    } else {
      setPanelId(prev => prev === item.id ? null : item.id)
    }
  }

  const panelItem = visibleItems.find(i => i.id === panelId && i.items) ?? null

  return (
    <aside className="sidebar-wrap">

      {/* ── Rail ── */}
      <div className="sidebar-rail">
        <div className="rail-brand">
          <span className="rail-brand-badge">DSS</span>
        </div>

        <nav className="rail-nav">
          {visibleItems.map(item => (
            <button
              key={item.id}
              className={[
                'rail-btn',
                panelId === item.id    ? 'rail-btn--open'   : '',
                isItemActive(item)     ? 'rail-btn--active' : '',
              ].filter(Boolean).join(' ')}
              onClick={() => handleRailClick(item)}
              title={item.label}
            >
              <span className="rail-icon">{item.icon}</span>
              <span className="rail-label">{item.label}</span>
            </button>
          ))}
        </nav>

        <div className="rail-bottom">
          <div className="rail-user">
            <div className="rail-avatar">
              {nombreUsuario.charAt(0).toUpperCase()}
            </div>
            <span className="rail-username">
              {nombreUsuario.split(' ')[0]}
            </span>
          </div>
          <button
            className="rail-btn rail-btn--logout"
            onClick={handleLogout}
            title="Cerrar sesión"
          >
            <span className="rail-icon"><IcLogout /></span>
            <span className="rail-label">Salir</span>
          </button>
        </div>
      </div>

      {/* ── Sub-panel (solo Admin) ── */}
      {panelItem && (
        <div className="sidebar-panel">
          <div className="panel-header">
            <span className="panel-title">{panelItem.label}</span>
            <button
              className="panel-close-btn"
              onClick={() => setPanelId(null)}
              title="Cerrar"
            >
              <IcChevronLeft />
            </button>
          </div>
          <nav className="panel-nav">
            {panelItem.items.map(sub => (
              <Link
                key={sub.path}
                to={sub.path}
                className={`panel-link${isActive(sub.path) ? ' panel-link--active' : ''}`}
                onClick={() => setPanelId(null)}
              >
                {sub.label}
              </Link>
            ))}
          </nav>
        </div>
      )}

    </aside>
  )
}

export default Sidebar
