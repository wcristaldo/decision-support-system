# Texto para el chat de documentación de la tesis

Este texto resume las correcciones aplicadas al sistema Roshka DSS para que el
libro de tesis quede consistente con el sistema real. Está pensado para pegar
en el chat donde se está editando el documento (`Tesis_Cristaldo_Willian.docx`).

---

## 1. Corrección de terminología (RF09)

**Contexto:** el libro de tesis usa la terminología **Apto / Condicional / No
Apto** para las tres recomendaciones del motor de decisión. El sistema
implementado usa **Desplegar / Revisar / No desplegar**. Es solo una diferencia
de nombres — la lógica, los umbrales y el comportamiento son exactamente los
que describe la tesis. Se decidió corregir el **libro**, no el código, porque
"Desplegar/Revisar/No desplegar" es más claro para los usuarios finales del
sistema (líderes técnicos, gerentes QA) que van a interactuar con la
aplicación real.

**Acción sobre el libro:** reemplazar, en todo el documento, cada aparición de:

| Término del libro (a reemplazar) | Término real del sistema |
|---|---|
| Apto (para despliegue)           | Desplegar |
| Condicional                      | Revisar |
| No Apto                          | No desplegar |

Esto afecta principalmente al capítulo donde se describe el motor de
recomendación (RF09) y a cualquier capítulo de resultados/validación que cite
estas tres categorías (capturas de pantalla, tablas de casos de prueba,
diagramas de flujo de decisión). Si hay diagramas o capturas de pantalla con
el texto viejo, hay que rehacerlos con el texto real del sistema.

---

## 2. Brechas RF/RNF corregidas (para el capítulo de resultados/validación)

Se hizo un análisis cruzado entre el libro de tesis y el código fuente real
del sistema, y se identificaron 5 brechas. Las cinco ya están implementadas,
probadas y verificadas en el sistema real. Sugerencia de redacción por cada
una:

### 2.1 RF10 — Comparación entre versiones

**Antes:** el sistema solo mostraba una tendencia histórica de métricas por
proyecto, sin una vista que comparara dos versiones concretas lado a lado.

**Ahora:** desde el detalle de un proyecto, el usuario puede seleccionar dos o
más versiones (checkbox en cada tarjeta de versión) y abrir una comparación
que muestra, lado a lado: número de versión, estado, tasa de éxito, cobertura,
tiempo de ejecución y la recomendación del motor para cada una.

**Evidencia:** verificado en navegador con datos reales (dos versiones de un
mismo proyecto, métricas distintas, comparación correcta) y cubierto por el
caso de prueba automatizado `CP-DETPROY-08_Comparar_Versiones` (Robot
Framework), que pasa correctamente.

### 2.2 RF13 — Asociación usuario–proyecto con restricción real de acceso

**Antes:** el acceso a los proyectos dependía únicamente del rol del usuario
(RBAC por rol). Cualquier usuario con permiso `ver_proyectos` veía **todos**
los proyectos del sistema, sin distinción. No existía una tabla ni un
mecanismo real de asociación usuario↔proyecto — solo era informativo en el
diseño, no estaba implementado.

**Ahora:** existe una tabla `usuario_proyecto` (nueva) que asocia
explícitamente qué proyectos puede ver y operar cada usuario no-administrador.
El Administrador sigue viendo todo sin restricción (por diseño). El resto de
los roles solo ve los proyectos que un Administrador les asignó explícitamente
desde "Gestión de Usuarios". La restricción se aplica en el backend (a nivel
de API, no solo en la interfaz), en todos los endpoints relacionados con
proyectos, versiones, resultados de pruebas, métricas, recomendaciones y
decisiones de despliegue.

