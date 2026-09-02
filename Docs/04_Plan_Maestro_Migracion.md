# 04. Plan Maestro de Migración — Simulador Óptico VR

**Tipo de documento**: Plan de proyecto (fuente: `PLAN_MAESTRO_10_SEMANAS.pdf`, fecha original 2026-08-22). Este archivo es la versión versionada en Markdown del mismo plan — vive en `Docs/` junto con el resto de la documentación técnica para que no dependa de un PDF suelto y quede sujeto a control de versiones como cualquier otro documento del proyecto.
**Estado de ejecución**: en curso — Semana 1 iniciada el 2026-08-26 (ver §5, Estado de avance).

---

## 1. Resumen

La arquitectura actual del simulador opera bajo un esquema de procesamiento por lotes, donde la comunicación entre Unity y el backend de Python depende de la escritura de archivos locales (`input.json` y `output.json`) y el sondeo de subprocesos locales. Esta dependencia del disco y la dispersión de la lógica de navegación representan una limitación crítica para la escalabilidad del proyecto.

El objetivo de este Plan Maestro es ejecutar una migración hacia una **arquitectura basada en red**. Esta evolución técnica permitirá la interacción en tiempo real mediante **WebSockets** y centralizará el flujo de eventos, garantizando que el sistema sea lo suficientemente robusto para la expansión funcional de las Fases 1 a 3 sin incurrir en retrabajos estructurales durante la integración de nuevos experimentos físicos y mejoras visuales.

**Relación con el resto de la documentación**: la arquitectura vigente hoy en producción (subproceso CLI + `stdout` + archivos) está documentada en `00_Overview_Arquitectura.md` §2 y `02_Backend_Python.md`. Ese patrón **no se retira de golpe** — coexiste con esta migración durante las Fases 1 y 2, y solo se da por completado cuando el estado de avance de este documento (§5) marque como cerradas las fases correspondientes. `ESTANDARES_DOCUMENTACION_TECNICA.md` es quien traduce cada fase de este plan a convenciones de código concretas (ver su §2, §5.11, §6 y §7.4).

---

## 2. Plan de Trabajo por Fases

### Fase 1 — Infraestructura de Red y Centralización

| # | Entregable | Descripción |
|---|---|---|
| 1.1 | Reestructuración Arquitectónica | Implementación del singleton `SceneController.cs` como punto único de entrada para navegación y experimentos, desacoplando la UI y el XR Interaction Toolkit de la lógica de simulación. |
| 1.2 | Generación Dinámica de Parámetros | Eliminación del uso de `input.json` manual; captura de parámetros en memoria mediante DTOs serializables en tiempo de ejecución. |
| 1.3 | Backend como API (FastAPI + SSE) | Creación de `server.py` utilizando FastAPI para endpoints REST y `StreamingResponse` (SSE) para el reporte de progreso en vivo. |
| 1.4 | Flujo de Datos en Vivo en Unity | Sustitución de `System.Diagnostics` por `UnityWebRequest`, permitiendo la actualización de la interfaz sin lectura de disco local. |
| 1.5 | Contenerización | Implementación de Docker para el entorno de backend, facilitando el despliegue y la consistencia entre desarrolladores. |

### Fase 2 — Interacción en Tiempo Real (WebSockets)

| # | Entregable | Descripción |
|---|---|---|
| 2.1 | Gestión de Sesión Backend | Implementación de endpoints de WebSocket para actualizaciones de alta frecuencia y clase `SimulationSession` para manejar el estado en memoria. |
| 2.2 | Integración NativeWebSocket en Unity | Transición de `UnityWebRequest` a conexiones bidireccionales persistentes para manipulación física continua. |
| 2.3 | Gating de Mesa y Validación XR | Uso de `XRSocketInteractor` y `TableValidator.cs` para validación local de configuraciones antes de la apertura de red. |

### Fase 3 — Expansión y Fidelidad Visual

| # | Entregable | Descripción |
|---|---|---|
| 3.1 | Integración de Experimentos | Inclusión de dos nuevos módulos de física en el backend y sus correspondientes escenas y configuraciones en Unity. |
| 3.2 | Interfaces Diegéticas | Evolución de paneles planos a pantallas integradas visualmente en el entorno VR para una experiencia más inmersiva. |
| 3.3 | Acabado Visual Profesional | Implementación de URP, materiales PBR y Lightmapping para alcanzar estándares visuales sin comprometer el rendimiento (FPS). |

---

## 3. Cronograma del Proyecto

