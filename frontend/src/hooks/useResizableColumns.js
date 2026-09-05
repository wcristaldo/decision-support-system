import { useState, useRef, useCallback, useEffect } from 'react'

/**
 * Anchos de columna redimensionables a mano (estilo Excel), persistidos por tabla en localStorage.
 * columns: [{ key, defaultWidth, minWidth? }]
 */
export function useResizableColumns(storageKey, columns) {
  const fullKey = `dss-col-widths:${storageKey}`

  const [widths, setWidths] = useState(() => {
    const base = Object.fromEntries(columns.map(c => [c.key, c.defaultWidth]))
    try {
      const saved = JSON.parse(localStorage.getItem(fullKey) || 'null')
      if (saved) return { ...base, ...saved }
    } catch { /* ignora storage corrupto */ }
    return base
  })

  useEffect(() => {
    try { localStorage.setItem(fullKey, JSON.stringify(widths)) } catch { /* storage lleno/deshabilitado */ }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [widths])

  const dragRef = useRef(null)

  useEffect(() => {
    function onMove(e) {
      const d = dragRef.current
      if (!d) return
      const clientX = e.touches ? e.touches[0].clientX : e.clientX
      const delta = clientX - d.startX
      const min = d.minWidth || 60
      setWidths(w => ({ ...w, [d.key]: Math.max(min, d.startWidth + delta) }))
    }
    function onUp() {
      dragRef.current = null
      document.body.classList.remove('dss-col-resizing')
    }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup', onUp)
    window.addEventListener('touchmove', onMove, { passive: true })
    window.addEventListener('touchend', onUp)
    return () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup', onUp)
      window.removeEventListener('touchmove', onMove)
      window.removeEventListener('touchend', onUp)
    }
  }, [])

  const startResize = useCallback((key, minWidth) => (e) => {
    e.preventDefault()
    e.stopPropagation()
    const clientX = e.touches ? e.touches[0].clientX : e.clientX
    dragRef.current = { key, startX: clientX, startWidth: widths[key], minWidth }
    document.body.classList.add('dss-col-resizing')
  }, [widths])

  const resetWidths = useCallback(() => {
    const base = Object.fromEntries(columns.map(c => [c.key, c.defaultWidth]))
    setWidths(base)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return { widths, startResize, resetWidths }
}
