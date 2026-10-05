import { decodeJwtPayload } from './jwt'

/**
 * Roles del claim "role" del JWT actual (decodificado en vivo, igual que
 * getPermisos() más abajo) -- antes se leía de sessionStorage["userRoles"],
 * una copia separada que solo se llenaba en el login normal vía UI y podía
 * quedar desincronizada del token real (p.ej. una sesión restaurada de otra
 * forma), haciendo que isAdmin()/isGerenteQA()/etc. devolvieran resultados
 * incorrectos sin que el token en sí estuviera mal.
 */
export function getRoles() {
  const payload = decodeJwtPayload(sessionStorage.getItem('token'))
  if (!payload) return []
  const r = payload.role
  return r ? (Array.isArray(r) ? r : [r]) : []
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
