import { useEffect, useState, useMemo } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import {
  ResponsiveContainer, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend,
} from 'recharts'
import api from '../services/api'
import { decodeJwtPayload } from '../utils/jwt'
import { fmtFechaCorta } from '../utils/fecha'
import { getRoles } from '../utils/auth'
import '../styles/Dashboard.css'

// ── Helpers ───────────────────────────────────────────────────────────────────

function normalizeRec(t) {
  if (!t) return ''
  const u = t.toUpperCase()
  if (u === 'DESPLEGAR_CON_OBSERVACIONES') return 'REVISAR'
  return u
}

function badgeClass(rec) {
  const r = normalizeRec(rec)
  if (r === 'DESPLEGAR')    return 'dash-badge--ok'
  if (r === 'NO_DESPLEGAR') return 'dash-badge--danger'
  return 'dash-badge--warn'
}

function badgeLabel(rec) {
  const r = normalizeRec(rec)
  if (r === 'DESPLEGAR')    return 'Desplegar'
  if (r === 'NO_DESPLEGAR') return 'No desplegar'
  return 'Revisar'
}

function fmtFechaHora(dateStr) {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleString('es-PY', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
}

// ── Módulos del sistema (accesos rápidos) ────────────────────────────────────

const BASE_MODULES = [
  { to: '/proyectos',          title: 'Proyectos',          desc: 'Gestión de proyectos y versiones.' },
  { to: '/cargar-resultados',  title: 'Cargar resultados',  desc: 'Registrar un nuevo resultado de pruebas.' },
  { to: '/analisis',           title: 'Análisis y métricas', desc: 'Historial completo de evaluaciones.' },
  { to: '/suscripcion',        title: 'Suscripción',        desc: 'Plan activo, uso y pagos.' },
]

const ADMIN_MODULE = { to: '/usuarios', title: 'Gestión de usuarios', desc: 'Cuentas, roles y permisos.' }

// ── Dashboard ─────────────────────────────────────────────────────────────────

function Dashboard({ onLogout }) {
  const navigate = useNavigate()
  const [usuario, setUsuario]     = useState(null)
  const [proyectos, setProyectos] = useState([])
  const [historial, setHistorial] = useState([])
  const [adherencia, setAdherencia] = useState(null)
  const [loadingDatos, setLoadingDatos] = useState(true)
  const isAdmin = getRoles().includes('Administrador')

  useEffect(() => {
    const token = sessionStorage.getItem('token')
    if (!token) { navigate('/login'); return }
    const payload = decodeJwtPayload(token)
    if (!payload) { navigate('/login'); return }
    setUsuario({
      nombre: payload.name || payload.nombre || 'Usuario',
      roles:  payload.role ? (Array.isArray(payload.role) ? payload.role : [payload.role]) : [],
    })
  }, [navigate])

  useEffect(() => {
    Promise.all([
      api.get('/proyectos').catch(() => ({ data: [] })),
      api.get('/analisis/historial').catch(() => ({ data: [] })),
      api.get('/decisionesDespliegue/adherencia').catch(() => ({ data: null })),
    ]).then(([pRes, hRes, aRes]) => {
      setProyectos(pRes.data)
      setHistorial(hRes.data)
      setAdherencia(aRes.data)
    }).finally(() => setLoadingDatos(false))
  }, [])

  // KPIs de los últimos 30 días
  const stats = useMemo(() => {
    const desde = Date.now() - 30 * 24 * 60 * 60 * 1000
    const recientes = historial.filter(h => h.fechaCarga && new Date(h.fechaCarga).getTime() >= desde)
    const aptas    = recientes.filter(h => normalizeRec(h.recomendacion) === 'DESPLEGAR').length
    const revisar  = recientes.filter(h => normalizeRec(h.recomendacion) === 'REVISAR').length
    const noAptas  = recientes.filter(h => normalizeRec(h.recomendacion) === 'NO_DESPLEGAR').length
    return {
      totalProyectos: proyectos.length,
      evaluacionesRecientes: recientes.length,
      aptas,
      atencion: revisar + noAptas,
    }
  }, [proyectos, historial])

  // Tendencia: últimas 10 evaluaciones cargadas, en orden cronológico
  const tendencia = useMemo(() => {
    return [...historial]
      .filter(h => h.fechaCarga)
      .sort((a, b) => new Date(a.fechaCarga) - new Date(b.fechaCarga))
      .slice(-10)
      .map(h => ({
        fecha:     fmtFechaCorta(h.fechaCarga),
        tasaExito: h.tasaExito != null ? Number(h.tasaExito) : null,
        cobertura: h.cobertura != null ? Number(h.cobertura) : null,
      }))
  }, [historial])

  // Actividad reciente: últimas 5 evaluaciones
  const actividadReciente = useMemo(() => {
    return [...historial]
      .filter(h => h.fechaCarga)
      .sort((a, b) => new Date(b.fechaCarga) - new Date(a.fechaCarga))
      .slice(0, 5)
  }, [historial])

  if (!usuario) return (
    <div className="dash-loading">
      <span className="dash-spinner" /> Cargando...
    </div>
  )

  const modules = isAdmin ? [...BASE_MODULES, ADMIN_MODULE] : BASE_MODULES
  const primerNombre = usuario.nombre?.split(' ')[0] || usuario.nombre
  const rolDisplay   = usuario.roles.length > 0 ? usuario.roles[0] : 'Usuario'

  return (
    <div className="dash-page">

      {/* ── Bienvenida ── */}
      <div className="dash-hero">
        <div className="dash-hero-inner">
          <div>
            <h1 className="dash-name">¡Bienvenido, {primerNombre}!</h1>
            <p className="dash-subtitle">Sistema de Apoyo a la Toma de Decisiones - Roshka S.A.</p>
          </div>
          <div className="dash-hero-chips">
            <span className="dash-chip">{rolDisplay}</span>
            <span className="dash-chip">
              {new Date().toLocaleDateString('es-PY', { weekday: 'long', day: '2-digit', month: 'long' })}
            </span>
          </div>
        </div>
      </div>

      <div className="dash-body">

        {/* ── KPIs ── */}
        <div className="dash-stats">
          <div className="dash-stat">
            <div className="dash-stat-icon dash-stat-icon--total">📁</div>
            <div>
              <div className="dash-stat-val">{loadingDatos ? '-' : stats.totalProyectos}</div>
              <div className="dash-stat-lbl">Proyectos activos</div>
            </div>
          </div>
          <div className="dash-stat">
            <div className="dash-stat-icon dash-stat-icon--total">📊</div>
            <div>
              <div className="dash-stat-val">{loadingDatos ? '-' : stats.evaluacionesRecientes}</div>
              <div className="dash-stat-lbl">Evaluaciones (30 días)</div>
            </div>
          </div>
          <div className="dash-stat">
            <div className="dash-stat-icon dash-stat-icon--ok">✓</div>
            <div>
              <div className="dash-stat-val">{loadingDatos ? '-' : stats.aptas}</div>
              <div className="dash-stat-lbl">Aptas para desplegar</div>
            </div>
          </div>
          <div className="dash-stat">
            <div className="dash-stat-icon dash-stat-icon--warn">⚠</div>
            <div>
              <div className="dash-stat-val">{loadingDatos ? '-' : stats.atencion}</div>
              <div className="dash-stat-lbl">Requieren atención</div>
            </div>
          </div>
        </div>

        {/* ── Tendencia + Actividad reciente ── */}
        <div className="dash-panels">
          <div className="dash-panel dash-panel-chart">
            <h2 className="dash-panel-title">Tendencia de calidad</h2>
            {tendencia.length > 1 ? (
              <ResponsiveContainer width="100%" height={220}>
                <LineChart data={tendencia} margin={{ top: 8, right: 20, left: 0, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#e9ecf1" />
                  <XAxis dataKey="fecha" tick={{ fontSize: 11, fill: '#4a647a' }} axisLine={{ stroke: '#d7e0e7' }} tickLine={false} />
                  <YAxis domain={[0, 100]} unit="%" tick={{ fontSize: 11, fill: '#4a647a' }} axisLine={{ stroke: '#d7e0e7' }} tickLine={false} width={38} />
                  <Tooltip formatter={(v) => v == null ? '-' : `${v}%`} contentStyle={{ fontSize: '0.8rem', borderRadius: 8 }} />
                  <Legend wrapperStyle={{ fontSize: '0.8rem' }} />
                  <Line type="monotone" dataKey="tasaExito" name="Tasa de éxito" stroke="#7e9ab2" strokeWidth={2.5} dot={{ r: 3 }} connectNulls />
                  <Line type="monotone" dataKey="cobertura" name="Cobertura"    stroke="#1a7a4e" strokeWidth={2.5} dot={{ r: 3 }} connectNulls />
                </LineChart>
              </ResponsiveContainer>
            ) : (
              <p className="dash-empty">Todavía no hay suficientes evaluaciones para mostrar una tendencia.</p>
            )}
          </div>

          <div className="dash-panel">
            <h2 className="dash-panel-title">Actividad reciente</h2>
            {actividadReciente.length === 0 ? (
              <p className="dash-empty">Sin evaluaciones registradas.</p>
            ) : (
              <ul className="dash-activity-list">
                {actividadReciente.map((h) => (
                  <li key={`${h.resultadoId}-${h.recomendacionId}`}>
                    <Link to={`/versiones/${h.versionId}/analisis?resultado=${h.resultadoId}`} className="dash-activity-item">
                      <span className={`dash-badge ${badgeClass(h.recomendacion)}`}>{badgeLabel(h.recomendacion)}</span>
                      <span className="dash-activity-text">
                        <strong>{h.proyectoNombre}</strong> · v{h.versionNumero}
                      </span>
                      <span className="dash-activity-date">{fmtFechaHora(h.fechaCarga)}</span>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        {/* ── Adherencia a recomendaciones ── */}
        {!loadingDatos && adherencia && adherencia.total > 0 && (
          <div className="dash-panel dash-adherencia-panel">
            <div className="dash-adherencia-main">
              <div className="dash-adherencia-value">{adherencia.porcentajeAdherencia}%</div>
              <div>
                <h2 className="dash-panel-title dash-adherencia-title">Adherencia a las recomendaciones</h2>
                <p className="dash-adherencia-sub">
                  De {adherencia.total} decisiones de despliegue registradas, {adherencia.alineadas} siguieron
                  la recomendación automática del sistema.
                  {adherencia.overrides > 0 && ` Hubo ${adherencia.overrides} en las que el Líder Técnico decidió apartarse de ella.`}
                </p>
              </div>
            </div>
            <div className="dash-adherencia-bar">
              <div className="dash-adherencia-bar-fill" style={{ width: `${adherencia.porcentajeAdherencia}%` }} />
            </div>
            <p className="dash-adherencia-hint">
              El sistema asiste a la decisión — no decide por sí solo. Esta métrica mide cuánto influye realmente
              su recomendación en la decisión final.
            </p>
          </div>
        )}

        {/* ── Módulos ── */}
        <div className="dash-section-head">
          <h2 className="dash-section-title">Accesos rápidos</h2>
        </div>
        <div className="dash-grid">
          {modules.map((m) => (
            <Link to={m.to} key={m.title} className="dash-card">
              <h3 className="dash-card-title">{m.title}</h3>
              <p className="dash-card-desc">{m.desc}</p>
            </Link>
          ))}
        </div>
      </div>

      <footer className="dash-footer">
        © 2026 Roshka S.A. - Proyecto de Tesis UNIDA
      </footer>
    </div>
  )
}

export default Dashboard
