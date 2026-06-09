import { useState } from 'react'
import api from '../services/api'
import '../styles/CargarResultados.css'

function CargarResultados() {
  const [versionId, setVersionId] = useState('')
  const [jsonContent, setJsonContent] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [success, setSuccess] = useState(null)

  const handleSubmit = async (e) => {
    e.preventDefault()
    setLoading(true)
    setError(null)
    setSuccess(null)

    try {
      const resultados = JSON.parse(jsonContent)

      if (!Array.isArray(resultados)) {
        throw new Error('El JSON debe ser un array de resultados')
      }

      const payload = {
        versionId: parseInt(versionId),
        resultados: resultados.map(r => ({
          nombrePrueba: r.nombrePrueba || r.name,
          tipoPrueba: r.tipoPrueba || r.type || 'unit',
          estado: r.estado || r.status || 'PASSED',
          tiempoEjecucion: r.tiempoEjecucion || r.duration,
          mensaje: r.mensaje || r.message
        }))
      }

      const response = await api.post('/resultadosPrueba/upload', payload)
      setSuccess(response.data.message)
      setJsonContent('')
      setVersionId('')
    } catch (err) {
      if (err instanceof SyntaxError) {
        setError('JSON inválido')
      } else {
        setError(err.message || 'Error al cargar resultados')
      }
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="cargar-resultados-container">
      <h1>Cargar Resultados de Pruebas</h1>

      {error && <div className="error-message">{error}</div>}
      {success && <div className="success-message">{success}</div>}

      <form onSubmit={handleSubmit} className="form-cargar">
        <div className="form-group">
          <label>ID de Versión:</label>
          <input
            type="number"
            value={versionId}
            onChange={(e) => setVersionId(e.target.value)}
            required
            placeholder="Ej: 1"
          />
        </div>

        <div className="form-group">
          <label>Resultados JSON:</label>
          <textarea
            value={jsonContent}
            onChange={(e) => setJsonContent(e.target.value)}
            placeholder={'[\n  {\n    "nombrePrueba": "test_login",\n    "estado": "PASSED",\n    "tiempoEjecucion": 0.5\n  }\n]'}
            rows="10"
            required
          />
        </div>

        <button type="submit" disabled={loading} className="btn-primary">
          {loading ? 'Cargando...' : 'Cargar Resultados'}
        </button>
      </form>

      <div className="formato-ayuda">
        <h3>Formato esperado:</h3>
        <pre>{`[
  {
    "nombrePrueba": "test_name",
    "tipoPrueba": "unit",
    "estado": "PASSED",
    "tiempoEjecucion": 0.5,
    "mensaje": "optional message"
  }
]`}</pre>
      </div>
    </div>
  )
}

export default CargarResultados
