import '../styles/Pagination.css'

/**
 * Paginación reutilizable (mismo patrón que el historial de pagos de
 * Suscripcion.jsx): números de página con elipsis, Anterior/Siguiente,
 * selector de tamaño de página y texto "Mostrando X–Y de Z".
 */
export default function Pagination({
  page,
  totalPages,
  onPageChange,
  pageSize,
  onPageSizeChange,
  totalItems,
  pageSizeOptions = [5, 10, 20],
}) {
  if (totalPages <= 1 && totalItems <= pageSizeOptions[0]) return null

  const paginasVisibles = (() => {
    if (totalPages <= 7) return Array.from({ length: totalPages }, (_, i) => i + 1)
    const pages = [1]
    const left  = Math.max(2, page - 1)
    const right = Math.min(totalPages - 1, page + 1)
    if (left > 2) pages.push('…')
    for (let i = left; i <= right; i++) pages.push(i)
    if (right < totalPages - 1) pages.push('…')
    pages.push(totalPages)
    return pages
  })()

  const desde = totalItems === 0 ? 0 : Math.min((page - 1) * pageSize + 1, totalItems)
  const hasta = Math.min(page * pageSize, totalItems)

  return (
    <div className="dss-pagination">
      <span className="dss-pag-info">
        {totalItems > 0
          ? `Mostrando ${desde}–${hasta} de ${totalItems} registro${totalItems !== 1 ? 's' : ''}`
          : 'Sin resultados'}
      </span>

      <div className="dss-pag-controls">
        <button
          type="button"
          className="dss-pag-btn"
          onClick={() => onPageChange(Math.max(1, page - 1))}
          disabled={page === 1}
        >
          Anterior
        </button>

        <div className="dss-pag-nums">
          {paginasVisibles.map((n, i) =>
            n === '…' ? (
              <span key={`e${i}`} className="dss-pag-ellipsis">…</span>
            ) : (
              <button
                key={n}
                type="button"
                className={`dss-pag-btn dss-pag-num ${page === n ? 'active' : ''}`}
                onClick={() => onPageChange(n)}
              >
                {n}
              </button>
            )
          )}
        </div>

        <button
          type="button"
          className="dss-pag-btn"
          onClick={() => onPageChange(Math.min(totalPages, page + 1))}
          disabled={page === totalPages}
        >
          Siguiente
        </button>

        {onPageSizeChange && (
          <select
            value={pageSize}
            onChange={(e) => onPageSizeChange(Number(e.target.value))}
            className="dss-pag-size"
          >
            {pageSizeOptions.map((n) => (
              <option key={n} value={n}>{n} por página</option>
            ))}
          </select>
        )}
      </div>
    </div>
  )
}
