import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import axios from 'axios'
import '../styles/Login.css'

function Login({ onLogin }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const navigate = useNavigate()

  const handleSubmit = async (e) => {
    e.preventDefault()
    setLoading(true)
    setError(null)

    try {
      const response = await axios.post('http://localhost:5000/api/auth/login', {
        email,
        password
      })

      localStorage.setItem('token', response.data.token)
      localStorage.setItem('userRoles', JSON.stringify(response.data.usuario.roles))
      onLogin()
      navigate('/')
    } catch (err) {
      setError(err.response?.data?.message || 'Error al iniciar sesión. Credenciales inválidas.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-page">
      {/* Encabezado */}
      <header className="login-header">
        <div className="header-content">
          <div className="logo-section">
            <div className="logo">✨</div>
            <div className="logo-text">
              <h1>DSS</h1>
              <p>Sistema de Apoyo a Decisiones</p>
            </div>
          </div>
          <nav className="header-nav">
            <a href="#features">Características</a>
            <a href="#docs">Documentación</a>
            <a href="#contact">Soporte</a>
          </nav>
        </div>
      </header>

      {/* Contenido Principal */}
      <div className="login-container">
        <div className="login-wrapper">
          {/* Lado Izquierdo - Información */}
          <div className="login-info">
            <h2>Decisiones de Despliegue Inteligentes</h2>
            <p>Sistema impulsado por IA que analiza resultados de pruebas y guía tus decisiones de despliegue con confianza</p>

            <ul className="features-list">
              <li>
                <span className="icon">⚡</span>
                <span>Análisis en tiempo real de resultados de pruebas</span>
              </li>
              <li>
                <span className="icon">🎯</span>
                <span>Recomendaciones inteligentes de despliegue</span>
              </li>
              <li>
                <span className="icon">🔍</span>
                <span>Trazabilidad completa de decisiones</span>
              </li>
              <li>
                <span className="icon">👥</span>
                <span>Control de acceso basado en roles</span>
              </li>
              <li>
                <span className="icon">📊</span>
                <span>Análisis avanzado e información</span>
              </li>
            </ul>

            <div className="info-box">
              <strong>Acceso a Demostración Disponible</strong>
              <p>Explora la plataforma completa con nuestra cuenta de demostración preconfigurada. Sin tarjeta de crédito requerida.</p>
            </div>
          </div>

          {/* Lado Derecho - Formulario */}
          <div className="login-form-container">
            <form onSubmit={handleSubmit} className="login-form">
              <div className="form-header">
                <h3>¡Bienvenido de Vuelta!</h3>
                <p>Inicia sesión en tu cuenta para continuar</p>
              </div>

              <div className="form-group">
                <label htmlFor="email">Correo o Usuario</label>
                <input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  placeholder="usuario@empresa.com"
                  className="form-input"
                  autoComplete="email"
                />
              </div>

              <div className="form-group">
                <label htmlFor="password">Contraseña</label>
                <input
                  id="password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  placeholder="••••••••"
                  className="form-input"
                  autoComplete="current-password"
                />
              </div>

              <div className="form-options">
                <label className="remember-me">
                  <input type="checkbox" name="remember" />
                  <span>Recuérdame</span>
                </label>
                <a href="#forgot" className="forgot-link">¿Necesitas ayuda?</a>
              </div>

              {error && (
                <div className="error-message" style={{
                  background: 'rgba(239, 68, 68, 0.15)',
                  border: '1px solid rgba(239, 68, 68, 0.3)',
                  padding: '1rem',
                  borderRadius: '8px',
                  marginBottom: '1.5rem',
                  color: '#fca5a5',
                  fontSize: '0.95rem',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.75rem'
                }}>
                  <span>⚠️</span>
                  <span>{error}</span>
                </div>
              )}

              <button
                type="submit"
                disabled={loading}
                className="btn-login"
              >
                {loading ? (
                  <>
                    <span className="spinner"></span>
                    Iniciando sesión...
                  </>
                ) : (
                  'Iniciar Sesión'
                )}
              </button>
            </form>

            {/* Tarjeta de Credenciales de Demostración */}
            <div className="credentials-card">
              <div className="credentials-header">
                <span className="cred-icon">🚀</span>
                <h4>Credenciales de Demostración</h4>
              </div>
              <div className="credentials-body">
                <div className="credential-item">
                  <span className="label">Correo</span>
                  <code>admin@test.com</code>
                </div>
                <div className="credential-item">
                  <span className="label">Contraseña</span>
                  <code>admin123</code>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Pie de Página */}
      <footer className="login-footer">
        <div className="footer-content">
          <p>&copy; 2026 DSS. Todos los derechos reservados.</p>
          <div className="footer-links">
            <a href="#privacy">Privacidad</a>
            <span className="separator">•</span>
            <a href="#terms">Términos</a>
            <span className="separator">•</span>
            <a href="#status">Estado</a>
          </div>
        </div>
      </footer>
    </div>
  )
}

export default Login