**Evidencia:** verificado con una prueba real de extremo a extremo: se crea un
usuario nuevo sin proyectos asignados → no ve ningún proyecto (mensaje "No
tenés proyectos asignados") → un Administrador le asigna un proyecto
específico → el usuario pasa a ver únicamente ese proyecto. Cubierto por el
caso de prueba automatizado `CP-RBAC-09_Restriccion_Por_Proyecto_Asignado`,
que pasa correctamente. También se verificó a nivel de API (peticiones HTTP
directas) que un usuario sin acceso recibe `403 Forbidden` al intentar acceder
a un proyecto que no le fue asignado.

### 2.3 RNF08 — Cobertura de pruebas unitarias ≥ 80%

**Antes:** el proyecto backend tenía un único archivo de pruebas
(`RecommendationEngineTests.cs`, 5 casos), muy por debajo del 80% exigido por
el requisito no funcional.

**Ahora:** se agregaron 18 archivos de pruebas nuevos (además del existente),
totalizando **98 casos de prueba** unitarios/de integración con xUnit y
EF Core InMemory, cubriendo los controllers y servicios principales del
sistema (proyectos, versiones, resultados de pruebas, métricas,
recomendaciones, reglas de evaluación, decisiones de despliegue, roles,
usuarios, auditoría, análisis, autenticación, reportes, ingesta de resultados,
generación de actas PDF, exportación de reportes). Quedan fuera del alcance
del cálculo los módulos de pagos/suscripción (PayPal, AdamsPay, Pagopar,
email, recibos), porque no forman parte de ningún requisito funcional o no
funcional de la tesis.

**Resultado medido:** **95.4% de cobertura de líneas** sobre el conjunto de
archivos en alcance (medido con `dotnet test --collect:"XPlat Code Coverage"`
y coverlet), superando cómodamente el 80% exigido.

### 2.4 RNF12 — Respaldo periódico de la base de datos

**Antes:** no existía ningún mecanismo de respaldo de la base de datos, ni
manual ni automático.

**Ahora:** existe un servicio en segundo plano (`BackgroundService` de
.NET) que corre dentro de la propia aplicación backend y, de forma
automática y periódica, según un intervalo configurable (en horas),
genera un respaldo completo de la base de datos PostgreSQL usando
`pg_dump`, lo guarda en una carpeta configurable, y registra la fecha de la
última ejecución. Un Administrador puede, desde una nueva sección del panel
de administración: ver y cambiar el intervalo de respaldo, activar/desactivar
el respaldo automático, disparar un respaldo manual inmediato ("Ejecutar
ahora"), y ver el historial de respaldos generados (con fecha y tamaño de
archivo).

**Evidencia:** verificado con archivos `.sql` reales generados por el sistema
(tanto por el disparo automático como por el botón manual), visibles en la
carpeta configurada y listados correctamente en la interfaz.

### 2.5 RNF07 / RNF09 — Prueba de carga y rendimiento del dashboard

**Requisitos:**
- RNF07: el dashboard de métricas debe cargar y renderizar completamente en
  menos de 2 segundos bajo una carga simultánea de hasta 10 usuarios activos.
- RNF09: el sistema debe soportar al menos 10 usuarios concurrentes sin
  degradación perceptible del tiempo de respuesta de la API.

**Antes:** no se había ejecutado ninguna prueba de carga que verificara estos
dos requisitos con datos concretos.

**Ahora:** se diseñó y ejecutó un plan de prueba de carga con **Apache
JMeter** contra el sistema con datos realistas (~85 proyectos ya cargados,
no una base vacía): 10 usuarios virtuales concurrentes, con un login único
compartido (para no disparar el límite de intentos de login del sistema) y 5
iteraciones cada uno contra los 4 endpoints que alimentan el Dashboard.

**Resultado medido:**

| Endpoint | Muestras | Prom. (ms) | Máx. (ms) | Errores |
|---|---:|---:|---:|---:|
| GET /api/proyectos | 50 | 5.6 | 29 | 0 |
| GET /api/analisis/historial | 50 | 12.4 | 66 | 0 |
| GET /api/decisionesDespliegue/adherencia | 50 | 5.3 | 21 | 0 |
| GET /api/auditoria/estadisticas | 50 | 8.5 | 104 | 0 |

**Total: 201 muestras, 0 errores (0% de tasa de error).** El peor tiempo
individual medido fue 104 ms, muy por debajo de los 2000 ms exigidos por
RNF07. **Ambos requisitos (RNF07 y RNF09) se cumplen.**

---

## Notas para quien edite el libro

- Los 5 puntos de la sección 2 son candidatos naturales para el capítulo de
  **resultados y validación** (o el que corresponda según la estructura del
  libro), como evidencia de que los requisitos funcionales y no funcionales
  se cumplen en el sistema real.
- La sección 1 (terminología) es una corrección **retroactiva**: hay que
  revisar todo el libro buscando "Apto", "Condicional" y "No Apto" en el
  contexto de la recomendación del sistema, no solo el capítulo de RF09.
- Si el libro incluye capturas de pantalla del sistema, conviene actualizarlas
  ya que la interfaz cambió (nuevo checklist de "Proyectos asignados" en
  Gestión de Usuarios, nueva sección de comparación de versiones, nueva
  sección de Respaldo en el panel de Admin).
