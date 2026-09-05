# Análisis exhaustivo: libro de tesis vs. sistema implementado (Roshka DSS)

> **Actualización 2026-08-31**: los puntos 1.1 (bcrypt), 1.2 (ingesta CI/CD), 1.5
> (RBAC), 1.10 (umbrales por proyecto), la sección 1.4 en lo referido a Recharts,
> y el punto 3.6 del diccionario de datos sobre trazabilidad de decisión/carga
> **ya fueron implementados** — ver `CHANGELOG.md` para el detalle completo de
> qué se hizo, qué quedó fuera de alcance y por qué. El resto de este documento
> se conserva tal cual se escribió originalmente, como registro histórico del
> diagnóstico.

Fecha del análisis: 2026-08-16
Fuentes revisadas:
- `Tesis 2026/Taller de Tesis/Tesis_Cristaldo_Willian.docx` (libro completo, ~1910 líneas extraídas)
- `Tesis 2026/Primera Instancia/ANTEPROYECTO_CRISTALDO WILLIAN.docx`
- `Tesis 2026/Segunda Instancia/ANTEPROYECTO 2DA INSTANCIA_CRISTALDO WILLIAN.docx`
- `Tesis 2026/Taller de Tesis/Revision_Samuel.docx` (comentarios del tutor)
- `Tesis 2026/Taller de Tesis/CRISTALDO WILL-segunda instancia.pdf` (planilla de evaluación, defensa 2da instancia)
- `Tesis 2026/Tesis 2026 Ingenieria UNIDA/Anteproyecto/Comentarios Temas recibidos/Comentarios Temas recibidos.docx`
- Código completo: `decision-support-system/backend/**`, `decision-support-system/frontend/src/**`, `decision-support-system/database/*.sql`

---

## 0. Lo más urgente: el documento está incompleto

El .docx todavía tiene secciones sin redactar, literalmente con el texto de plantilla de UNIDA sin completar:

- **Dedicatoria, Agradecimientos, Frase, Resumen (150–250 palabras) y Abstract**: los cinco están con el placeholder `[Redactar...]` / `[Texto del resumen...]` / `[English version...]`, sin contenido real.
- **Palabras clave / Keywords**: `[palabra 1], [palabra 2]...` sin completar.
- **Tabla de Contenido / Lista de Tablas**: varias entradas (Tabla 23 a 28, diccionario de datos) tienen número de página `xx` sin actualizar (nota del propio Word: "Ctrl+A y F9").
- **Capítulo V "Presentación de resultados"**: no contiene ningún resultado real. El texto describe qué se *va a* medir (pre-test/post-test, indicadores Likert) pero no hay una sola tabla de datos, gráfico o cifra obtenida. Es contenido metodológico repetido, no resultados.
- **Tabla 21 y 22 (casos de prueba)**: las 14 filas (CP-U01 a CP-U08, CP-I01 a CP-I05, CP-S01) tienen `Estado = Pendiente` y `Resultado obtenido = Por determinar` — **ningún caso de prueba fue ejecutado y documentado todavía**.
- **Manual técnico**: contiene literalmente `[INSERTAR URL DEL REPOSITORIO AQUÍ]`.
- **Anexos A, B, C** (carta de autorización, cuestionario pre-test, cuestionario post-test): son placeholders `[Insertar aquí...]`, no están adjuntos dentro del documento (aunque sí existen como archivos aparte en la carpeta `Taller de Tesis/`).
- **Tutores**: figuran 3 (Phd. Ariel Pedrozo, Ing. Lucia Caballero, Lic. Laura Salinas) — dato que faltaba en `CLAUDE.md`, ya lo agregué a memoria.

**Esto es lo más crítico a resolver primero**, independientemente de las inconsistencias técnicas de abajo: sin resumen, sin resultados y sin casos de prueba ejecutados, el documento no está en condiciones de defensa final todavía.

---

## 1. Discrepancias técnicas graves (tesis dice una cosa, el código hace otra)

Ordenadas por severidad — estas son las que más probablemente te pregunte un jurado si te toca defender el sistema en vivo.

