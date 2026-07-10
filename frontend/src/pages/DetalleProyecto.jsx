import { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import api from '../services/api'
import '../styles/DetalleProyecto.css'

const SEMVER_RE = /^\d+\.\d+\.\d+$/

const ESTADO_VERSION = {
  pendiente:   { text: 'Pendiente',   cls: 'vest-pendiente' },
  en_revision: { text: 'En revisión', cls: 'vest-revision' },
  aprobada:    { text: 'Aprobada',    cls: 'vest-aprobada' },
  rechazada:   { text: 'Rechazada',   cls: 'vest-rechazada' },
}

const TIPO_MAP = {
  web:  'Aplicación Web',
  api:  'API / Microservicio',
  otro: 'Otro',
}

const ESTADO_PROY = {
  activo:    { text: 'Activo',    cls: 'badge-activo' },
  inactivo:  { text: 'Inactivo',  cls: 'badge-inactivo' },
  archivado: { text: 'Archivado', cls: 'badge-archivado' },
}

function validateVersion(fields) {
  const errors = {}
  const num = fields.numeroVersion.trim()
  if (!num) {
    errors.numeroVersion = 'El número de versión es obligatorio.'
  } else if (!SEMVER_RE.test(num)) {
    errors.numeroVersion = 'Usá formato semántico: mayor.menor.parche  (ej: 1.0.0)'
  }
  if (fields.descripcion && fields.descripcion.length > 300) {
    errors.descripcion = 'No puede superar los 300 caracteres.'
  }
  return errors
}

function VersionModal({ proyectoId, onClose, onSaved }) {
  const [fields, setFields] = useState({ numeroVersion: '', descripcion: '' })
  const [errors, setErrors] = useState({})
  const [saving, setSaving] = useState(false)
  const [apiError, setApiError] = useState(null)
  const inputRef = useRef(null)

  useEffect(() => {
    inputRef.current?.focus()
    const handleKey = (e) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [onClose])

  const set = (k) => (e) => {
    setFields(prev => ({ ...prev, [k]: e.target.value }))
    if (errors[k]) setErrors(prev => { const n = { ...prev }; delete n[k]; return n })
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    const errs = validateVersion(fields)
    if (Object.keys(errs).length) { setErrors(errs); return }
    setSaving(true)
    setApiError(null)
    try {
      await api.post('/versiones', {
        proyectoId:    parseInt(proyectoId),
        numeroVersion: fields.numeroVersion.trim(),
        descripcion:   fields.descripcion.trim() || null,
      })
      onSaved()
    } catch (err) {
      const msg = err.response?.data?.message
        || err.response?.data?.title
        || 'No se pudo crear la versión. Intentá de nuevo.'
      setApiError(msg)
    } finally {
      setSaving(false)
    }
  }

  const remaining = 300 - (fields.descripcion?.length || 0)

  return (
    <div className="modal-overlay" onC