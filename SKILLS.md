# Especificación técnica: Fiadex

> Registrar préstamos personales y abonos, calcular saldos pendientes y consultar el historial de pagos en una aplicación sencilla para Windows que funcione sin conexión.

**Alcance**

Incluye registrar personas, préstamos, fechas y abonos; calcular saldos pendientes y consultar el historial. No incluye transferencias, conexión con bancos, pagos en línea ni cálculo automático de intereses.

**Contexto general**

Fiadex está pensada para una persona que actualmente anota en un cuaderno el dinero que presta y los abonos que recibe. Guarda la información localmente y ayuda a llevar las cuentas, pero no realiza pagos ni transferencias.

_Generado con SkillNeitor 1.2.0 · 2026-11-12 · 0 ítems · 0 categorías_

# Definición de producto

_Decisiones registradas por el wizard de SkillNeitor. Los pasos no aplicables se muestran explícitamente._

## Fase A — Producto y experiencia

### Paso 1 — Interfaz de usuario y categorías

**Skill:** UI Type Selector
**Estado:** Completo

**Tipo de interfaz:** Desktop
**Categorías del producto:** Reportes, Catálogos, Dashboards, IA / chatbot

### Paso 2 — Experiencia de usuario y comportamiento

**Skill:** UX Behavior Pattern
**Estado:** Completo

**Flujo:** Libre
**Densidad:** Cómoda
**Feedback:** Toasts, Modales, Inline
**Navegación:** Tabs
**Accesibilidad:** WCAG AA (por defecto)

### Paso 3 — Componentes estándar de UI

**Skill:** UI Component Catalog
**Estado:** Completo

**Panel de control:** Página dedicada
**Dropdown de opciones:** En header
**Sidebar: disposición:** Colapsable
**Sidebar: comportamiento:** Pin
**Menú hamburguesa:** Siempre visible

### Paso 4 — Elementos de UI + comportamiento UX

**Skill:** Element-Behavior Mapping
**Estado:** Completo

**Formularios:** Validación inline
**Tablas:** Paginación, Orden, Filtros
**Notificaciones:** Toast, Banner
**Estados vacíos:** Con guía
**Loaders / skeletons:** Spinner
**Confirmaciones destructivas:** Modal de confirmación

## Fase B — Stack técnico y datos

### Paso 5 — Tecnologías, librerías y usos

**Skill:** Tech Stack Declaration
**Estado:** Completo

**Frontend:** C# + WPF (.NET 10), interfaz de escritorio para Windows
**Backend:** Servicios locales en .NET 10; sin servidor web. Acceso a datos con SQLite
**Librerías de UI:** Controles y estilos nativos de WPF; sin librerías externas al inicio
**Gestión de estado:** MVVM con CommunityToolkit.Mvvm
**Testing:** xUnit; probar especialmente los cálculos de saldos y abonos
**Build:** dotnet publish para Windows x64, autocontenido y portable, sin instalador
**Uso declarado de cada pieza:** WPF presenta las pantallas y formularios; .NET maneja las reglas y cálculos; SQLite conserva los datos localmente; MVVM conecta la interfaz con la lógica; xUnit valida los cálculos; dotnet publish genera la versión portable

### Paso 6 — Estilo de backend y principios

**Skill:** Backend Style & Principles
**Estado:** Completo

**Estilo:** Monolito modular
**Principios:** SOLID, KISS

### Paso 7 — Tipos de base de datos

**Skill:** Data Store Selector
**Estado:** Completo

**Tipos:** Relacional (SQL)
**Justificación:** SQLite es adecuado porque Fiadex funciona localmente y sus datos tienen relaciones claras: una persona puede tener varios préstamos y cada préstamo puede tener varios abonos. SQL facilita consultar el historial y calcular saldos de forma consistente, sin necesitar un servidor de base de datos.

### Paso 8 — Procesamiento y analítica de datos

**Skill:** Data & Analytics Pipeline
**Estado:** Completo

**Ingesta:** Manual
**Procesamiento:** Pipelines ligeros en app
**Transformación:** Limpieza, Normalización, Agregación
**Almacenamiento analítico:** Tablas agregadas

## Fase C — Inteligencia artificial y analítica

### Paso 9 — Agentes de IA (local / online)

**Skill:** AI Agent Selector
**Estado:** Completo

