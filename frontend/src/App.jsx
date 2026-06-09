import { useState, useEffect } from 'react'
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom'
import './App.css'
import Dashboard from './pages/Dashboard'
import Login from './pages/Login'
import Proyectos from './pages/Proyectos'
import DetalleProyecto from './pages/DetalleProyecto'
import CargarResultados from './pages/CargarResultados'
import AnalisisVersion from './pages/AnalisisVersion'
import UserManagement from './pages/UserManagement'
import Navigation from './components/Navigation'

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const token = localStorage.getItem('token')
    setIsAuthenticated(!!token)
    setLoading(false)
  }, [])

  if (loading) return <div className="loading">Cargando...</div>

  const ProtectedRoute = ({ children }) => {
    return isAuthenticated ? children : <Navigate to="/login" />
  }

  const handleLogin = () => {
    setIsAuthenticated(true)
  }

  const handleLogout = () => {
    localStorage.removeItem('token')
    setIsAuthenticated(false)
  }

  return (
    <Router>
      {isAuthenticated && <Navigation onLogout={handleLogout} />}
      <Routes>
        <Route path="/login" element={<Login onLogin={handleLogin} />} />
        <Route path="/" element={<ProtectedRoute><Dashboard onLogout={handleLogout} /></ProtectedRoute>} />
        <Route path="/proyectos" element={<ProtectedRoute><Proyectos /></ProtectedRoute>} />
        <Route path="/proyectos/:id" element={<ProtectedRoute><DetalleProyecto /></ProtectedRoute>} />
        <Route path="/cargar-resultados" element={<ProtectedRoute><CargarResultados /></ProtectedRoute>} />
        <Route path="/versiones/:id/analisis" element={<ProtectedRoute><AnalisisVersion /></ProtectedRoute>} />
        <Route path="/usuarios" element={<ProtectedRoute><UserManagement /></ProtectedRoute>} />
      </Routes>
    </Router>
  )
}

export default App
