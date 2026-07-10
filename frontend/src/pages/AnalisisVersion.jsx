import { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import api from '../services/api'
import '../styles/AnalisisVersion.css'

// ── Constantes ────────────────────────────────────────────────────────────────

const TIPO_RECOMENDACION = {
  DESPLEGAR: {
    label:    'Apto para despliegue',
    sublabel: 'Las métricas cumplen los umbrales de calidad establecidos.',
    cls:      'sem-desplegar',
    iconCls:  'sem-icon-ok',
    icon:     '✓',
  },
  REVISAR: {
    label:    'Requiere revisión',
    sublabel: 'Algunas métricas están por debajo de los umbrales esperados.',
    cls:      'sem-revisar',
    iconCls:  'sem-icon-warn',
    icon:     '⚠',
  },
  NO_DESPLEGAR: {
    label:    'No apto para despliegue',
    sublabel: 'Las métricas no alcanzan los umbrales mínimos de calidad.',
    cls:      'sem-no-desplegar',
    iconCls:  'sem-icon-no',
    icon:     '✕',
  },
}

const DECISION_MAP = {
  Aprobado:  { text: 'Aprobado',  cls: 'dec-aprobado' },
  Rechazado: { text: 'Rechazado', cls: 'dec-rechazado' },
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function getRawValue(m) {
  const v = m.valorMetrica ?? m.valor
  return parseFloat(v)
}

function formatMetricValue(m) {
  const v = getRawValue(m)
  const unit = (m.unidad || '').toLowerCase()
  if (isNaN(v)) return String(m.valorMetrica ?? m.valor ?? '—')
  if (unit === '%' || unit === 'porcentaje' || unit === 'percent') {
    return `${v.toFixed(2)}%`
  }
  if (Number.isInteger(v)) return m.unidad ? `${v} ${m.unidad}` : `${v}`
  return m.unidad ? `${v.toFixed(3)} ${m.unidad}` : `${v.toFixed(3)}`
}

function metricColorClass(m) {
  const nombre = (m.nombreMetrica || '').toLowerCase()
  const v = getRawValue(m)
  if (isNaN(v)) return 'mc-neutral'
  if (nombre.includes('exito') || nombre.includes('éxito') || nombre.includes('cobertura')) {
    if (v >= 95) return 'mc-ok'
    if (v >= 80) return 'mc-warn'
    return 'mc-danger'
  }
  if (nombre.includes('fallo') || nombre.includes('error') || nombre.includes('fail')) {
    if (v <= 5)  return 'mc-ok'
    if (v <= 20) return 'mc-warn'
    return 'mc-danger'
  }
  return 'mc-neutral'
}

const METRIC_LABELS = {
  tasa_exito:       'Tasa de éxito',
  tasa_fallo:       'Tasa de fallo',
  total_pruebas:    'Total de pruebas',
  pruebas_exitosas: 'Pruebas exitosas',
  pruebas_fallidas: 'Pruebas fallidas',
  cobertura:        'Cobertura',
  cobertura_codigo: 'Cobertura de código',
  tiempo_promedio:  'Tiempo promedio',
  duracion_total:   'Duración total',
}

function metricLabel(nombre) {
  const k = (nombre || '').toLowerCase().replace(/ /g, '_')
  return METRIC_LABELS[k] || nombre
}

// ── Validación decisión ───────────────────────────────────────────────────────

function validateDecision(fields) {
  const errors = {}
  if (!fields.decision) {
    errors.decision = 'Seleccioná una decisión.'
  }
  const comentario = fields.comentario.trim()
  if (!comentario) {
    errors.comentario = 'La justificación es obligatoria.'
  } else if (comentario.length < 10) {
    errors.comentario = 'Debe tener al menos 10 caracteres.'
  } else if (comentario.length > 1000) {
    errors.comentario = 'No puede superar los 1000 caracteres.'
  }
  return errors
}

// ── DecisionModal ─────────────────────────────────────────────────────────────

function DecisionModal({ recomendaciones, onClose, onSaved }) {
  const [fields, setFields] = useState({ decision: '', comentario: '' })
  const [errors, setErrors] = useState({})
  const [saving, setSaving]   = useState(false)
  const [apiError, setApiError] = useState(null)
  const textRef = useRef(null)

  // Usamos el ID de la primera recomendación disponible
  const recomendacionId = recomendaciones?.[0]?.id ?? null

  useEffect(() => {
    const handleKey = (e) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [onClose])

  const set = (k) => (e) => {
    setFields(prev => ({ ...prev, [k]: e.target.value }))
    if (errors[k]) setErrors(prev => { const n = { ...prev }; delete n[k]; return n })
  }

  const handleSubmit = async (e) => {
    e.p