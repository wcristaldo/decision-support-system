export function fmtFechaCorta(dateStr) {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleDateString('es-PY', { day: '2-digit', month: '2-digit' })
}

export function fmtFechaCompleta(dateStr) {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleString('es-PY', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}
