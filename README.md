# VR Optical Physics Simulator

![Unity](https://img.shields.io/badge/UNITY-VR_DEVELOPMENT-000000?style=for-the-badge&logo=unity&logoColor=white)
![C#](https://img.shields.io/badge/C%23-FRONTEND_ARCHITECTURE-512BD4?style=for-the-badge&logo=c-sharp&logoColor=white)
![Python](https://img.shields.io/badge/PYTHON-PHYSICS_BACKEND-3776AB?style=for-the-badge&logo=python&logoColor=white)
![FastAPI](https://img.shields.io/badge/FASTAPI-SSE_%2F_WEBSOCKETS_IN_PROGRESS-yellow?style=for-the-badge&logo=fastapi&logoColor=white)
![Status](https://img.shields.io/badge/STATUS-IN_DEVELOPMENT-brightgreen?style=for-the-badge)

**Un laboratorio remoto de óptica y física cuántica, simulado en Realidad Virtual.**

Construido por Ballesteros Nathalia, Moto Luis y Pérez Ernesto — colaboradores.

## Por qué existe este proyecto

Los laboratorios de óptica cuántica son caros, frágiles y escasos: una universidad no puede comprar diez cristales SPDC ni diez mesas ópticas para que cada estudiante tenga la suya. En la práctica, esto se traduce en grupos numerosos compartiendo un único montaje físico, turnos cortos, y estudiantes que nunca llegan a tocar el equipo con sus propias manos porque simplemente no alcanza el tiempo.

Este proyecto no busca sustituir el laboratorio real, sino **quitarle la barrera de acceso**. La idea es que un estudiante pueda ponerse un visor VR desde su casa, cualquier área de la facultad, o un laboratorio de cómputo, sin necesidad de reservar turno físico ni pelear por espacio en una mesa óptica compartida, y aun así:

- Armar el montaje óptico con sus propias manos y entender por qué el orden de las piezas importa.
- Ver, en tiempo real, cómo cambian las cuentas de coincidencias y la anticorrelación g² cuando mueve un half-wave plate un grado a la vez y no solo con los parametros del libro de prácticas.
- Explorar configuraciones que en un laboratorio físico compartido tomaría semanas de turnos conseguir, y con eso llegar a resultados propios y de interés para el alumno.

El objetivo es que el estudiante **aprenda** manipulando un modelo físico real, haciendo que el acceso remoto ayude a más estudiantes de los que un laboratorio físico podría atender jamás al mismo tiempo.

## El mecanismo físico y matemático detrás del simulador

Lo que corre detrás de la interfaz VR es un motor de simulación Monte Carlo que reproduce, numéricamente, dos experimentos reales de óptica cuántica.

**Experimento de Grangier (anticorrelación de fotón único).** Una fuente SPDC (*spontaneous parametric down-conversion*, mediante un cristal no lineal BBO) genera pares de fotones señal/testigo. Un half-wave plate (HWP) barre un ángulo sobre el fotón señal, que después pasa por un beamsplitter y se mide en dos configuraciones distintas:

- **2 detectores** ("luz natural"): el fotón señal se divide probabilísticamente entre dos detectores.
- **3 detectores** ("testigo"/witness): se añade un tercer detector que marca la llegada del fotón gemelo, permitiendo condicionar la medición y aislar el comportamiento de fotón único.

La cantidad físicamente relevante que calcula el backend es la función de correlación **g²**, el parámetro que demuestra (o no) el comportamiento anticorrelacionado propio de un estado de fotón único — la firma experimental que distingue luz cuántica de luz clásica. El motor corre miles de pulsos por corrida usando Monte Carlo, con topes de seguridad explícitos en el código (`MAX_NUM_PULSES`, `MAX_RUNS`, `MAX_ANGLES`, `MAX_TOTAL_OPERATIONS`) para que una simulación mal configurada no cuelgue el proceso ni agote los recursos de la máquina que la ejecuta.

**Wave Interference.** Interferencia de dos caminos: a partir de una diferencia de fase se calcula la intensidad relativa y la visibilidad de las franjas — el módulo existe en el backend, aunque todavía no está conectado a una escena de Unity (ver estado real en `Docs/00_Overview_Arquitectura.md` §3).

Cada corrida ejecuta el mismo cálculo probabilístico que describiría el experimento real, con las mismas cantidades físicas que se reportarían en un laboratorio, y por eso los resultados que ve el estudiante son resultados que puede interpretar, cuestionar y comparar.

## Arquitectura: qué corre hoy vs. hacia dónde va

(ver `Docs/00_Overview_Arquitectura.md` y `Docs/04_Plan_Maestro_Migracion.md` para el detalle completo)

**Lo que corre hoy en producción** es un patrón subprocess + streaming por `stdout` + intercambio de archivos: Unity escribe `input.json`, lanza `python main.py <experimento>` como subproceso, lee el progreso línea por línea desde `stdout` en tiempo real, y al terminar lee `output.json`. Es un patrón sencillo, sin dependencias de red, y jecutado para un experimento que corre localmente en la misma máquina que el visor VR.

**El objetivo oficial de arquitectura**, según el `PLAN_MAESTRO_10_SEMANAS` (ahora versionado en `Docs/04_Plan_Maestro_Migracion.md`), es migrar esto a una arquitectura de red centralizada en tres fases: **Fase 1** (FastAPI + Server-Sent Events, `SceneController` como punto único de navegación, contenerización con Docker), **Fase 2** (WebSockets para interacción física en tiempo real, `SimulationSession`, validación de mesa vía `TableValidator`), y **Fase 3** (nuevos experimentos, interfaces diegéticas, acabado visual con URP/PBR/Lightmapping). Esta migración es precisamente lo que habilita, a futuro, que el laboratorio deje de depender de que el backend corra en la misma máquina que el visor VR — el paso necesario para que el acceso remoto real (backend en un servidor, estudiante conectándose desde cualquier lugar) sea posible.

La migración: **Semana 1 (2026-08-26)** quedó completada — `SceneController.cs` como singleton de navegación, reestructuración de `Assets/Scripts/` por capas, y un borrador funcional de `Backend/server.py` con `POST /simulate` (FastAPI) y un placeholder de SSE. El patrón de subproceso sigue siendo el que efectivamente ejecuta cada corrida mientras dure la transición — no se retira hasta que la Fase 1, y luego la Fase 2, estén completas. El avance semana a semana se documenta en `Docs/04_Plan_Maestro_Migracion.md` §5.

## Qué ya está construido y funcionando

- Motor de física Grangier completo (SPDC, HWP, beamsplitter, cálculo de g²) con Monte Carlo y topes de seguridad, en `Backend/simulator.py`.
- Módulo de Wave Interference en `Backend/wave_simulator.py` (visibilidad aún como placeholder fijo, declarado explícitamente en el propio código).
- Tres escenas VR navegables: introducción con selección de experimento, configuración de 2 detectores y configuración de 3 detectores.
- Pipeline óptico interactivo en VR (fuente láser, cristal BBO, beamsplitter, detectores) con XR Interaction Toolkit.
- `SceneController.cs`: singleton centralizado de navegación y estado del experimento actual.
- Streaming de progreso en tiempo real desde Python hacia la UI de Unity vía `stdout`, con parseo a un DTO (`ProgressLine`) pensado para sobrevivir el cambio de transporte de red en las siguientes fases.
- Borrador funcional de API en FastAPI (`Backend/server.py`, endpoint `POST /simulate`).
- Documentación técnica completa y versionada en `Docs/` (arquitectura real, frontend, backend, brechas de cumplimiento, plan de migración y estándares de código).

## Herramientas

| Categorías | Herramientas |
| :--- | :--- |
| **Frontend & VR** | Unity, C#, XR Interaction Toolkit, Newtonsoft.Json |
| **Backend & Physics** | Python, FastAPI (en migración hacia SSE/WebSockets — ver arriba) |
| **3D Modeling & Art** | Blender, PBR Materials, Universal Render Pipeline (URP) |
| **Version Control & Architecture** | Git, Monorepo, Singleton Pattern, DTO Mapping |

## Estructura del Directorio

```text
.
├── README.md
├── .gitignore
│
├── Assets/                             # Unity Client (Frontend & VR)
│   ├── Materials/                      # PBR Materials (M_ prefix)
│   ├── Models/                         # 3D FBX Models (SM_ prefix)
│   ├── Prefabs/                        # Assembled VR interactables (PF_ prefix)
│   ├── Scripts/                        # C# Architecture, por capas
│   │   ├── Controllers/                # Simulation logic
│   │   ├── Managers/                   # SceneController (singleton) y otros managers
│   │   ├── Models/                     # DTOs mapped to snake_case
│   │   ├── Networking/                 # Cliente de red (en construcción, Fase 1/2)
│   │   ├── XR/                         # Socket and Grab interactors
│   │   ├── WebSocket/                  # Cliente WebSocket (Fase 2)
│   │   ├── Interfaces/                 # Contratos compartidos
│   │   ├── Utils/                      # Helpers and Volumetric Lines
│   │   └── Editor/                     # Herramientas de editor
│   ├── Textures/                       # Base, Normal, and Metallic maps (T_ prefix)
│   └── Scenes/                         # Scene_1Intro, Scene_DosDet, Scene_TresDet
│
├── Backend/                             # Python Server (Physics Engine)
│   ├── main.py                         # Dispatcher CLI (subproceso, vigente hoy)
│   ├── server.py                       # FastAPI — borrador de la migración (Fase 1)
│   ├── simulator.py                    # Grangier experiment logic (SPDC, HWP, g²)
│   ├── wave_simulator.py               # Wave interference calculations
│   ├── utils.py                        # I/O, streaming de progreso, helpers
│   └── requirements.txt                # Python dependencies
│
└── Docs/
    ├── 00_Overview_Arquitectura.md         # Arquitectura real (as-built) del sistema
    ├── 01_Frontend_Unity.md                # Inventario de scripts, escenas y DTOs
    ├── 02_Backend_Python.md                # Detalle de main.py, utils.py, simuladores
    ├── 03_Cumplimiento_y_Brechas.md         # Brechas frente a los estándares del equipo
    ├── 04_Plan_Maestro_Migracion.md         # Plan de 10 semanas hacia FastAPI/SSE/WebSockets
    └── ESTANDARES_DOCUMENTACION_TECNICA.md  # Guía de estándares de código y documentación
```

Para el detalle completo de arquitectura, brechas de cumplimiento y el plan de migración semana a semana, ver la carpeta `Docs/`.