| Semana | Hitos Principales y Entregables |
|---|---|
| Semana 1 | Lanzamiento de `SceneController`, limpieza de C# y borrador inicial de FastAPI POST. |
| Semana 2 | Integración de `UnityWebRequest` + SSE, pruebas de flujo y Dockerización. |
| Semana 3 | Desarrollo de Endpoint WebSocket, `SimulationSession` y cliente `NativeWebSocket`. |
| Semana 4 | Configuración de `XRSocketInteractor` y lógica de `TableValidator`. |
| Semana 5 | Cierre de Fase 2: pruebas de integración, latencia y pulido de eventos de destrucción. |
| Semana 6 | Implementación de la física para nuevos experimentos en el backend. |
| Semana 7 | Creación de nuevas escenas y `TableLayoutConfig` en Unity. |
| Semana 8 | Diseño de interfaces diegéticas y refinamiento de inputs. |
| Semana 9 | Configuración de URP, PBR y procesos de Lightmapping. |
| Semana 10 | Cierre de Proyecto: control de calidad final, medición de rendimiento y pulido general. |

---

## 4. Estrategia para Evitar Retrabajos

La eficiencia del desarrollo se basa en cuatro pilares de diseño preventivo:

- **Transporte de red agnóstico**: al establecer FastAPI desde la Fase 1, la transición a WebSockets en la Fase 2 es una mejora de protocolo que no altera la estructura del servidor persistente.
- **Centralización de Control**: el `SceneController` unifica la lógica de navegación desde el inicio, permitiendo añadir validaciones de seguridad (gating) sin modificar los bindings de cada escena individualmente.
- **Invarianza del Contrato de Datos**: el DTO `ProgressLine` se mantiene constante a través de todas las fases, garantizando que los scripts de visualización no queden acoplados a un protocolo de transporte específico ni deban reescribirse cuando cambie el transporte de red.
- **Escalabilidad de Configuración**: el patrón `TableValidator`/`TableLayoutConfig` permite la expansión de experimentos mediante datos y no mediante código, reduciendo la probabilidad de errores en la Fase 3.

---

## 5. Estado de Avance

Esta sección se actualiza conforme avanza cada semana — es el punto donde este documento deja de ser solo el plan original y empieza a reflejar qué tan cerca está el código real de cumplirlo.

### Semana 1 (2026-08-26) — Completada

| Entregable del plan | Estado | Dónde |
|---|---|---|
| 1.1 — `SceneController.cs` (Singleton) | Implementado | `Assets/Scripts/Managers/SceneController.cs` — navegación (`LoadIntro`/`LoadDosDetectores`/`LoadTresDetectores`) y estado del experimento (`CurrentExperiment`) centralizados; `DialogueManager.SelectOption()` ya delega ahí. |
| Reestructuración de `Assets/Scripts/` a organización por capas | Implementado | `Controllers/`, `Managers/`, `Models/`, `Networking/` (nueva, vacía) — migrado con `git mv` para preservar GUIDs; ver `ESTANDARES_DOCUMENTACION_TECNICA.md` §3. |
| Preparación de interacción física del láser (previo a 3.3) | Implementado | `LaserSource.cs` — se quitó `Input.GetKeyDown`; se agregaron `ToggleLaser()`/`TurnOn()`/`TurnOff()` públicos, listos para conectarse en el Inspector. |
| 1.3 (parcial) — borrador inicial de FastAPI POST | Borrador funcional | `Backend/server.py` — `POST /simulate` funcional (reutiliza `simulator.run()`); `GET /simulate/stream` (SSE) es un placeholder explícito, sin progreso real todavía. `Backend/requirements.txt` agregado (`fastapi`, `uvicorn[standard]`). |
| Documentación alineada al plan | Implementado | `ESTANDARES_DOCUMENTACION_TECNICA.md` v2.2.0 — integra `SceneController` como estándar (§5.11) y documenta FastAPI/SSE/WebSocket como objetivo activo (§7.4), no como arquitectura ficticia. |

**Pendiente explícito para Semana 2** (no adelantado esta semana, a propósito): conectar el progreso real de la simulación al endpoint SSE (hoy `simulator.py` solo emite progreso por `stdout` vía `utils.emit_progress()`, un mecanismo pensado para el subproceso de `main.py`, no para un servidor persistente); implementar el cliente `UnityWebRequest` en Unity (1.4); Dockerización del backend (1.5).

### Semanas 2–10 — No iniciadas

Sin cambios sobre el plan original todavía. Se actualizará esta sección al cierre de cada semana.

---

## 6. Ver también

- `00_Overview_Arquitectura.md` §2 — arquitectura vigente hoy (subproceso + `stdout` + archivos), y cómo coexiste con esta migración.
- `02_Backend_Python.md` — detalle de los módulos que `server.py` reutiliza sin duplicar (`simulator.py`, `utils.py`).
- `ESTANDARES_DOCUMENTACION_TECNICA.md` §2, §5.11, §6, §7.4 — cómo cada fase de este plan se traduce en convenciones de código concretas.
- `03_Cumplimiento_y_Brechas.md` — brechas encontradas antes de que arrancara este plan; varias de sus recomendaciones pendientes (§4, folder por capas) ya se resolvieron como parte de la Semana 1.
