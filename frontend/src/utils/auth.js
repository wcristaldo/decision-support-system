export function getRoles() {
  return JSON.parse(sessionStorage.getItem('userRoles') || '[]')
}

export function isAdmin() {
  return getRoles().includes('Administrador')
}
