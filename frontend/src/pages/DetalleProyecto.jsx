import { useState, useEffect } from 'react'
import { useParams } from 'react-router-dom'
import api from '../services/api'
import '../styles/DetalleProyecto.css'

function DetalleProyecto() {
  const { id } = useParams()
  const [proyecto, setProyecto] = useState(null)
  const [versiones, setVersiones] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [showVersionForm, setShowVersionForm] = useState(false)
  const [numeroVersion, setNumeroVersion] = useState('')

  useEffect(() => {
    fetchProyecto()
    fetchVersiones()
  }, [id])

  const fetchProyecto = async () => {
    try {
      const response = await api.get(`/proyectos/${id}`)
      setProyecto(response.data)
    } catch (err) {
      setError('Error al cargar proyecto')
    } finally {
      setLoading(false)
    }
  }

  const fetchVersiones = async () => {
    try {
      const response = await api.get(`/versiones/proyecto/${id}`)
      setVersiones(response.data)
    } catch (err) {
      console.error(err)
    }
  }

  const handleCreateVersion = async (e) => {
    e.preventDefault()
    try {
      await api.post('/versiones', { proyectoId: parseInt(id), numeroVersion })
      setNumeroVersion('')
      setShowVersionForm(false)
      fetchVersiones()
    } catch (err) {
      setError('Error al crear versión')
    }
  }

  if (loading) return <div className="loading">Cargando...</div>
  if (!proyecto) return <div className="error-message">Proyecto no encontrado</div>

  return (
    <div className="detalle-container">
      <h1>{proyecto.nombre}</h1>
      <p className="descripcion">{proyecto.descripcion}</p>

      <section className="versiones-section">
        <h2>Versiones</h2>
        <button className="btn-primary" onClick={() => setShowVersionForm(!showVersionForm)}>
          {showVersionForm ? 'Cancelar' : 'Nueva Versión'}
        </button>

        {showVersionForm && (
          <form onSubmit={handleCreateVersion} className="form-version">
            <input
              type="text"
              placeholder="Ej: 1.0.0"
              value={numeroVersion}
              onChange={(e) => setNumeroVersion(e.target.value)}
              required
            />
            <button type="submit" className="btn-success">Crear</button>
          </form>
        )}

        <div className="versiones-list">
          {versiones.map((version) => (
            <div key={version.id} className="version-item">
              <h4>{version.numeroVersion}</h4>
              <p>{version.descripcion}</p>
              <small>{new Date(version.fechaCreacion).toLocaleDateString()}</small>
            </div>
          ))}
        </div>
      </section>
    </div>
  )
}

export default DetalleProyecto
