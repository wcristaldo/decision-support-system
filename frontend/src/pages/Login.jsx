import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../services/api'
import { IconBarChart, IconGear, IconSearch, IconLock } from '../components/icons'
import '../styles/Login.css'

/* ─────────────────────────────────────────────────────────────────────
   Contenido: Sobre el sistema
───────────────────────────────────────────────────────────────────── */
function AboutContent() {
  return (
    <div className="lm-body">
      <div className="lm-header">
        <div className="lm-header-icon">DSS</div>
        <div>
          <h2 className="lm-title">Sobre el sistema</h2>
          <p className="lm-subtitle">Decision Support System — Roshka DSS</p>
        </div>
      </div>

      <div className="lm-section">
        <h3>¿Qué es Roshka DSS?</h3>
        <p>Roshka DSS es una plataforma interna desarrollada para Roshka S.A. que asiste al equipo técnico en la evaluación de la calidad del software antes de autorizar su despliegue a producción, mediante el análisis automatizado de resultados de pruebas.</p>
      </div>

      <div className="lm-section">
        <h3>¿Cómo funciona?</h3>
        <p>El sistema analiza automáticamente los resultados de las pruebas automatizadas (archivo output.json de Robot Framework) y calcula métricas clave de calidad: tasa de éxito, tasa de fallo, cobertura de ejecución de pruebas (pruebas ejecutadas sobre el total) y tiempo de ejecución, además de su tendencia histórica. Estas métricas se comparan contra umbrales configurables y el sistema emite una recomendación categorizada en tres estados:</p>
        <div className="lm-states">
          <div className="lm-state lm-state--green">
            <span className="lm-state-dot" />
            <div>
              <strong>Desplegar</strong>
              <span>Todos los indicadores superan los umbrales configurados.</span>
            </div>
          </div>
          <div className="lm-state lm-state--yellow">
            <span className="lm-state-dot" />
            <div>
              <strong>Revisar</strong>
              <span>Uno o más indicadores están dentro del rango de tolerancia.</span>
            </div>
          </div>
          <div className="lm-state lm-state--red">
            <span className="lm-state-dot" />
            <div>
              <strong>No desplegar</strong>
              <span>Indicadores críticos por debajo del umbral mínimo.</span>
            </div>
          </div>
        </div>
      </div>

      <div className="lm-section">
        <h3>Principio de diseño</h3>
        <p>El sistema asiste, no decide. La recomendación es siempre consultiva: la aprobación final del despliegue requiere la intervención y responsabilidad explícita de un Líder Técnico, quien registra su decisión junto con una justificación escrita. Este enfoque está alineado con los principios de los Sistemas de Apoyo a la Toma de Decisiones descritos por Power (2019) y con las directrices éticas de la ACM (2018).</p>
      </div>

      <div className="lm-section">
        <h3>Capacidades principales</h3>
        <div className="lm-caps-grid">
          <div className="lm-cap">
            <span className="lm-cap-icon"><IconBarChart size={22} /></span>
            <div>
              <strong>Análisis de métricas</strong>
              <span>Cobertura, tasa de éxito, tiempo de ejecución y tendencia histórica por versión.</span>
            </div>
          </div>
          <div className="lm-cap">
            <span className="lm-cap-icon"><IconGear size={22} /></span>
            <div>
              <strong>Reglas configurables</strong>
              <span>Umbrales de calidad ajustables por el administrador según los estándares del equipo.</span>
            </div>
          </div>
          <div className="lm-cap">
            <span className="lm-cap-icon"><IconSearch size={22} /></span>
            <div>
              <strong>Trazabilidad completa</strong>
              <span>Historial de decisiones con justificación escrita y registro de auditoría.</span>
            </div>
          </div>
          <div className="lm-cap">
            <span className="lm-cap-icon"><IconLock size={22} /></span>
            <div>
              <strong>Control por roles</strong>
              <span>Acceso segmentado para Administrador, Analista QA, Líder Técnico y Gerente QA.</span>
            </div>
          </div>
        </div>
      </div>

      <div className="lm-section">
        <h3>Desarrollado por</h3>
        <p>Willian Samuel Cristaldo — Proyecto de Tesis de Grado, Universidad de la Integración de las Américas (UNIDA), Ingeniería en Sistemas, 2026.</p>
        <p style={{ marginTop: '0.4rem', color: 'var(--text-light)', fontSize: '0.875rem' }}>Empresa aliada: Roshka S.A. — Asunción, República del Paraguay.</p>
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────────────────────────────────
   Contenido: Planes
   Se traen SIEMPRE desde /api/suscripcion/planes (endpoint público) —
   la misma fuente que usa la pantalla de Suscripción una vez logueado.
   No hay una segunda copia hardcodeada: si cambia un límite o una
   funcionalidad en la base, se refleja acá automáticamente.
───────────────────────────────────────────────────────────────────── */
const PLAN_COLOR = ['basic', 'pro', 'enterprise']

function planToCard(p, idx) {
  const lim = p.limites || {}
  const fn  = p.funcionalidades || {}
  return {
    nombre: p.nombre,
    precio: `Gs. ${Number(p.precioMensual).toLocaleString('es-PY')}`,
    color:  PLAN_COLOR[idx] || 'basic',
    badge:  idx === 1 ? 'Recomendado' : null,
    features: [
      { label: `Proyectos: ${lim.maxProyectos}`,            ok: true },
      { label: `Usuarios: ${lim.maxUsuarios}`,               ok: true },
      { label: `Evaluaciones/mes: ${lim.maxEvaluacionesMes}`, ok: true },
      { label: lim.maxTamanoArchivoMb != null ? `Archivos hasta ${lim.maxTamanoArchivoMb} MB` : 'Archivos sin límite', ok: true },
      { label: `Historial: ${lim.historialDias}`,            ok: true },
      { label: 'Dashboard avanzado',       ok: !!fn.dashboardAvanzado },
      { label: 'Exportar PDF',             ok: !!fn.exportarPdf },
      { label: 'Exportar Excel/CSV',       ok: !!fn.exportarExcel },
      { label: 'Alertas por email',        ok: !!fn.notificacionesEmail },
      { label: 'Carga automatizada mediante la API', ok: !!fn.cargaAutomatizadaApi },
      { label: 'Auditoría detallada',      ok: !!fn.auditoriaDetallada },
      { label: 'Soporte prioritario',      ok: !!fn.soportePrioritario },
    ],
  }
}

function PlansContent() {
  const [planes, setPlanes]   = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError]     = useState(false)

  useEffect(() => {
    api.get('/suscripcion/planes')
      .then(res => setPlanes(res.data.map(planToCard)))
      .catch(() => setError(true))
      .finally(() => setLoading(false))
  }, [])

  return (
    <div className="lm-body lm-body--plans">
      <h2 className="lm-title">Planes disponibles</h2>
      <p className="lm-subtitle">Elegí el plan que mejor se adapte a las necesidades de tu equipo.</p>

      {loading && <p className="lm-plans-note">Cargando planes…</p>}
      {error && <p className="lm-plans-note">No se pudieron cargar los planes. Intentá de nuevo más tarde.</p>}

      <div className="lm-plans-grid">
        {planes.map(plan => (
          <div key={plan.nombre} className={`lm-plan-card lm-plan-card--${plan.color}`}>
            {plan.badge && <span className="lm-plan-badge">{plan.badge}</span>}
            <div className="lm-plan-name">{plan.nombre}</div>
            <div className="lm-plan-price">
              <span className="lm-plan-amount">{plan.precio}</span>
              <span className="lm-plan-period">/mes</span>
            </div>
            <ul className="lm-plan-features">
              {plan.features.map((f, i) => (
                <li key={i} className={f.ok ? 'feat-ok' : 'feat-no'}>
                  <span className="feat-icon">{f.ok ? '✓' : '—'}</span>
                  <span>{f.label}</span>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>

      <p className="lm-plans-note">Para contratar o cambiar de plan, contactá al administrador del sistema.</p>
    </div>
  )
}

/* ─────────────────────────────────────────────────────────────────────
   Contenido: Política de privacidad
───────────────────────────────────────────────────────────────────── */
function PrivacyContent() {
  return (
    <div className="lm-body lm-body--privacy">
      <h2 className="lm-title">Política de Privacidad</h2>
      <p className="lm-subtitle">Roshka DSS — Versión 1.1 · Última actualización: 26 de septiembre de 2026</p>

      <div className="lm-privacy-meta">
        <div><strong>Responsable:</strong> Roshka S.A.</div>
        <div><strong>Domicilio:</strong> Asunción, República del Paraguay</div>
        <div><strong>Contacto:</strong> privacidad@roshka.com.py</div>
        <div><strong>Marco normativo:</strong> Ley N.º 7593/2025 de Protección de Datos Personales — Paraguay</div>
      </div>

      <div className="lm-privacy-sections">
        <section>
          <h3>1. Objeto</h3>
          <p>La presente Política de Privacidad regula el tratamiento de los datos personales de los usuarios de Roshka DSS, plataforma interna de Roshka S.A. destinada al apoyo a la toma de decisiones para el despliegue de software mediante el análisis automatizado de resultados de pruebas.</p>
          <p>Roshka S.A. actúa como responsable del tratamiento y se compromete a tratar los datos con pleno respeto a la Ley N.º 7593/2025 "De Protección de Datos Personales" de la República del Paraguay. El acceso y uso del Sistema implica la aceptación de esta Política.</p>
        </section>

        <section>
          <h3>2. Responsable del Tratamiento</h3>
          <p><strong>Empresa:</strong> Roshka S.A. &nbsp;|&nbsp; <strong>Domicilio:</strong> Asunción, República del Paraguay &nbsp;|&nbsp; <strong>Contacto:</strong> privacidad@roshka.com.py</p>
        </section>

        <section>
          <h3>3. Datos Personales Recopilados</h3>
          <p><strong>Datos de identificación y acceso:</strong> nombre completo del usuario, correo electrónico institucional, contraseña almacenada como hash bcrypt (factor de costo 12, nunca en texto plano), rol asignado (Administrador, Analista QA, Líder Técnico o Gerente QA) y proyectos asignados.</p>
          <p><strong>Registros operativos:</strong> registros de auditoría (acción realizada, entidad afectada, fecha, hora y usuario), decisiones de despliegue (aprobado / rechazado / postergado) con su justificación escrita y códigos temporales de recuperación de contraseña (de un solo uso, con vigencia de 15 minutos).</p>
          <p><strong>No se recopilan:</strong> número de documento de identidad, teléfono, fecha de nacimiento, datos biométricos ni geolocalización. Los reportes JSON contienen métricas técnicas de software y no constituyen datos personales.</p>
        </section>

        <section>
          <h3>4. Finalidad del Tratamiento</h3>
          <p>Los datos se tratan exclusivamente para gestionar el registro, autenticación e identificación de los usuarios; controlar el acceso por rol (RBAC); ejecutar el análisis automatizado de resultados de pruebas; registrar las decisiones de despliegue con trazabilidad; y mantener registros de auditoría. No se utilizarán con fines comerciales, publicitarios ni de perfilado.</p>
        </section>

        <section>
          <h3>5. Base Jurídica del Tratamiento</h3>
          <p>El tratamiento se sustenta en la relación laboral o contractual que vincula al usuario con Roshka S.A., en el interés legítimo de garantizar la trazabilidad y el control interno del proceso de despliegue, en el consentimiento del titular y en el cumplimiento de las obligaciones legales aplicables, conforme a la Ley N.º 7593/2025.</p>
        </section>

        <section>
          <h3>6. Período de Conservación</h3>
          <p>Los datos de usuario se conservan mientras la cuenta esté activa. Al darse de baja, se procede a la supresión o anonimización, salvo obligación legal. Los registros de auditoría y decisiones de despliegue se conservan indefinidamente como historial trazable del Sistema.</p>
        </section>

        <section>
          <h3>7. Comunicación a Terceros</h3>
          <p>Roshka S.A. no comunicará datos personales a terceros, salvo obligación legal o requerimiento de autoridad competente, necesidad técnica para el funcionamiento del Sistema (encargados sujetos a las mismas obligaciones de confidencialidad), o consentimiento expreso del titular. Los reportes JSON no serán comunicados fuera del entorno del Sistema bajo ninguna circunstancia.</p>
        </section>

        <section>
          <h3>8. Medidas de Seguridad de la Información</h3>
          <p><strong>Técnicas:</strong> autenticación JWT con tiempo de expiración, control de acceso RBAC por rol y por proyecto asignado, contraseñas con bcrypt (factor de costo 12), limitación de intentos de inicio de sesión, comunicaciones HTTPS/TLS en el entorno de producción, registros de auditoría, respaldo periódico de la base de datos, despliegue en contenedores Docker con Nginx como proxy inverso y gestión de credenciales mediante variables de entorno.</p>
          <p><strong>Organizativas:</strong> acceso por principio de mínimo privilegio, revisión periódica de roles y capacitación del personal en materia de protección de datos.</p>
        </section>

        <section>
          <h3>9. Derechos del Titular</h3>
          <p>Conforme a la Ley N.º 7593/2025, el titular puede ejercer los derechos de <strong>acceso</strong>, <strong>rectificación</strong>, <strong>supresión</strong>, <strong>oposición</strong>, <strong>revocación del consentimiento</strong> y <strong>portabilidad</strong>. Las solicitudes deben dirigirse a <strong>privacidad@roshka.com.py</strong>. Roshka S.A. responderá en los plazos establecidos por la ley.</p>
        </section>

        <section>
          <h3>10. Consentimiento</h3>
          <p>El Sistema no admite el autorregistro: las cuentas son creadas por el Administrador a solicitud de Roshka S.A. Al momento del alta, el usuario es informado sobre esta Política y sobre los Términos y Condiciones de Uso, que permanecen publicados y accesibles en todo momento desde la pantalla de inicio de sesión. El inicio de sesión y el uso del Sistema implican la aceptación de ambos documentos. El titular puede revocar su consentimiento en cualquier momento escribiendo a privacidad@roshka.com.py, lo que conlleva la inactivación de su cuenta.</p>
        </section>

        <section>
          <h3>11. Gestión de Sesiones</h3>
          <p>El Sistema no utiliza cookies de rastreo ni tecnologías de seguimiento de terceros. La sesión se gestiona mediante tokens JWT con una vigencia configurable de 60 minutos por defecto, que se guardan en el almacenamiento de sesión del navegador y se eliminan al cerrar sesión o al cerrar la pestaña. Si el usuario activa la opción «Recordarme», el Sistema guarda únicamente su correo electrónico en el almacenamiento local del navegador para completar el formulario de ingreso; desmarcar la opción lo elimina.</p>
        </section>

        <section>
          <h3>12. Actualizaciones de Esta Política</h3>
          <p>Roshka S.A. podrá actualizar esta Política cuando resulte necesario por cambios normativos, funcionales o tecnológicos. Toda modificación será publicada en el Sistema con indicación de la fecha de la nueva versión. El uso continuado implica la aceptación de los cambios.</p>
        </section>

        <section>
          <h3>13. Contacto</h3>
          <p><strong>Roshka S.A. — Área de Privacidad y Protección de Datos</strong><br />
          Correo electrónico: privacidad@roshka.com.py<br />
          Ubicación: Asunción, República del Paraguay</p>
        </section>
      </div>

      <p className="lm-privacy-footer">© 2026 Roshka S.A. — Todos los derechos reservados. Este documento tiene carácter vinculante para todos los usuarios de Roshka DSS.</p>
    </div>
  )
}

/* ─────────────────────────────────────────────────────────────────────
   Contenido: Términos y condiciones de uso
───────────────────────────────────────────────────────────────────── */
function TermsContent() {
  return (
    <div className="lm-body lm-body--privacy">
      <h2 className="lm-title">Términos y Condiciones de Uso</h2>
      <p className="lm-subtitle">Roshka DSS — Versión 1.0 · Vigente desde el 26 de septiembre de 2026</p>

      <div className="lm-privacy-meta">
        <div><strong>Titular del servicio:</strong> Roshka S.A.</div>
        <div><strong>Domicilio:</strong> Asunción, República del Paraguay</div>
        <div><strong>Contacto:</strong> privacidad@roshka.com.py</div>
        <div><strong>Marco normativo:</strong> legislación de la República del Paraguay</div>
      </div>

      <div className="lm-privacy-sections">
        <section>
          <h3>1. Objeto y aceptación</h3>
          <p>Los presentes Términos y Condiciones regulan el acceso y uso de Roshka DSS, sistema de apoyo a la toma de decisiones para el despliegue de software mediante el análisis automatizado de resultados de pruebas. El inicio de sesión en el Sistema implica la aceptación plena de estos Términos y de la Política de Privacidad. Quien no esté de acuerdo debe abstenerse de utilizarlo.</p>
        </section>

        <section>
          <h3>2. Definiciones</h3>
          <p><strong>Sistema:</strong> la aplicación web Roshka DSS y su API. <strong>Usuario:</strong> persona con una cuenta activa en el Sistema. <strong>Administrador:</strong> usuario responsable de gestionar cuentas, roles, permisos, proyectos, reglas de evaluación y respaldos. <strong>Recomendación:</strong> resultado automático del análisis de métricas (Desplegar, Revisar o No desplegar). <strong>Decisión:</strong> resolución final registrada por un usuario autorizado (aprobado, rechazado o postergado).</p>
        </section>

        <section>
          <h3>3. Cuentas de usuario y acceso</h3>
          <p>Las cuentas son creadas por el Administrador; el Sistema no admite el autorregistro. Las credenciales son personales e intransferibles, y el usuario es responsable de su confidencialidad y de toda actividad realizada con ellas. Cada usuario accede únicamente a las funciones de su rol (Administrador, Analista QA, Líder Técnico o Gerente QA) y a los proyectos que tiene asignados, conforme al principio de mínimo privilegio.</p>
        </section>

        <section>
          <h3>4. Obligaciones del usuario</h3>
          <p>El usuario se compromete a: (a) utilizar el Sistema exclusivamente para fines laborales vinculados al proceso de evaluación y despliegue de software; (b) cargar reportes de pruebas auténticos, sin alterar sus resultados; (c) no intentar acceder a información o funciones no autorizadas, ni vulnerar las medidas de seguridad; (d) no divulgar fuera de la organización la información del Sistema; y (e) informar al Administrador cualquier incidente de seguridad o uso indebido del que tenga conocimiento.</p>
        </section>

        <section>
          <h3>5. Naturaleza de las recomendaciones</h3>
          <p>Las recomendaciones que genera el Sistema son de carácter consultivo: resultan de comparar las métricas de cada ejecución de pruebas con los umbrales configurados y no reemplazan el juicio profesional. La decisión final de despliegue y su responsabilidad corresponden exclusivamente al usuario autorizado que la registra, quien debe consignar una justificación escrita, que será más extensa cuando la decisión contradiga la recomendación del Sistema.</p>
        </section>

        <section>
          <h3>6. Información cargada</h3>
          <p>Los reportes de pruebas, métricas, versiones y decisiones registradas en el Sistema son información de Roshka S.A. y de sus clientes, y se utilizan solo para las finalidades del Sistema. El usuario garantiza que cuenta con autorización para cargar la información que ingresa.</p>
        </section>

        <section>
          <h3>7. Auditoría y trazabilidad</h3>
          <p>El Sistema registra en su bitácora de auditoría las acciones relevantes de cada usuario (inicios de sesión, cargas de resultados, cambios de configuración, gestión de usuarios y decisiones de despliegue), con fecha, hora y autor. El usuario acepta este registro, que tiene por objeto garantizar la trazabilidad y la rendición de cuentas del proceso.</p>
        </section>

        <section>
          <h3>8. Disponibilidad, mantenimiento y respaldos</h3>
          <p>Roshka S.A. procurará mantener el Sistema disponible y en funcionamiento, sin garantizar un servicio ininterrumpido ni libre de errores. Podrán realizarse interrupciones programadas por mantenimiento o actualización. La base de datos cuenta con respaldos periódicos configurados por el Administrador.</p>
        </section>

        <section>
          <h3>9. Suscripciones y pagos</h3>
          <p>En la modalidad de suscripción, las condiciones de cada plan (cantidad de usuarios, funcionalidades y precio) son las publicadas en la sección «Planes». Los pagos se procesan a través de pasarelas de terceros, sujetas a sus propios términos; el Sistema no almacena números de tarjeta ni códigos de seguridad, y solo conserva la referencia, el estado y el monto de cada transacción.</p>
        </section>

        <section>
          <h3>10. Propiedad intelectual</h3>
          <p>El Sistema fue desarrollado en el marco de un trabajo de tesis de grado de la carrera de Ingeniería en Sistemas de la Universidad de la Integración de las Américas (UNIDA), en y para Roshka S.A. Los componentes de terceros que lo integran se utilizan conforme a sus respectivas licencias de código abierto. Queda prohibida la copia, modificación o distribución del Sistema sin autorización expresa.</p>
        </section>

        <section>
          <h3>11. Suspensión y baja de cuentas</h3>
          <p>El Administrador podrá inactivar una cuenta ante el incumplimiento de estos Términos, la desvinculación del usuario de la organización o un riesgo para la seguridad del Sistema. La inactivación no elimina los registros de auditoría ni las decisiones asociadas, que se conservan como historial trazable.</p>
        </section>

        <section>
          <h3>12. Limitación de responsabilidad</h3>
          <p>Roshka S.A. no será responsable por daños derivados del uso indebido del Sistema, de la carga de información inexacta, de decisiones de despliegue adoptadas por los usuarios ni de fallas atribuibles a servicios de terceros.</p>
        </section>

        <section>
          <h3>13. Datos personales</h3>
          <p>El tratamiento de los datos personales de los usuarios se rige por la Política de Privacidad del Sistema, elaborada conforme a la Ley N.º 7593/2025 de Protección de Datos Personales.</p>
        </section>

        <section>
          <h3>14. Modificaciones</h3>
          <p>Roshka S.A. podrá modificar estos Términos cuando resulte necesario. La nueva versión se publicará en el Sistema con su fecha de vigencia, y el uso posterior implica su aceptación.</p>
        </section>

        <section>
          <h3>15. Ley aplicable y jurisdicción</h3>
          <p>Estos Términos se rigen por las leyes de la República del Paraguay. Cualquier controversia será sometida a los tribunales ordinarios de la ciudad de Asunción.</p>
        </section>
      </div>

      <p className="lm-privacy-footer">© 2026 Roshka S.A. — Todos los derechos reservados. Consultas: privacidad@roshka.com.py</p>
    </div>
  )
}

/* ─────────────────────────────────────────────────────────────────────
   Contenido: Olvidé mi contraseña
   Autoservicio en 2 pasos: 1) pedir código por correo, 2) ingresar el
   código de 6 dígitos + nueva contraseña. El backend siempre responde el
   mismo mensaje genérico en el paso 1, exista o no la cuenta.
───────────────────────────────────────────────────────────────────── */
const CODIGO_RE = /^\d{6}$/

function ForgotPasswordContent() {
  const [paso, setPaso] = useState(1)
  const [email, setEmail] = useState('')
  const [codigo, setCodigo] = useState('')
  const [nuevaPassword, setNuevaPassword] = useState('')
  const [confirmarPassword, setConfirmarPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [info, setInfo] = useState(null)
  const [listo, setListo] = useState(false)

  const handlePedirCodigo = async (e) => {
    e.preventDefault()
    setLoading(true)
    setError(null)
    try {
      const res = await api.post('/auth/forgot-password', { email })
      setInfo(res.data?.message || 'Si el correo está registrado, te enviamos un código de recuperación.')
      setPaso(2)
    } catch (err) {
      setError(err.response?.data?.message || 'No se pudo procesar la solicitud. Intentá de nuevo.')
    } finally {
      setLoading(false)
    }
  }

  const handleRestablecer = async (e) => {
    e.preventDefault()
    setError(null)

    if (!CODIGO_RE.test(codigo)) {
      setError('El código tiene 6 dígitos numéricos.')
      return
    }
    if (nuevaPassword.length < 8) {
      setError('La contraseña debe tener al menos 8 caracteres.')
      return
    }
    if (nuevaPassword !== confirmarPassword) {
      setError('Las contraseñas no coinciden.')
      return
    }

    setLoading(true)
    try {
      await api.post('/auth/reset-password-with-code', { email, codigo, newPassword: nuevaPassword })
      setListo(true)
    } catch (err) {
      setError(err.response?.data?.message || 'El código es inválido o ya venció. Pedí uno nuevo.')
    } finally {
      setLoading(false)
    }
  }

  if (listo) {
    return (
      <div className="lm-body">
        <div className="lm-header">
          <div className="lm-header-icon">DSS</div>
          <div>
            <h2 className="lm-title">Contraseña actualizada</h2>
            <p className="lm-subtitle">Ya podés iniciar sesión con tu nueva contraseña.</p>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="lm-body">
      <div className="lm-header">
        <div className="lm-header-icon">DSS</div>
        <div>
          <h2 className="lm-title">¿Olvidaste tu contraseña?</h2>
          <p className="lm-subtitle">
            {paso === 1
              ? 'Ingresá tu correo y te enviamos un código de recuperación.'
              : 'Ingresá el código que te llegó por correo y elegí una nueva contraseña.'}
          </p>
        </div>
      </div>

      <div className="lm-section">
        {paso === 1 ? (
          <form onSubmit={handlePedirCodigo} className="lm-forgot-form">
            <div className="form-group">
              <label htmlFor="fp-email">Correo electrónico</label>
              <input
                id="fp-email"
                type="email"
                className="form-input"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="usuario@roshka.com"
                autoComplete="email"
                required
              />
            </div>

            {error && (
              <div className="error-box">
                <span className="error-icon">!</span>
                <span>{error}</span>
              </div>
            )}

            <button type="submit" className="btn-login" disabled={loading}>
              {loading ? <><span className="spinner" /> Enviando…</> : 'Enviar código'}
            </button>
          </form>
        ) : (
          <form onSubmit={handleRestablecer} className="lm-forgot-form">
            {info && <p className="lm-forgot-info">{info}</p>}

            <div className="form-group">
              <label htmlFor="fp-codigo">Código de 6 dígitos</label>
              <input
                id="fp-codigo"
                type="text"
                inputMode="numeric"
                maxLength={6}
                className="form-input lm-codigo-input"
                value={codigo}
                onChange={(e) => setCodigo(e.target.value.replace(/\D/g, '').slice(0, 6))}
                placeholder="000000"
                autoComplete="one-time-code"
                required
              />
            </div>

            <div className="form-group">
              <label htmlFor="fp-nueva">Nueva contraseña</label>
              <input
                id="fp-nueva"
                type="password"
                className="form-input"
                value={nuevaPassword}
                onChange={(e) => setNuevaPassword(e.target.value)}
                placeholder="Mínimo 8 caracteres"
                autoComplete="new-password"
                required
              />
            </div>

            <div className="form-group">
              <label htmlFor="fp-confirmar">Confirmar contraseña</label>
              <input
                id="fp-confirmar"
                type="password"
                className="form-input"
                value={confirmarPassword}
                onChange={(e) => setConfirmarPassword(e.target.value)}
                placeholder="Repetí la nueva contraseña"
                autoComplete="new-password"
                required
              />
            </div>

            {error && (
              <div className="error-box">
                <span className="error-icon">!</span>
                <span>{error}</span>
              </div>
            )}

            <button type="submit" className="btn-login" disabled={loading}>
              {loading ? <><span className="spinner" /> Restableciendo…</> : 'Restablecer contraseña'}
            </button>

            <button
              type="button"
              className="lp-nav-btn lm-forgot-back"
              onClick={() => { setPaso(1); setError(null); setCodigo('') }}
              disabled={loading}
            >
              Pedir un código nuevo
            </button>
          </form>
        )}
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────────────────────────────────
   Modal wrapper
───────────────────────────────────────────────────────────────────── */
function InfoModal({ type, onClose }) {
  useEffect(() => {
    const handleKey = (e) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [onClose])

  return (
    <div className="lm-overlay" onClick={onClose}>
      <div className="lm-modal" onClick={e => e.stopPropagation()}>
        <button className="lm-close" onClick={onClose} aria-label="Cerrar">×</button>
        {type === 'about'   && <AboutContent />}
        {type === 'plans'   && <PlansContent />}
        {type === 'privacy' && <PrivacyContent />}
        {type === 'terms'   && <TermsContent />}
        {type === 'forgot'  && <ForgotPasswordContent />}
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────────────────────────────────
   Login principal
───────────────────────────────────────────────────────────────────── */
const RECORDAR_EMAIL_KEY = 'dss-recordar-email'

function Login({ onLogin }) {
  const [email, setEmail]           = useState(() => localStorage.getItem(RECORDAR_EMAIL_KEY) || '')
  const [password, setPassword]     = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [loading, setLoading]       = useState(false)
  const [error, setError]           = useState(null)
  const [activeModal, setActiveModal] = useState(null)
  const [recordar, setRecordar]     = useState(() => !!localStorage.getItem(RECORDAR_EMAIL_KEY))
  const navigate = useNavigate()

  const handleSubmit = async (e) => {
    e.preventDefault()
    setLoading(true)
    setError(null)

    try {
      const response = await api.post('/auth/login', { email, password })
      sessionStorage.setItem('token', response.data.token)
      if (recordar) localStorage.setItem(RECORDAR_EMAIL_KEY, email)
      else localStorage.removeItem(RECORDAR_EMAIL_KEY)
      onLogin()
      navigate('/')
    } catch (err) {
      if (err.response?.status === 429) {
        // El backend limita los intentos de inicio de sesión (8 cada 5 minutos por dirección IP)
        setError('Demasiados intentos de inicio de sesión. Esperá unos minutos antes de volver a intentar.')
      } else {
        setError(err.response?.data?.message || 'Credenciales inválidas. Verificá tu correo y contraseña.')
      }
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-page">
      {/* Burbujas decorativas */}
      <div className="bubble bubble-1" />
      <div className="bubble bubble-2" />
      <div className="bubble bubble-3" />
      <div className="bubble bubble-4" />

      <div className="login-split">
        {/* Panel izquierdo */}
        <div className="login-panel-left">
          <div className="brand">
            <div className="brand-icon">DSS</div>
            <h1>Decision Support System</h1>
            <p>Sistema de apoyo a la toma de decisiones para el despliegue de software en Roshka S.A.</p>
          </div>

          <div className="features">
            <div className="feature-item">
              <div className="feature-dot" />
              <span>Análisis de resultados de pruebas automatizadas</span>
            </div>
            <div className="feature-item">
              <div className="feature-dot" />
              <span>Métricas e indicadores de calidad por versión</span>
            </div>
            <div className="feature-item">
              <div className="feature-dot" />
              <span>Recomendaciones basadas en reglas configurables</span>
            </div>
            <div className="feature-item">
              <div className="feature-dot" />
              <span>Trazabilidad completa del proceso de decisión</span>
            </div>
            <div className="feature-item">
              <div className="feature-dot" />
              <span>Control de acceso por roles</span>
            </div>
          </div>

          {/* Nav de información */}
          <nav className="lp-nav">
            <button className="lp-nav-btn" onClick={() => setActiveModal('about')}>
              Sobre el sistema
            </button>
            <span className="lp-nav-sep">·</span>
            <button className="lp-nav-btn" onClick={() => setActiveModal('plans')}>
              Planes
            </button>
            <span className="lp-nav-sep">·</span>
            <button className="lp-nav-btn" onClick={() => setActiveModal('privacy')}>
              Política de privacidad
            </button>
            <span className="lp-nav-sep">·</span>
            <button className="lp-nav-btn" onClick={() => setActiveModal('terms')}>
              Términos y condiciones
            </button>
          </nav>

          <div className="panel-footer">
            <span>© 2026 Roshka S.A. - Proyecto de Tesis UNIDA</span>
          </div>
        </div>

        {/* Panel derecho */}
        <div className="login-panel-right">
          <div className="login-card">
            <div className="card-header">
              <h2>Iniciar sesión</h2>
              <p>Ingresá con tu cuenta institucional</p>
            </div>

            <form onSubmit={handleSubmit} className="login-form">
              <div className="form-group">
                <label htmlFor="email">Correo electrónico</label>
                <input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  placeholder="usuario@roshka.com"
                  className="form-input"
                  autoComplete="email"
                />
              </div>

              <div className="form-group">
                <label htmlFor="password">Contraseña</label>
                <div className="input-password-wrap">
                  <input
                    id="password"
                    type={showPassword ? 'text' : 'password'}
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                    placeholder="••••••••"
                    className="form-input"
                    autoComplete="current-password"
                  />
                  <button
                    type="button"
                    className="btn-eye"
                    onClick={() => setShowPassword((v) => !v)}
                    aria-label={showPassword ? 'Ocultar contraseña' : 'Mostrar contraseña'}
                  >
                    {showPassword ? (
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                        <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94"/>
                        <path d="M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19"/>
                        <line x1="1" y1="1" x2="23" y2="23"/>
                      </svg>
                    ) : (
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                        <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
                        <circle cx="12" cy="12" r="3"/>
                      </svg>
                    )}
                  </button>
                </div>
              </div>

              <div className="form-options">
                <label className="remember-me">
                  <input
                    type="checkbox"
                    name="remember"
                    checked={recordar}
                    onChange={(e) => setRecordar(e.target.checked)}
                  />
                  <span>Recordarme</span>
                </label>
                <button
                  type="button"
                  className="forgot-link"
                  onClick={() => setActiveModal('forgot')}
                >
                  ¿Olvidaste tu contraseña?
                </button>
              </div>

              {error && (
                <div className="error-box">
                  <span className="error-icon">!</span>
                  <span>{error}</span>
                </div>
              )}

              <button type="submit" disabled={loading} className="btn-login">
                {loading ? (
                  <><span className="spinner" /> Verificando...</>
                ) : (
                  'Ingresar al sistema'
                )}
              </button>
            </form>
            <p className="login-aviso">
              Aviso de privacidad: al ingresar aceptás la{' '}
              <button type="button" className="forgot-link" onClick={() => setActiveModal('privacy')}>Política de Privacidad</button>
              {' '}y los{' '}
              <button type="button" className="forgot-link" onClick={() => setActiveModal('terms')}>Términos y Condiciones de Uso</button>.
              {' '}Tus datos se tratan conforme a la Ley N.º 7593/2025.
            </p>
          </div>
        </div>
      </div>

      {/* Modal de información */}
      {activeModal && (
        <InfoModal type={activeModal} onClose={() => setActiveModal(null)} />
      )}
    </div>
  )
}

export default Login
