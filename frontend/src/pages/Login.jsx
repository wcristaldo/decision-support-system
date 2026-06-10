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

          <div className="panel-footer">
            <span>© 2026 Roshka S.A. — Proyecto de Tesis UNIDA</span>
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
    </div>
  )
}

export default Login
