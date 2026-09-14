import { decodeJwtPayload } from './jwt'

export function getRoles() {
  return JSON.parse(sessionStorage.getItem('userRoles') || '[]')
}

/**
 * Permisos del claim "permission" del JWT actual. El backend siempre vuelve a
 * validar el permiso real en cada endpoint (DbClaimsTransformation reconstruye
 * los claims desde la base en cada request) — esto es solo para no mostrar en
 * la UI acciones que el usuario no podrá completar.
 */
export function getPermisos() {
  const payload = decodeJwtPayload(sessionStorage.getItem('token'))
  if (!payload) return []
  const p = payload.permission
  return p ? (Array.isArray(p) ? p : [p]) : []
}

export function hasPermiso(permiso) {
  return getPermisos().includes(permiso)
}

export function isAdmin() {
  return getRoles().includes('Administrador')
}

export function isGerenteQA() {
  return getRoles().includes('Gerente QA')
}

export function isLiderTecnico() {
  return getRoles().includes('Líder Técnico')
}

export function isAnalistaQA() {
  return getRoles().includes('Analista QA')
}