### 1.1 Hashing de contraseñas: la tesis dice bcrypt, el código usa SHA-256 sin sal
La tesis afirma **repetidamente** (RNF04, diccionario de datos Tabla 23, CU-01, diagrama de secuencia, diagrama de clases, arquitectura de seguridad, conclusiones) que las contraseñas se guardan con **hash bcrypt, factor de costo ≥10 (o "12" en otro párrafo)**.

El código real (`AuthenticationService.cs`) usa **SHA-256 puro, sin salt, sin iteraciones** — no hay ninguna referencia a BCrypt en todo el backend (confirmado: no existe el paquete `BCrypt.Net` en el `.csproj`, no hay ningún `.cs` que lo mencione). Además, las contraseñas de prueba están en **texto plano en comentarios SQL** (`seed.sql`, `fix_usuarios.sql`), lo que combinado con SHA-256 sin sal las hace triviales de revertir.

**Esta es la inconsistencia más grave del documento** porque aparece en al menos 6 lugares distintos del libro y es fácilmente verificable por un jurado (alcanza con mirar el hash en la base o el código).

### 1.2 Ingesta automática vía CI/CD (`POST /api/reports`) — no existe
El libro describe con mucho detalle (CU-05, diagrama de secuencia, diagrama de actividades, diagrama de despliegue, manual técnico, manual de usuario, recomendaciones finales) un flujo donde el pipeline de CI/CD de Roshka **envía automáticamente** el JSON a un endpoint `POST /api/reports` con un "token de cuenta de servicio".

El sistema real **no tiene ese endpoint**. La carga es 100% manual desde `CargarResultados.jsx`: el usuario elige el archivo en el navegador, el JSON se parsea **en el cliente** (no en el backend) y se envía `POST /api/ResultadosPrueba` (ruta real, sin guiones, a diferencia de lo documentado) con las métricas ya calculadas — **el archivo JSON original nunca llega al backend ni se persiste**. Tampoco existe integración con GitHub Actions/GitLab CI, ni "token de cuenta de servicio".

De hecho, ese mismo endpoint real (`ResultadosPruebaController`) **no tiene `[Authorize]` en absoluto** — cualquiera sin login puede cargar resultados falsos.

### 1.3 Docker / Nginx / arquitectura física — no existe en el repo
El Capítulo IV y el plan de implementación del Capítulo V describen con mucho detalle: 3 contenedores Docker (`api`, `frontend`, `db`) orquestados con Docker Compose, red privada `sad-roshka-net`/`backend-network`, volumen nombrado `postgres_data`, Nginx como proxy inverso en el host con TLS, servidor Ubuntu 22.04 LTS, y pasos `docker compose up --build -d`.

**No existe ningún `docker-compose.yml` en el repositorio**, ni Dockerfile, ni configuración de Nginx. El propio `CLAUDE.md` del proyecto documenta cómo levantar el sistema con `dotnet run` + `npm run dev` directamente (sin contenedores). Si esta arquitectura de despliegue no se llegó a implementar, el Capítulo IV/V la describe como si ya existiera.

### 1.4 Frontend: TypeScript + Recharts + Bootstrap 5 — ninguno de los tres está presente
- La tesis dice el frontend es **"React 18 con TypeScript"** en tres lugares distintos (bases teóricas, diagrama de componentes, arquitectura lógica). El código real es **JavaScript puro** (`.jsx`, no `.tsx` — confirmé que no existe un solo archivo `.tsx` en `frontend/src`).
- La tesis dice que el dashboard usa **"gráficos de tendencia temporal implementados con la librería Recharts"**. El frontend real **no tiene ninguna librería de gráficos** — ni Recharts ni ninguna otra — instalada ni usada. No hay tendencias, ni gráficos, en ninguna pantalla.
- La tesis (bases teóricas, sección de arquitectura) dice que la interfaz **"se implementó con Bootstrap 5"**. El código real usa **CSS custom exclusivamente** — y esto además es una regla explícita y no negociable del propio `CLAUDE.md` del proyecto ("No usar Bootstrap"). Es decir, en este punto puntual el código es correcto y la tesis está desactualizada respecto a una decisión de diseño posterior.