**Modo:** IA local
**Runtime local:** Ollama instalado en el equipo
**Proveedor online:** Otro
**Modelo:** qwen2.5:3b
**Manejo de API key:** undefined
**Rol del agente:** Generación de texto, Resumen, Agente con herramientas

### Paso 10 — Asistencia por IA y chatbot

**Skill:** AI Assistant & Chatbot
**Estado:** Completo

**¿Asistencia por IA en la app?:** Sí
**¿Chatbot?:** Sí
**Alcance del chatbot:** Consultas de datos
**Canal:** In-app
**Memoria:** Sesión
**Límites y escalado:** Solo puede consultar préstamos, abonos, saldos e historial mediante funciones de solo lectura. Fiadex calcula los saldos; el modelo no debe inventar cifras ni modificar registros. Si no encuentra la información, debe indicarlo claramente.
**Circunstancias de uso:** Siempre disponible

### Paso 11 — Métricas, KPI, EDA y visualización

**Skill:** Analytics & Visualization Catalog
**Estado:** Completo

**Métricas y KPI:** Errores, Uso por módulo, Métrica personalizada
**Fórmula y unidad de cada métrica:** Saldo pendiente total = suma de préstamos menos suma de abonos aplicados (COP). Abonos del mes = suma de abonos registrados durante el mes (COP). Préstamos activos = cantidad de préstamos con saldo pendiente mayor que cero (préstamos). Uso por módulo = cantidad de consultas por sección (conteo local). Errores = cantidad de errores de validación o guardado (conteo local).
**Estadísticas descriptivas:** Mediana
**EDA:** Valores nulos, Tendencias
**Tipos de gráficos:** Barras, Líneas
**Frecuencia de actualización:** Bajo demanda

## Fase D — Operación, seguridad y distribución

### Paso 12 — Seguridad de login/logout

**Skill:** Auth & Session Security
**Estado:** Completo

**Método de login:** Usuario/contraseña
**Duración de sesión:** 8 horas
**Renovación:** Deslizante
**Cierre por inactividad:** 15 minutos
**Logout global:** Sí
**2FA:** Sin 2FA
**Bloqueo por intentos:** 5 intentos
**Política de contraseñas:** Fuerte
**Recuperación:** Administrador

### Paso 13 — Mono usuario vs. multiusuario

**Skill:** User Model Selector
**Estado:** Completo

**Modelo:** Mono usuario
**Roles:** Admin
**Jerarquía:** Plana
**Aislamiento de datos:** Sin aislamiento

### Paso 14 — Persistencia local (localStorage / PWA)

**Skill:** Local Persistence Selector
**Estado:** Completo

**Modo de persistencia:** SQLite embebido
**¿PWA?:** No
**Sincronización con backend:** No
**Estrategia de conflicto:** undefined

### Paso 15 — Compilación y plataforma destino

**Skill:** Build & Deploy Target
**Estado:** Completo

**Compilación:** Empaquetado
**Plataformas:** Windows
**Empaquetadores:** Otro
**Notas de despliegue:** Publicar Fiadex en Release para Windows x64 con .NET 10 y opción self-contained. Entregar la carpeta publicada comprimida como ZIP portable, sin instalador ni runtime de .NET requerido. Guardar la base SQLite en %LocalAppData%\Fiadex, no junto al ejecutable. El chatbot requiere Ollama y el modelo local configurado; documentar su instalación por separado.

### Paso 16 — Repositorio y acciones asociadas

**Skill:** Repository & VCS Actions
**Estado:** Completo

**¿Maneja repositorio?:** Sí
**Proveedor:** GitHub
**Visibilidad:** Público
**Acciones:** Init, Branch, Commit, Push, Release
**Estrategia de ramas:** Feature branches
**Notas de repositorio:** Usar GitNeitor para documentar la creación del repositorio, el flujo de ramas, los commits y la publicación de la versión portable. Mantener main estable y trabajar los cambios en ramas feature. No subir datos reales de préstamos, contraseñas ni claves; usar datos de prueba. Publicar una release de Windows con el ZIP portable y los manuales.

## Resumen de decisiones

