/**
 * Nombres legibles para los permisos del sistema (RBAC). El código real del
 * permiso (ej. "ver_auditoria") es el identificador técnico usado por la API
 * y las políticas de autorización — esto es solo la traducción para mostrar
 * en pantallas orientadas al usuario final (Mi perfil, alta/edición de
 * usuarios). Definido en un solo lugar para que no se desincronice entre
 * pantallas distintas.
 */
export const PERMISO_LABELS = {
  gestionar_usuarios:  'Crear, editar y activar/inactivar usuarios',
  ver_usuarios:        'Ver la lista de usuarios',
  gestionar_proyectos: 'Crear, editar y eliminar proyectos',
  ver_proyectos:       'Ver proyectos y versiones',
  cargar_resultados:   'Cargar resultados de pruebas',
  ver_resultados:      'Ver resultados de pruebas cargados',
  ejecutar_evaluacion: 'Forzar la generación de una recomendación',
  ver_evaluacion:      'Ver métricas y recomendaciones',
  gestionar_reglas:    'Configurar los umbrales de calidad',
  registrar_decision:  'Registrar una decisión de despliegue',
  ver_decisiones:      'Ver el historial de decisiones',
  ver_auditoria:       'Ver el registro de auditoría',
}

export function permisoLabel(nombre) {
  return PERMISO_LABELS[nombre] || nombre
}
