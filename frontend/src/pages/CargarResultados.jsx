import { useState, useEffect, useRef } from 'react'
import api from '../services/api'
import NotificationModal from '../components/NotificationModal'
import '../styles/CargarResultados.css'

// Formato siempre JSON (según la tesis)
const FORMATO_FIJO = 'JSON'

// ── Parser y validador de Robot Framework JSON (output.json nativo de RF 7+) ──
// Campos requeridos:
//   generator           → "Robot 7.4 (Python 3.12.0 on win32)"
//   generated           → "2026-07-18T11:52:49.104114"
//   suite.status        → "PASS" | "FAIL"
//   suite.elapsed_time  → número >= 0 (segundos totales)
//   statistics.total    → { pass: N, fail: N, skip: N }
// Métricas calculadas:
//   total_pruebas    = pass + fail + skip
//   pruebas_exitosas = pass
//   pruebas_fallidas = fail
//   pruebas_omitidas = skip
//   cobertura        = (pass + fail) / total × 100  (cobertura del plan de pruebas)
//   tiempo_ejecucion = suite.elapsed_time

function parsearRobotFramework(texto) {
  const json = JSON.parse(texto)

  // 1. Validar generator
  if (!json.generator || typeof json.generator !== 'string' || !json.generator.toLowerCase().includes('robot')) {
    throw new Error('El archivo no fue generado por Robot Framework. El campo "generator" debe contener "Robot".')
  }

  // 2. Validar generated
  if (!json.generated || typeof json.generated !== 'string') {
    throw new Error('Falta el campo "generated" con la fecha/hora de generación del reporte.')
  }

  // 3. Validar suite
  if (!json.suite || typeof json.suite !== 'object') {
    throw new Error('Falta el objeto "suite" en el archivo.')
  }
  if (!json.suite.status || !['PASS', 'FAIL'].includes(json.suite.status)) {
    throw new Error('El campo "suite.status" debe ser "PASS" o "FAIL".')
  }
  if (typeof json.suite.elapsed_time !== 'number' || json.suite.elapsed_time < 0) {
    throw new Error('El campo "suite.elapsed_time" debe ser un número no negativo (segundos).')
  }

  // 4. Validar statistics.total
  if (!json.statistics || !json.statistics.total) {
    throw new Error('Falta el objeto "statistics.total" con los contadores de pruebas.')
  }
  const { pass, fail, skip } = json.statistics.total
  for (const [key, val] of [['pass', pass], ['fail', fail], ['skip', skip]]) {
    if (typeof val !== 'number' || !Number.isInteger(val) || val < 0) {
      throw new Error(`El campo "statistics.total.${key}" debe ser un entero no negativo.`)
    }
  }
  const total = pass + fail + skip
  if (total < 1) {
    throw new Error('El total de pruebas (pass + fail + skip) debe ser al menos 1.')
  }

  // 5. Calcular métricas
  const cobertura = parseFloat(((pass + fail) / total * 100).toFixed(2))
  // Extraer nombre corto de herramienta: "Robot 7.4 (Python...)" → "Robot 7.4"
  const herramienta = json.generator.replace(/\s*\(.*\)$/, '').trim()

  return {
    // Métricas numéricas
    totalPruebas:    total,
    pruebasExitosas: pass,
    pruebasFallidas: fail,
    pruebasOmitidas: skip,
    cobertura,
    tiempoEjecucion: json.suite.elapsed_time,
    // Metadatos de contexto
    herramienta,
    nombreSuite:     json.suite.name || '(sin nombre)',
    estadoSuite:     json.suite.status,
    erroresGlobales: Array.isArray(json.errors) ? json.errors.length : 0,
    fechaGeneracion: json.generated,
  }
}

