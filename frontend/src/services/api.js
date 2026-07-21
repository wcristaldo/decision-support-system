import axios from 'axios'

// En dev: Vite proxea /api → localhost:5000 (vite.config.js)
// En prod (Vercel): VITE_API_URL apunta al backend en Railway
const API_BASE_URL = import.meta.env.VITE_API_URL || '/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
})

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

export const authService = {
  login: (email, password) => api.post('/auth/login', { email, password }),
  logout: () => {
    localStorage.removeItem('token')
  }
}

export const projectService = {
  getAll: () => api.get('/projects'),
  getById: (id) => api.get(`/projects/${id}`),
  create: (data) => api.post('/projects', data),
  update: (id, data) => api.put(`/projects/${id}`, data),
  delete: (id) => api.delete(`/projects/${id}`)
}

export default api
