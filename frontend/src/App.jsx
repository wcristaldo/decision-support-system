import { useState, useEffect } from 'react'
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom'
import './App.css'
import Dashboard from './pages/Dashboard'
import Login from './pages/Login'
import Proyectos from './pages/Proyectos'
import DetalleProyecto from './pages/DetalleProyecto'
import CargarResultados from './pages/CargarResultados'
import AnalisisMetricas from './pages/AnalisisMetricas'
import AnalisisVersion from './pages/AnalisisVersion'
import UserManagement from './pages/UserManagement'
import Auditoria from './pages/Auditoria'
import RespaldoBackup from './pages/RespaldoBackup'
import Suscripcion from './pages/Suscripcion'
import MiPerfil from './pages/MiPerfil'
import Sidebar from './components/Sidebar'
import { hasPermiso } from './utils/auth'

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const token = sessionStorage.getItem('token')
    setIsAuthenticated(!!token)
    setLoading(false)
  }, [])

  if (loading) return <div className="loading">Cargando...</div>

  const ProtectedRoute = ({ children }) => {
    return isAuthenticated ? children : <Navigate to="/login" />
  }

  // Defensa en profundidad: el backend siempre vuelve a validar el permiso real
  // (DbClaimsTransformation reconstruye los claims desde la base en cada
  // request), pero antes cualquier usuario autenticado podía tipear /usuarios o
  // /auditoria en la URL y ver la pantalla completa renderizarse (vacía o con
  // error) en vez de ser redirigido — inconsistente con el resto de la app.
  const PermissionRoute = ({ check, children }) => {
    if (!isAuthenticated) return <Navigate to="/login" />
    return check() ? children : <Navigate to="/" />
  }

  const handleLogin = () => {
    setIsAuthenticated(true)
  }

  const handleLogout = () => {
    sessionStorage.removeItem('token')
    setIsAuthenticated(false)
  }

  return (
    <Router>
      <div className="app-layout">
        {isAuthenticated && <Sidebar onLogout={handleLogout} />}
        <main className="app-main">
          <Routes>
            <Route path="/login" element={<Login onLogin={handleLogin} />} />
            <Route path="/" element={<ProtectedRoute><Dashboard onLogout={handleLogout} /></ProtectedRoute>} />
            <Route path="/proyectos" element={<ProtectedRoute><Proyectos /></ProtectedRoute>} />
            <Route path="/proyectos/:id" element={<ProtectedRoute><DetalleProyecto /></ProtectedRoute>} />
            <Route path="/cargar-resultados" element={<PermissionRoute check={() => hasPermiso('cargar_resultados')}><CargarResultados /></PermissionRoute>} />
            <Route path="/analisis" element={<ProtectedRoute><AnalisisMetricas /></ProtectedRoute>} />
            <Route path="/versiones/:id/analisis" element={<ProtectedRoute><AnalisisVersion /></ProtectedRoute>} />
            <Route path="/usuarios" element={<PermissionRoute check={() => hasPermiso('gestionar_usuarios') || hasPermiso('ver_usuarios')}><UserManagement /></PermissionRoute>} />
            <Route path="/auditoria" element={<PermissionRoute check={() => hasPermiso('ver_auditoria')}><Auditoria /></PermissionRoute>} />
            <Route path="/respaldo" element={<PermissionRoute check={() => hasPermiso('gestionar_usuarios')}><RespaldoBackup /></PermissionRoute>} />
            <Route path="/suscripcion" element={<ProtectedRoute><Suscripcion /></ProtectedRoute>} />
            <Route path="/mi-perfil" element={<ProtectedRoute><MiPerfil /></ProtectedRoute>} />
          </Routes>
        </main>
      </div>
    </Router>
  )
}

export default App
