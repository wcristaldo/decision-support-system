import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import '../styles/Dashboard.css'

function Dashboard({ onLogout }) {
  const navigate = useNavigate()
  const [usuario, setUsuario] = useState(null)

  useEffect(() => {
    const token = localStorage.getItem('token')
    if (!token) {
      navigate('/login')
    } else {
      try {
        const payload = JSON.parse(atob(token.split('.')[1]))
        setUsuario({
          nombre: payload.name,
          email: payload.email,
          roles: payload.role ? (Array.isArray(payload.role) ? payload.role : [payload.role]) : [],
          permisos: payload.permission ? (Array.isArray(payload.permission) ? payload.permission : [payload.permission]) : []
        })
      } catch (err) {
        console.error('Error:', err)
        navigate('/login')
      }
    }
  }, [navigate])

  const handleLogout = () => {
    localStorage.removeItem('token')
    onLogout()
    navigate('/login')
  }

  if (!usuario) return <div className="dashboard-loading">Cargando...</div>

  return (
    <div className="dashboard-page">
      <header className="dashboard-header">
        <div className="header-content">
          <h1>🎯 DSS</h1>
          <div className="user-section">
            <span>{usuario.nombre}</span>
            <button onClick={handleLogout} className="btn-logout">Cerrar Sesión</button>
          </div>
        </div>
      </header>

      <div className="dashboard-container">
        <main className="main-content">
          <h2>¡Bienvenido, {usuario.nombre}!</h2>
          <div className="user-info">
            <p><strong>Email:</strong> {usuario.email}</p>
            <p><strong>Roles:</strong> {usuario.roles.join(', ') || 'Sin roles'}</p>
            <p><strong>Permisos:</strong> {usuario.permisos.length} asignados</p>
          </div>
        </main>
      </div>
    </div>
  )
}

export default Dashboard