### 1.5 Autorización (RBAC): documentada como universal, aplicada solo parcialmente
La tesis presenta el RBAC como un mecanismo transversal ("todas las rutas protegidas salvo `/api/auth/login`", matriz de trazabilidad OE-05, CU por rol, arquitectura de seguridad §4.3).

El código real: de **14 controllers, solo 4 tienen `[Authorize]` a nivel de clase** (`UsuariosController`, `AuditoriaController`, `RolesController`, `ReglaEvaluacionController`). Quedan **completamente sin autenticación**: `ProyectosController`, `VersionesController`, `ResultadosPruebaController`, `MetricasController`, `RecomendacionesController`, `DecisionesDespliegueController`, `AnalisisController`. Esto incluye el endpoint que registra la **decisión de despliegue** (`POST /api/decisionesdespliegue`) — que además nunca setea `UsuarioDecisorId` (queda siempre `null`), rompiendo la trazabilidad de "quién decidió" que la tesis reclama como aporte central (RF12, NIST SSDF, ACM Code of Ethics).

En el frontend, ninguna pantalla (`AnalisisVersion.jsx`, `Proyectos.jsx`, etc.) valida el rol antes de mostrar botones de acción — toda la restricción de rol que existe es cosmética (ocultar un link del menú) o depende 100% de que el backend responda 403, cosa que en 7 de 14 controllers ni siquiera intenta.

### 1.6 Estados de recomendación: nombres distintos en tesis, código y schema
- Tesis (mayoría de capítulos): **Apto / No Apto / Revisar** (a veces "Despliegue condicional").
- Código real (`RecommendationEngine.cs`, valor persistido): `"desplegar"` / `"no_desplegar"` / **`"desplegar_con_observaciones"`** (no `"revisar"`).
- `schema.sql` (`CHECK` constraint de `recomendaciones.tipo_recomendacion`): coincide con el código, no con la tesis.
- Dato interno de la regla individual (`EvaluacionRegla.ResultadoRegla`) sí usa el string `"revisar"` — pero el `CHECK` constraint de esa columna en `schema.sql` solo permite `('cumple','no_cumple','no_aplica')`, **sin `'revisar'`**. Si ese constraint está realmente activo en la base en uso, cualquier evaluación que caiga en tolerancia ±5% debería fallar al insertar. Vale la pena verificar contra la base real si esto está pasando silenciosamente distinto (constraint relajado a mano) o si nunca se probó un caso de tolerancia end-to-end.

### 1.7 Repository Pattern — no existe
La tesis (arquitectura lógica, diagrama de clases) dice explícitamente que la capa de datos usa **"el patrón Repositorio (Repository Pattern) con inyección de dependencias"**. El código real accede a `ApplicationDbContext` **directamente desde los controllers y servicios**, sin ninguna capa de repositorio intermedia.

### 1.8 Pruebas automatizadas — no existen
RNF08 exige cobertura de pruebas del backend ≥80%, y hay 8 casos de prueba unitarios (CP-U01 a CP-U08) más 5 de integración (CP-I01 a CP-I05) y 1 smoke test, todos con dotnet-coverage/xUnit/k6. **No existe ningún proyecto de test (`*.Tests.csproj`) en el repositorio.** Cero pruebas automatizadas ejecutables hoy, consistente con que las 14 filas de las tablas de casos de prueba figuran "Pendiente".

### 1.9 Modelo de datos: UUID vs. IDs enteros, y entidades con nombres distintos
El diccionario de datos (Tablas 23–28) describe PKs `UUID` para todas las tablas (`usuarios`, `proyectos`, `ejecuciones_prueba`, `umbrales_calidad`, `decisiones_despliegue`, `auditoria`), y nombres de entidad distintos a los reales:

| Tesis (diccionario de datos) | Código/schema real |
|---|---|
| `ejecuciones_prueba` (branch, coverage, passRate, recomendacion) | `resultados_prueba` + `metricas` + `evaluaciones` + `recomendaciones` (4 tablas separadas, sin campo `branch`) |
| `umbrales_calidad` (por proyecto, con `actualizado_por`) | `reglas_evaluacion` (globales, no por proyecto; sin campo de quién las modificó) |
| PK tipo `UUID` en todas las tablas | Backend usa IDs enteros (`Id`, según convención `ApplicationDbContext`/DTOs de los agentes de análisis) |

