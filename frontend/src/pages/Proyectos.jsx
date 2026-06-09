import { useState, useEffect } from 'react'
import api from '../services/api'
import '../styles/Proyectos.css'

function Proyectos() {
  const [proyectos, setProyectos] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [showForm, setShowForm] = useState(false)
  const [nombre, setNombre] = useState('')
  const [descripcion, setDescripcion] = useState('')

  useEffect(() => {
    fetchProyectos()
  }, [])

  const fetchProyectos = async () => {
    try {
      const response = await api.get('/proyectos')
      setProyectos(response.data)
      setError(null)
    } catch (err) {
      setError('Error al cargar proyectos')
      console.error(err)
    } finally {
      setLoading(false)
    }
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    try {
      await api.post('/proyectos', { nombre, descripcion })
      setNombre('')
      setDescripcion('')
      setShowForm(false)
      fetchProyectos()
    } catch (err) {
      setError('Error al crear proyecto')
    }
  }

  if (loading) return <div className="loading">Cargando...</div>

  return (
    <div className="proyectos-container">
      <h1>Proyectos</h1>

      {error && <div className="error-message">{error}</div>}

      <button className="btn-primary" onClick={() => setShowForm(!showForm)}>
        {showForm ? 'Cancelar' : 'Nuevo Proyecto'}
      </button>

      {showForm && (
        <form onSubmit={handleSubmit} className="form-proyectos">
          <input
            type="text"
            placeholder="Nombre del proyecto"
            value={nombre}
            onChange={(e) => setNombre(e.target.value)}
            required
          />
          <textarea
            placeholder="Descripción"
            value={descripcion}
            onChange={(e) => setDescripcion(e.target.value)}
            rows="4"
          />
          <button type="submit" className="btn-success">Crear</button>
        </form>
      )}

      <div className="proyectos-grid">
        {proyectos.map((proyecto) => (
          <div key={proyecto.id} className="proyecto-card">
            <h3>{proyecto.nombre}</h3>
            <p>{proyecto.descripcion}</p>
            <small>{new Date(proyecto.fechaCreacion).toLocaleDateString()}</small>
          </div>
        ))}
      </div>
    </div>
  )
}

export default Proyectos