function CargarResultados() {
  const fileRef = useRef(null)

  const [proyectos,        setProyectos]        = useState([])
  const [versiones,        setVersiones]        = useState([])
  const [loadingProyectos, setLoadingProyectos] = useState(true)
  const [loadingVersiones, setLoadingVersiones] = useState(false)
  const [proyectoId,       setProyectoId]       = useState('')
  const [versionId,        setVersionId]        = useState('')
  const [observaciones,    setObservaciones]    = useState('')
  const [archivo,          setArchivo]          = useState(null)
  const [metricas,         setMetricas]         = useState(null)
  const [errorArchivo,     setErrorArchivo]     = useState(null)
  const [errores,          setErrores]          = useState({})
  const [saving,           setSaving]           = useState(false)
  const [notification,     setNotification]     = useState(null)

  const showNotification = (type, title, message) => {
    setNotification({ type, title, message })
  }
  const closeNotification = () => {
    setNotification(null)
  }

  useEffect(() => {
    api.get('/proyectos')
      .then(res => setProyectos(res.data))
      .catch(() => {})
      .finally(() => setLoadingProyectos(false))
  }, [])

  const handleProyectoChange = async (e) => {
    const val = e.target.value
    setProyectoId(val)
    setVersionId('')
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

  const handleFileChange = async (e) => {
    const file = e.target.files[0]
    if (!file) return

    setArchivo(null)
    setMetricas(null)
    setErrorArchivo(null)
    if (errores.archivo) setErrores(p => { const n = { ...p }; delete n.archivo; return n })

    if (!file.name.toLowerCase().endsWith('.json')) {
      setErrorArchivo('Solo se aceptan archivos con extensión .json')
      e.target.value = ''
      return
    }

    try {
      const texto = await file.text()
      const m = parsearRobotFramework(texto)
      setArchivo(file)
      setMetricas(m)
      // Auto-completar observaciones con metadatos RF si el campo está vacío
      setObservaciones(prev => {
        if (prev.trim()) return prev
        const partes = [
          `Suite: ${m.nombreSuite}`,
          `Estado: ${m.estadoSuite}`,
          `Herramienta: ${m.herramienta}`,
          m.erroresGlobales > 0 ? `Errores globales: ${m.erroresGlobales}` : null,
        ].filter(Boolean)
        return partes.join(' | ')
      })
    } catch (err) {
      setErrorArchivo(err.message || 'El archivo no tiene el formato de Robot Framework esperado.')
      e.target.value = ''
    }
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    const errs = {}
    if (!versionId)   errs.versionId = 'Seleccioná una versión.'
    if (!archivo)     errs.archivo   = 'Seleccioná el archivo JSON de resultados.'
    if (observaciones.length > 500)
      errs.observaciones = 'No puede superar los 500 caracteres.'
    if (Object.keys(errs).length) { setErrores(errs); return }

    setSaving(true)
    try {
      await api.post('/resultadosPrueba', {
        versionId:       parseInt(versionId),
        nombreArchivo:   archivo.name,
        formatoArchivo:  FORMATO_FIJO,
        rutaArchivo:     null,
        observaciones:   observaciones.trim() || null,
        totalPruebas:    metricas.totalPruebas,
        pruebasExitosas: metricas.pruebasExitosas,
        pruebasFallidas: metricas.pruebasFallidas,
        cobertura:       metricas.cobertura,
        tiempoEjecucion: metricas.tiempoEjecucion,
      })
      showNotification('success', 'Éxito', 'El resultado fue registrado correctamente.')
      handleNuevo()
    } catch (err) {
      const msg = err.response?.data?.message
        || err.response?.data?.title
        || 'No se pudo registrar el resultado. Verificá los datos e intentá de nuevo.'
      showNotification('error', 'Error', msg)
    } finally {
      setSaving(false)
    }
  }

  const handleNuevo = () => {
    setProyectoId('')
    setVersionId('')
    setVersiones([])
    setObservaciones('')
    setArchivo(null)
    setMetricas(null)
    setErrorArchivo(null)
    setErrores({})
    if (fileRef.current) fileRef.current.value = ''
  }

  const obsRestantes = 500 - observaciones.length

  return (
    <div className="cr-page">

      {/* ── Header ── */}
      <div className="cr-header">
        <div className="cr-header-inner">
          <div>
            <h1 className="cr-title">Cargar resultados</h1>
            <p className="cr-subtitle">
              Subí el archivo JSON generado por el pipeline CI/CD para una versión específica
            </p>
          </div>
        </div>
      </div>

      {/* ── Body ── */}
      <div className="cr-body">
        <div className="cr-form-card">

          <form onSubmit={handleSubmit} noValidate>

            {/* ── Paso 1: Proyecto y versión ── */}
            <div className="cr-section">
              <h3 className="cr-section-title">
                <span className="cr-step">1</span>
                Proyecto y versión
              </h3>

              <div className="cr-grid-2">
                <div className="proy-form-group">
                  <label htmlFor="cr-proyecto">
                    Proyecto <span className="required">*</span>
                  </label>
                  <select
                    id="cr-proyecto"
                    value={proyectoId}
                    onChange={handleProyectoChange}
                    disabled={loadingProyectos}
                  >
                    <option value="">
                      {loadingProyectos ? 'Cargando proyectos…' : 'Seleccionar proyecto…'}
                    </option>
                    {proyectos.map(p => (
                      <option key={p.id} value={p.id}>{p.nombre}</option>
                    ))}
                  </select>
                </div>

                <div className={`proy-form-group ${errores.versionId ? 'has-error' : ''}`}>
                  <label htmlFor="cr-version">
                    Versión <span className="required">*</span>
                  </label>
                  <select
                    id="cr-version"
                    value={versionId}
                    onChange={e => {
                      setVersionId(e.target.value)
                      if (errores.versionId) setErrores(p => { const n = { ...p }; delete n.versionId; return n })
                    }}
                    disabled={!proyectoId || loadingVersiones}
                  >
                    <option value="">
                      {!proyectoId        ? 'Primero seleccioná un proyecto'
                       : loadingVersiones ? 'Cargando versiones…'
                       : versiones.length === 0 ? 'Sin versiones disponibles'
                       : 'Seleccionar versión…'}
                    </option>
                    {versiones.map(v => (
                      <option key={v.id} value={v.id}>v{v.numeroVersion}</option>
                    ))}
                  </select>
                  {errores.versionId && <span className="field-error">{errores.versionId}</span>}
                </div>
              </div>
            </div>

            {/* ── Paso 2: Archivo JSON + métricas extraídas ── */}
            <div className="cr-section">
              <h3 className="cr-section-title">
                <span className="cr-step">2</span>
                Archivo de resultados
              </h3>

              <p className="cr-section-hint">
                Seleccioná el <strong>output.json</strong> generado por Robot Framework (v7+) desde tu pipeline CI/CD.
                El sistema validará la estructura y extraerá automáticamente todas las métricas.
                Campos requeridos: <code className="cr-code-inline">generator</code>,{' '}
                <code className="cr-code-inline">suite.status</code>,{' '}
                <code className="cr-code-inline">suite.elapsed_time</code>,{' '}
                <code className="cr-code-inline">statistics.total</code>{' '}
                (con <code className="cr-code-inline">pass</code>, <code className="cr-code-inline">fail</code>, <code className="cr-code-inline">skip</code>).
              </p>

              {/* Input file oculto + zona de upload visible */}
              <input
                ref={fileRef}
                id="cr-file"
                type="file"
                accept=".json,application/json"
                className="cr-file-input-hidden"
                onChange={handleFileChange}
              />

              <label
                htmlFor="cr-file"
                className={`cr-upload-zone ${
                  errorArchivo     ? 'cr-upload-zone--error'   :
                  archivo          ? 'cr-upload-zone--ok'      :
                  errores.archivo  ? 'cr-upload-zone--required':
                  ''
                }`}
              >
                {archivo ? (
                  <>
                    <span className="cr-upload-icon-ok">✓</span>
                    <div className="cr-upload-file-info">
                      <span className="cr-upload-filename">{archivo.name}</span>
                      <span className="cr-upload-change">Hacer clic para cambiar el archivo</span>
                    </div>
                  </>
                ) : (
                  <>
                    <svg className="cr-upload-svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
                      <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>
                      <polyline points="17 8 12 3 7 8"/>
                      <line x1="12" y1="3" x2="12" y2="15"/>
                    </svg>
                    <span className="cr-upload-text">Seleccioná el archivo JSON</span>
                    <span className="cr-upload-subtext">Solo archivos .json</span>
                  </>
                )}
              </label>

              {errores.archivo && !errorArchivo && (
                <span className="field-error">{errores.archivo}</span>
              )}

              {errorArchivo && (
                <div className="cr-file-error">
                  <span className="form-api-error-icon">!</span>
                  <div>
                    <strong>Archivo inválido: </strong>{errorArchivo}
                    <br/>
                    <span className="cr-file-error-hint">
                      Asegurate de usar el output.json nativo de Robot Framework 7+.
                      Campos requeridos: generator, generated, suite (con status y elapsed_time), statistics.total (con pass, fail, skip).
                    </span>
                  </div>
                </div>
              )}

              {/* Vista previa de métricas extraídas automáticamente */}
              {metricas && (
                <div className="cr-metrics-preview">
                  {/* Barra de metadatos RF */}
                  <div className="cr-rf-meta">
                    <span className="cr-rf-meta-item">
                      <span className="cr-rf-meta-lbl">Suite</span>
                      <span className="cr-rf-meta-val">{metricas.nombreSuite}</span>
                    </span>
                    <span className="cr-rf-meta-sep" />
                    <span className="cr-rf-meta-item">
                      <span className="cr-rf-meta-lbl">Herramienta</span>
                      <span className="cr-rf-meta-val">{metricas.herramienta}</span>
                    </span>
                    <span className="cr-rf-meta-sep" />
                    <span className="cr-rf-meta-item">
                      <span className="cr-rf-meta-lbl">Estado</span>
                      <span className={`cr-rf-badge cr-rf-badge--${metricas.estadoSuite === 'PASS' ? 'pass' : 'fail'}`}>
                        {metricas.estadoSuite}
                      </span>
                    </span>
                    {metricas.erroresGlobales > 0 && (
                      <>
                        <span className="cr-rf-meta-sep" />
                        <span className="cr-rf-meta-item">
                          <span className="cr-rf-meta-lbl">Errores globales</span>
                          <span className="cr-rf-badge cr-rf-badge--fail">{metricas.erroresGlobales}</span>
                        </span>
                      </>
                    )}
                  </div>

                  {/* Chips numéricos */}
                  <p className="cr-metrics-preview-title">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="14" height="14">
                      <line x1="18" y1="20" x2="18" y2="10"/>
                      <line x1="12" y1="20" x2="12" y2="4"/>
                      <line x1="6"  y1="20" x2="6"  y2="14"/>
                    </svg>
                    Métricas extraídas automáticamente
                  </p>
                  <div className="cr-metric-chips">
                    <div className="cr-metric-chip">
                      <span className="cr-metric-chip-lbl">Total pruebas</span>
                      <span className="cr-metric-chip-val">{metricas.totalPruebas}</span>
                    </div>
                    <div className="cr-metric-chip cr-metric-chip--ok">
                      <span className="cr-metric-chip-lbl">Exitosas</span>
                      <span className="cr-metric-chip-val">{metricas.pruebasExitosas}</span>
                    </div>
                    <div className="cr-metric-chip cr-metric-chip--warn">
                      <span className="cr-metric-chip-lbl">Fallidas</span>
                      <span className="cr-metric-chip-val">{metricas.pruebasFallidas}</span>
                    </div>
                    {metricas.pruebasOmitidas > 0 && (
                      <div className="cr-metric-chip cr-metric-chip--skip">
                        <span className="cr-metric-chip-lbl">Omitidas</span>
                        <span className="cr-metric-chip-val">{metricas.pruebasOmitidas}</span>
                      </div>
                    )}
                    <div className="cr-metric-chip">
                      <span className="cr-metric-chip-lbl">Cobertura plan</span>
                      <span className="cr-metric-chip-val">{metricas.cobertura}%</span>
                    </div>
                    <div className="cr-metric-chip">
                      <span className="cr-metric-chip-lbl">Tiempo (s)</span>
                      <span className="cr-metric-chip-val">{metricas.tiempoEjecucion.toFixed(2)}</span>
                    </div>
                  </div>
                </div>
              )}

              {/* Observaciones */}
              <div className={`proy-form-group cr-full-row ${errores.observaciones ? 'has-error' : ''}`}>
                <label htmlFor="cr-obs">
                  Observaciones
                  <span className="char-count">{obsRestantes} restantes</span>
                </label>
                <textarea
                  id="cr-obs"
                  value={observaciones}
                  onChange={e => {
                    setObservaciones(e.target.value)
                    if (errores.observaciones) setErrores(p => { const n = { ...p }; delete n.observaciones; return n })
                  }}
                  placeholder="Notas sobre esta ejecución: sprint, condiciones especiales, incidentes reportados…"
                  rows={3}
                  maxLength={501}
                />
                {errores.observaciones && (
                  <span className="field-error">{errores.observaciones}</span>
                )}
              </div>
            </div>

            <div className="cr-form-footer">
              <button type="submit" className="btn-save cr-submit" disabled={saving}>
                {saving
                  ? <><span className="btn-spinner" /> Registrando…</>
                  : 'Registrar resultado'}
              </button>
            </div>
          </form>
        </div>
      </div>

      <NotificationModal
        isOpen={!!notification}
        type={notification?.type}
        title={notification?.title}
        message={notification?.message}
        onClose={closeNotification}
      />
    </div>
  )
}

export default CargarResultados
