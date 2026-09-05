/**
 * Decodifica el payload de un JWT de forma segura para UTF-8.
 *
 * `atob()` decodifica base64 a una "cadena binaria" (1 char = 1 byte), pero NO
 * interpreta esos bytes como UTF-8 — cualquier claim con tildes o eñes (un
 * nombre como "María", un rol como "Líder Técnico") queda con cada byte de su
 * secuencia UTF-8 multibyte convertido en un carácter suelto ("MarÃa"). Acá
 * se decodifica correctamente: primero a bytes crudos, después esos bytes se
 * interpretan como UTF-8 con TextDecoder.
 */
export function decodeJwtPayload(token) {
  if (!token) return null
  try {
    const base64Url = token.split('.')[1]
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64.padEnd(base64.length + (4 - (base64.length % 4)) % 4, '=')
    const binary = atob(padded)
    const bytes  = Uint8Array.from(binary, (c) => c.charCodeAt(0))
    const json   = new TextDecoder('utf-8').decode(bytes)
    return JSON.parse(json)
  } catch {
    return null
  }
}