Esto sugiere que el diccionario de datos del Capítulo IV describe un **diseño anterior/alternativo** que no es el que terminó implementado — hay que decidir si se actualiza el diccionario o se re-explica la evolución del modelo.

### 1.10 Umbrales configurables **por proyecto** — en realidad son globales
La tesis (RF07, RF08, CU-03, diccionario `umbrales_calidad`) describe umbrales de calidad configurables **por proyecto**. El código real (`reglas_evaluacion`) es una tabla **global**: las mismas 4 reglas (`tasa_exito`, `cobertura`, `tasa_fallo`, `tiempo_ejecucion`) aplican a todos los proyectos por igual — no hay forma de tener un umbral distinto para el Proyecto A vs. el Proyecto B. Tampoco existe el `PUT` de umbrales con rango 0–100% validado como describe CU-03: el `ReglaEvaluacionController.Update` real solo permite cambiar `Umbral` y `Descripcion`, sin validación de rango explícita más allá de lo que ponga el usuario.

---

## 2. Discrepancias de alcance / negocio

### 2.1 Roles: coincide con el código real, pero no con `CLAUDE.md`
La tesis fija **4 roles**: Administrador, Analista QA, Líder Técnico, Gerente QA — esto **sí coincide** con el seed real de la base de datos (`fix_usuarios.sql`/`seed.sql`). El desactualizado es el `CLAUDE.md` del repo, que todavía lista un 5to rol "Desarrollador" que no existe. (Ya lo tengo registrado para corregir si querés que actualice `CLAUDE.md`.)

### 2.2 Planes SaaS: el Lean Canvas no se ajustó al recorte de alcance
Entre la 1ra y 2da instancia del anteproyecto, el alcance de validación se redujo de "empresas del sector privado de Asunción" a una sola empresa (Roshka S.A., censo de 8–12 personas según la versión). Pero el **Lean Canvas y los 3 planes de precios SaaS quedaron intactos** en ambas instancias y en el libro final, incluyendo cifras de proyección de ingresos ("5 clientes en Plan Básico → recuperación de inversión en 3 meses") que no se sostienen sobre una validación de una sola empresa. Es una sección que probablemente el jurado cuestione: conviene aclarar explícitamente que es una proyección a futuro y no parte de lo validado.

### 2.3 Límite de evaluaciones/mes: 3 fuentes, 3 valores distintos
- Tesis / `CLAUDE.md`: 20 (Básico) / 100 (Profesional) / ilimitado (Empresarial).
- `suscripcion_seed.sql` real: 100 (Básico) / 500 (Profesional) / ilimitado (Empresarial).
- `Login.jsx` (hardcodeado en el frontend): "100 evaluaciones/mes" (Básico) / "500" (Profesional) — coincide con el seed, no con la tesis ni `CLAUDE.md`.

Precio, cantidad de proyectos y usuarios por plan sí coinciden entre todas las fuentes (250k/500k/900k Gs.; 3/10/∞ proyectos; 10/25/∞ usuarios).

### 2.4 Observaciones del tutor (`Revision_Samuel.docx`) — no confirmadas como resueltas
No pude verificar si estas quedaron resueltas en el libro final (serían necesarias más fuentes/anexos para confirmarlo), pero al menos dos siguen visibles como huecos en el libro actual:
1. Falta evidencia documental de autorización de Roshka S.A. — el Anexo A sigue siendo un placeholder sin adjuntar dentro del .docx.
2. Falta la sección de resultados reales del pre-test/post-test — ver punto 0.

---

## 3. Bugs reales encontrados en el código (independientes de la tesis)

Estos no son inconsistencias documentales, son defectos del sistema en ejecución:

