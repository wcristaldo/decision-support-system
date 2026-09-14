import { useEffect, useRef, useState } from 'react'
import ResizableTh from './ResizableTh'

/**
 * <th> filtrable: al hacer click en el nombre de la columna se habilita un
 * campo de texto para buscar (substring, sin distinguir mayúsculas) dentro
 * de esa columna. Envuelve ResizableTh así que sigue soportando el drag de
 * ancho de columna.
 */
export default function FilterableTh({ children, filterValue, onFilterChange, onResizeStart, ...rest }) {
  const [editing, setEditing] = useState(false)
  const inputRef = useRef(null)
  const active = !!filterValue

  useEffect(() => {
    if (editing) inputRef.current?.focus()
  }, [editing])

  const stop = (e) => e.stopPropagation()

  return (
    <ResizableTh
      onResizeStart={onResizeStart}
      className={`dss-th-filterable ${active ? 'dss-th-filter-active' : ''}`}
      {...rest}
    >
      {editing || active ? (
        <span className="dss-th-filter-wrap" onClick={stop}>
          <input
            ref={inputRef}
            type="text"
            className="dss-th-filter-input"
            value={filterValue}
            placeholder={`Buscar...`}
            onChange={(e) => onFilterChange(e.target.value)}
            onBlur={() => { if (!filterValue) setEditing(false) }}
            onKeyDown={(e) => {
              if (e.key === 'Escape') { onFilterChange(''); setEditing(false); inputRef.current?.blur() }
            }}
          />
          {active && (
            <button
              type="button"
              className="dss-th-filter-clear"
              onMouseDown={(e) => e.preventDefault()}
              onClick={() => { onFilterChange(''); setEditing(false) }}
              aria-label={`Limpiar filtro de ${children}`}
            >
              ✕
            </button>
          )}
        </span>
      ) : (
        <button type="button" className="dss-th-filter-label" onClick={() => setEditing(true)}>
          <span>{children}</span>
          <svg className="dss-th-filter-icon" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="9" cy="9" r="6" />
            <line x1="17" y1="17" x2="13.6" y2="13.6" />
          </svg>
        </button>
      )}
    </ResizableTh>
  )
}
