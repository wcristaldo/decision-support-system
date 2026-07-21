import '../styles/NotificationModal.css'

function NotificationModal({ isOpen, type, title, message, onClose }) {
  if (!isOpen) return null

  return (
    <div className="notif-overlay" onClick={onClose}>
      <div className="notif-modal" onClick={(e) => e.stopPropagation()}>
        <div className="notif-header">
          <div className="notif-icon-box">
            {type === 'success' ? (
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
                <path d="M22 4L12 14.01l-3-3" />
              </svg>
            ) : (
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <circle cx="12" cy="12" r="10" />
                <line x1="12" y1="8" x2="12" y2="12" />
                <line x1="12" y1="16" x2="12.01" y2="16" />
              </svg>
            )}
          </div>
          <h2 className="notif-title">{title}</h2>
          <button className="notif-close" onClick={onClose}>✕</button>
        </div>

        <div className="notif-body">
          <p className="notif-message">{message}</p>
        </div>

        <div className="notif-footer">
          <button className={`notif-btn ${type === 'success' ? 'notif-btn-ok' : 'notif-btn-err'}`} onClick={onClose}>
            {type === 'success' ? 'Aceptar' : 'Entendido'}
          </button>
        </div>
      </div>
    </div>
  )
}

export default NotificationModal
