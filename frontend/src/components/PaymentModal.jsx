import '../styles/PaymentModal.css'

// ── Helpers de formato ────────────────────────────────────────────────────────

const formatGs = (n) =>
  new Intl.NumberFormat('es-PY', {
    style: 'currency',
    currency: 'PYG',
    maximumFractionDigits: 0,
  }).format(n)

// ── Iconos SVG inline ─────────────────────────────────────────────────────────

const IcClose = () => (
  <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="2.2">
    <line x1="5" y1="5" x2="15" y2="15" />
    <line x1="15" y1="5" x2="5" y2="15" />
  </svg>
)

const IcArrow = () => (
  <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="2">
    <polyline points="7 4 13 10 7 16" />
  </svg>
)

const IcLock = () => (
  <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8">
    <rect x="4" y="9" width="12" height="9" rx="2" />
    <path d="M7 9V6a3 3 0 016 0v3" />
  </svg>
)

// Logo PayPal SVG simplificado (blanco sobre azul PayPal)
const PayPalLogo = () => (
  <svg viewBox="0 0 80 22" fill="none" xmlns="http://www.w3.org/2000/svg">
    {/* "Pay" en blanco */}
    <text x="0" y="17" fontFamily="Arial, sans-serif" fontWeight="800"
          fontSize="18" fill="#ffffff">Pay</text>
    {/* "Pal" en amarillo PayPal */}
    <text x="34" y="17" fontFamily="Arial, sans-serif" fontWeight="800"
          fontSize="18" fill="#009cde">Pal</text>
  </svg>
)

// ── Componente ────────────────────────────────────────────────────────────────

/**
 * PaymentModal
 * Props:
 *   isOpen        {boolean}
 *   plan          {{ id, nombre, precioMensual }}  o null
 *   cargando      {boolean}   — deshabilita botones mientras procesa
 *   onClose       {() => void}
 *   onAdamsPay    {(planId) => void}
 *   onPayPal      {(planId) => void}
 */
export default function PaymentModal({
  isOpen,
  plan,
  cargando,
  onClose,
  onAdamsPay,
  onPayPal,
}) {
  if (!isOpen || !plan) return null

  const handleBackdrop = (e) => {
    if (e.target === e.currentTarget) onClose()
  }

  return (
    <div className="pm-backdrop" onClick={handleBackdrop} role="dialog" aria-modal="true">
      <div className="pm-modal">

        {/* ── Header ── */}
        <div className="pm-header">
          <h2 className="pm-title">Seleccioná el método de pago</h2>
          <button className="pm-close" onClick={onClose} aria-label="Cerrar">
            <IcClose />
          </button>
        </div>

        {/* ── Resumen del plan ── */}
        <div className="pm-plan-info">
          <span className="pm-plan-nombre">Plan {plan.nombre}</span>
          <span className="pm-plan-precio">
            {formatGs(plan.precioMensual)}
            <span className="pm-plan-periodo">/mes</span>
          </span>
        </div>

        {/* ── Separador ── */}
        <div className="pm-separator">
          <div className="pm-sep-line" />
          <span className="pm-sep-text">Elegí tu pasarela</span>
          <div className="pm-sep-line" />
        </div>

        {/* ── Opciones de pago ── */}
        <div className="pm-options">

          {/* AdamsPay */}
          <button
            className="pm-option"
            onClick={() => onAdamsPay(plan.id)}
            disabled={cargando}
          >
            <div className="pm-option-logo pm-option-logo--adams">
              ADAMS
            </div>
            <div className="pm-option-content">
              <span className="pm-option-name">AdamsPay</span>
              <span className="pm-option-desc">
                Tigo Money, Billetera Personal, transferencia bancaria
              </span>
            </div>
            <span className="pm-option-arrow"><IcArrow /></span>
          </button>

          {/* PayPal */}
          <button
            className="pm-option"
            onClick={() => onPayPal(plan.id)}
            disabled={cargando}
          >
            <div className="pm-option-logo pm-option-logo--paypal">
              <PayPalLogo />
            </div>
            <div className="pm-option-content">
              <span className="pm-option-name">PayPal</span>
              <span className="pm-option-desc">
                Tarjeta de crédito / débito o cuenta PayPal
              </span>
            </div>
            <span className="pm-option-arrow"><IcArrow /></span>
          </button>

        </div>

        {/* ── Footer ── */}
        <p className="pm-footer">
          <IcLock />
          Pago seguro · SSL / TLS encriptado
        </p>

      </div>
    </div>
  )
}
