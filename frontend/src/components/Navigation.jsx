import { Link, useLocation, useNavigate } from 'react-router-dom'
import '../styles/Navigation.css'

function Navigation({ onLogout }) {
  const navigate  = useNavigate()
  const location  = useLocation()
  const roles     = JSON.parse(localStorage.getItem('userRoles') || '[]')
  const isAdmin   = roles.includes('ADMIN') || roles.includes('Administrador')

  // nombre del usuario desde JWT
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
    path === '/'
      ? location.pathname === '/'
      : location.pathname.startsWith(path)

  return (
    <nav className="navbar">
      <div className="navbar-container">
        {/* Logo */}
        <Link to="/" className="navbar-brand">
          <span className="brand-badge">DSS</span>
          <span className="brand-name">Sistema de Apoyo a Decisiones</span>
        </Link>

        {/* Links */}
        <ul className="nav-menu">
          <li>
            <Link to="/" className={`nav-link ${isActive('/') ? 'active' : ''}`}>
              Dashboard
            </Link>
          </li>
          <li>
            <Link to="/proyectos" className={`nav-link ${isActive('/proyectos') ? 'active' : ''}`}>
              Proyectos
            </Link>
          </li>
          <li>
            <Link to="/cargar-resultados" className={`nav-link ${isActive('/cargar-resultados') ? 'active' : ''}`}>
              Cargar Resultados
            </Link>
          </li>
          {isAdmin && (
            <li>
              <Link to="/usuarios" className={`nav-link ${isActive('/usuarios') ? 'active' : ''}`}>
                Gestión de Usuarios
              </Link>
            </li>
          )}
        </ul>

        {/* Usuario */}
        <div className="nav-user">
          <div className="user-avatar">{nombreUsuario.charAt(0).toUpperCase()}</div>
          <span className="user-name">{nombreUsuario}</span>
          <button onClick={handleLogout} className="btn-nav-logout">
            Cerrar sesión
          </button>
        </div>
      </div>
    </nav>
  )
}

export default Navigation
