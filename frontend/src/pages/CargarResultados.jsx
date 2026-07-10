import { useState, useEffect } from 'react'
import api from '../services/api'
import '../styles/CargarResultados.css'

// El formato de resultados de prueba en el sistema es siempre JSON (según la tesis)
const FORMATO_FIJO = 'JSON'

function validate(fields) {
  const errors = {}
  if (!fields.versionId) {
    errors.versionId = 'Seleccioná una versión.'
  }
  const nombre = fields.nombreArchivo.trim()
  if (!nombre) {
    errors.nombreArchivo = 'El nombre del archivo es obligatorio.'
  } else if (nombre.length < 2) {
    errors.nombreArchivo = 'Debe tener al menos 2 caracteres.'
  } else if (nombre.length > 200) {
    errors.nombreArchivo = 'No puede superar los 200 caracteres.'
  }
  if (fields.observaciones && fields.observaciones.length > 500) {
    errors.observaciones = 'No puede superar los 500 caracteres.'
  }
  return errors
}

function CargarResultados() {
  const [proyectos, setProyectos] = useState([])
  const [versiones, setVersiones] = useState([])
  const [loadingProyectos, setLoadingProyectos] = useState(true)
  const [loadingVersiones, setLoadingVersiones] = useState(false)
  const [proyectoId, setProyectoId] = useState('')
  const [fields, setFields] = useState({
    versionId:     '',
    nombreArchivo: '',
    rutaArchivo:   '',
    observaciones: '',
  })
  const [errors, setErrors]   = useState({})
  const [saving, setSaving]   = useState(false)
  const [apiError, setApiError] = useState(null)
  const [success, setSuccess] = useState(false)

  useEffect(() => {
    api.get('/proyectos')
      .then(res => setProyectos(res.data))
      .catch(() => {})
      .finally(() => setLoadingProyectos(false))
  }, [])

  const handleProyectoChange = async (e) => {
    const val = e.target.value
    setProyectoId(val)
    setFields(prev => ({ ...prev, versionId: '' }))
    setVersiones([])
    if (!val) return
    setLoadingVersiones(true)
    try {
      const res = await api.get(`/versiones/proyecto/${val}`)
      setVersiones(res.data)
    } catch {
      setVersiones([])
    } finally {
      setLoadingVersiones(false)
    }
  }

  const set = (k) => (e) => {
    setFields(prev => ({ ...prev, [k]: e.target.value }))
    if (errors[k]) setErrors(prev => { const n = { ...prev }; delete n[k]; return n })
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    const errs = validate(fields)
    if (Object.keys(errs).length) { setErrors(errs); return }
    setSaving(true)
    setApiError(null)
    try {
      await api.post('/resultadosPrueba', {
        versionId:     parseInt(fields.versionId),
        nombreArchivo: fields.nombreArchivo.trim(),
        formatoArchivo: FORMATO_FIJO,
        rutaArchivo:   fields.rutaArchivo.trim() || null,
        observaciones: fields.observaciones.trim() || null,
      })
      setSuccess(true)
    } catch (err) {
      const msg = err.response?.data?.message
        || err.response?.data?.title
        || 'No 