| Fase | Paso | Estado | Resumen |
| --- | --- | --- | --- |
| A | 1. Interfaz de usuario y categorías | Completo | Desktop; Reportes, Catálogos, Dashboards, IA / chatbot |
| A | 2. Experiencia de usuario y comportamiento | Completo | Libre; Cómoda; Toasts, Modales, Inline; Tabs; WCAG AA (por defecto) |
| A | 3. Componentes estándar de UI | Completo | Página dedicada; En header; Colapsable; Pin; Siempre visible |
| A | 4. Elementos de UI + comportamiento UX | Completo | Validación inline; Paginación, Orden, Filtros; Toast, Banner; Con guía; Spinner; Modal de confirmación |
| B | 5. Tecnologías, librerías y usos | Completo | C# + WPF (.NET 10), interfaz de escritorio para Windows; Servicios locales en .NET 10; sin servidor web. Acceso a datos con SQLite; Controles y estilos nativos de WPF; sin librerías externas al inicio; MVVM con CommunityToolkit.Mvvm; xUnit; probar especialmente los cálculos de saldos y abonos; dotnet publish para Windows x64, autocontenido y portable, sin instalador; WPF presenta las pantallas y formularios; .NET maneja las reglas y cálculos; SQLite conserva los datos localmente; MVVM conecta la interfaz con la lógica; xUnit valida los cálculos; dotnet publish genera la versión portable |
| B | 6. Estilo de backend y principios | Completo | Monolito modular; SOLID, KISS |
| B | 7. Tipos de base de datos | Completo | Relacional (SQL); SQLite es adecuado porque Fiadex funciona localmente y sus datos tienen relaciones claras: una persona puede tener varios préstamos y cada préstamo puede tener varios abonos. SQL facilita consultar el historial y calcular saldos de forma consistente, sin necesitar un servidor de base de datos. |
| B | 8. Procesamiento y analítica de datos | Completo | Manual; Pipelines ligeros en app; Limpieza, Normalización, Agregación; Tablas agregadas |
| C | 9. Agentes de IA (local / online) | Completo | IA local; Ollama instalado en el equipo; Otro; qwen2.5:3b; undefined; Generación de texto, Resumen, Agente con herramientas |
| C | 10. Asistencia por IA y chatbot | Completo | Sí; Sí; Consultas de datos; In-app; Sesión; Solo puede consultar préstamos, abonos, saldos e historial mediante funciones de solo lectura. Fiadex calcula los saldos; el modelo no debe inventar cifras ni modificar registros. Si no encuentra la información, debe indicarlo claramente.; Siempre disponible |
| C | 11. Métricas, KPI, EDA y visualización | Completo | Errores, Uso por módulo, Métrica personalizada; Saldo pendiente total = suma de préstamos menos suma de abonos aplicados (COP). Abonos del mes = suma de abonos registrados durante el mes (COP). Préstamos activos = cantidad de préstamos con saldo pendiente mayor que cero (préstamos). Uso por módulo = cantidad de consultas por sección (conteo local). Errores = cantidad de errores de validación o guardado (conteo local).; Mediana; Valores nulos, Tendencias; Barras, Líneas; Bajo demanda |
| D | 12. Seguridad de login/logout | Completo | Usuario/contraseña; 8 horas; Deslizante; 15 minutos; Sí; Sin 2FA; 5 intentos; Fuerte; Administrador |
| D | 13. Mono usuario vs. multiusuario | Completo | Mono usuario; Admin; Plana; Sin aislamiento |
| D | 14. Persistencia local (localStorage / PWA) | Completo | SQLite embebido; No; No; undefined |
| D | 15. Compilación y plataforma destino | Completo | Empaquetado; Windows; Otro; Publicar Fiadex en Release para Windows x64 con .NET 10 y opción self-contained. Entregar la carpeta publicada comprimida como ZIP portable, sin instalador ni runtime de .NET requerido. Guardar la base SQLite en %LocalAppData%\Fiadex, no junto al ejecutable. El chatbot requiere Ollama y el modelo local configurado; documentar su instalación por separado. |
| D | 16. Repositorio y acciones asociadas | Completo | Sí; GitHub; Público; Init, Branch, Commit, Push, Release; Feature branches; Usar GitNeitor para documentar la creación del repositorio, el flujo de ramas, los commits y la publicación de la versión portable. Mantener main estable y trabajar los cambios en ramas feature. No subir datos reales de préstamos, contraseñas ni claves; usar datos de prueba. Publicar una release de Windows con el ZIP portable y los manuales. |

---

---

<!-- Avisos:
- No hay ítems seleccionados.
-->