1. **[CORREGIDO en esta sesión]** El motor de recomendación reevaluaba TODOS los resultados de una versión cada vez que se cargaba uno nuevo, pisando la fecha de evaluación de los anteriores. Ver `RecommendationEngine.cs` / `ResultadosPruebaController.cs`.
2. `DecisionesDespliegueController.Create` nunca asigna `UsuarioDecisorId` — queda siempre `null` (agravado por la falta de `[Authorize]`, ni siquiera hay claim de usuario disponible para setearlo).
3. `Auditoria.jsx`: los botones "Anterior"/"Siguiente" están mal cableados — "Anterior" siempre salta a la página 1 y "Siguiente" siempre salta a la última, en vez de `pagina±1`.
4. `AuditService` y `AuditoriaService` son dos servicios de auditoría duplicados y activos simultáneamente; `ProyectosController` llama a ambos, generando 2 filas de auditoría por cada operación CRUD de proyecto, una de ellas siempre con `usuarioId = null`.
5. `PagoparService` es código muerto (no registrado en DI, sin `HttpClient` nombrado) — remanente de una pasarela de pago reemplazada por AdamsPay/PayPal. Las columnas de BD `Pagopar*` se reutilizan para AdamsPay/PayPal con nombres engañosos.
6. `seed.sql` referencia `admin@roshka.com` para vincular las 4 reglas de evaluación iniciales por `CROSS JOIN`, pero el usuario admin real es `wcristaldo@roshka.com` — si se corre `seed.sql` de cero, las reglas no se insertan y el motor de recomendación no genera nada hasta insertarlas a mano.
7. `AuthController` tiene un endpoint `POST /api/auth/debug/hash` público (sin `[Authorize]`) que devuelve el hash SHA-256 de cualquier password enviada — endpoint de debug olvidado en el controller de producción.
8. CORS configurado como `AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()` pese a llamarse `"AllowReactApp"` — no hay whitelisting real del origen del frontend.

---

## 4. Qué SÍ está alineado (para no perder de vista lo que funciona bien)

- El algoritmo central de recomendación (umbrales `mayor_igual`/`menor_igual`, tolerancia ±5%, resultado final DESPLEGAR/REVISAR/NO_DESPLEGAR según haya `no_cumple`/`revisar`) está **conceptualmente implementado tal cual lo describe el modelo de toma de decisiones del Capítulo II** — solo cambian los nombres de los valores persistidos (ver 1.6).
- Los 4 roles del RBAC coinciden entre tesis y base de datos real.
- Los 3 planes de precio (Gs. 250k/500k/900k) coinciden en casi todas las fuentes.
- JWT + expiración configurable, auditoría de acciones críticas, y la arquitectura general de 3 capas (aunque sin Repository Pattern) sí están presentes.
- El stack de base (ASP.NET Core, React+Vite, PostgreSQL+EF Core) es correcto, solo desactualizado en detalles (.NET 10 real vs. "ASP.NET Core 8" en la tesis; JS vs. TypeScript).
- Las políticas de precios de planes y las funcionalidades de `Suscripcion.jsx` (historial de pagos, PayPal, AdamsPay) son mucho más completas en el código real que lo que documenta el libro — acá el código va adelante de la tesis, no al revés.

---

## 5. Recomendación de priorización

Dado que esto es para la defensa de un trabajo de grado, sugiero este orden:

1. **Completar lo que falta en el documento** (Resumen, Abstract, Dedicatoria, Capítulo V con resultados reales, casos de prueba ejecutados, Anexos) — sin esto no hay libro que defender, independientemente del resto.
2. **Decidir, para cada discrepancia técnica grave (sección 1), si se corrige el código o se corrige el texto** — no necesariamente hay que implementar todo lo que la tesis promete (Docker, TypeScript, Recharts, Repository Pattern, ingesta CI/CD automática) antes de la defensa; muchas veces alcanza con que el Capítulo V reconozca explícitamente qué quedó como trabajo futuro (de hecho, varias ya figuran en "Recomendaciones": webhooks CI/CD, Swagger, notificaciones). Pero **bcrypt vs. SHA-256 y la falta de `[Authorize]` en 7 controllers** son los dos puntos donde recomendaría corregir el código, no el texto — son fallas de seguridad reales, no solo desprolijidad documental.
3. Los bugs de la sección 3 son arreglos rápidos y de bajo riesgo — puedo encararlos cuando quieras.

Quedo a la espera de que me digas por dónde arrancamos.
