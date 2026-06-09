import { Link, useNavigate } from 'react-router-dom'
import '../styles/Navigation.css'

function Navigation({ onLogout }) {
  const navigate = useNavigate()
  const roles = JSON.parse(localStorage.getItem('userRoles') || '[]')
  const isAdmin = roles.includes('ADMIN') || roles.includes('Administrador')

  const handleLogout = () => {
    localStorage.removeItem('token')
    localStorage.removeItem('userRoles')
    onLogout()
    navigate('/login')
  }

  return (
    <nav className="navbar">
      <div className="navbar-container">
        <Link to="/" className="navbar-brand">
          <span className="brand-icon">📊</span>
          Sistema de Apoyo a Decisiones
        </Link>

        <ul className="nav-menu">
          <li className="nav-item">
            <Link to="/" className="nav-link">Dashboard</Link>
          </li>
          <li className="nav-item">
            <Link to="/proyectos" className="nav-link">Proyectos</Link>
          </li>
          <li className="nav-item">
            <Link to="/cargar-resultados" className="nav-link">Cargar Resultados</Link>
          </li>
          {isAdmin && (
            <li className="nav-item">
              <Link to="/usuarios" className="nav-link">Gestión de Usuarios</Link>
            </li>
          )}
          <li className="nav-item">
            <button onClick={handleLogout} className="nav-logout">Cerrar Sesión</button>
          </li>
        </ul>
      </div>
    </nav>
  )
}

export default Navigation
