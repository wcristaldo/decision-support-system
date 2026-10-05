import axios from 'axios'

const api = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json'
  }
})

api.interceptors.request.use((config) => {
  const token = sessionStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// Rutas de autenticación: un 401 acá es "credenciales inválidas" o similar,
// no una sesión vencida. El propio componente (Login, etc.) debe mostrar el
// mensaje de error; no hay sesión que limpiar ni redirección que hacer.
const AUTH_PATHS = ['/auth/login', '/auth/forgot-password', '/auth/reset-password-with-code']

api.interceptors.response.use(
  (response) => response,
  (error) => {
    const isAuthRequest = AUTH_PATHS.some((path) => error.config?.url?.includes(path))
    if (error.response?.status === 401 && !isAuthRequest) {
      // Token expirado o inválido → limpiar sesión y redirigir al login
      sessionStorage.removeItem('token')
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)

export default api
