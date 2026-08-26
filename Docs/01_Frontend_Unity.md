# 01. Frontend Unity — Estado Real

**Fecha de auditoría**: 2026-08-26 · **Rama**: `MVP_escenas` · Ver advertencia sobre estado del repo en `00_Overview_Arquitectura.md`.

---

## 1. Inventario de scripts (`Assets/Scripts/`)

```
Assets/Scripts/
├── Dialogue/
│   └── DialogueManager.cs          (139 líneas)
├── Optics/
│   ├── LaserSource.cs               (51 líneas)
│   ├── BboCrystal.cs                (88 líneas)
│   ├── BeamSplitter.cs              (64 líneas)
│   └── DetectorLogic.cs             (22 líneas — stub)
├── VR/
│   ├── SimulationControllerVR.cs    (154 líneas)
│   ├── GrangierDataReader.cs        (224 líneas)
│   └── SimulationUIController.cs    (272 líneas)
└── Utils/
    └── (vacía — solo `.gitkeep`; ver nota abajo)
```

**Nota de nomenclatura (✅ resuelta)**: los nombres de archivo/clase individuales ya estaban en inglés (producto del commit `f4d59d6`, "homologacion... a ingles"); las carpetas `Dialogos/`→`Dialogue/` y `Optica/`→`Optics/` se renombraron con `git mv` para cerrar la brecha de idioma que quedaba abierta — ver fix en `03_Cumplimiento_y_Brechas.md` §4.

**Nota sobre `Utils/` (✅ resuelta)**: los 3 scripts de `VolumetricLines` (plugin de terceros para renderizar los haces láser como líneas volumétricas) y sus 4 ejemplos de demo vivían directamente dentro de `Assets/Scripts/Utils/`, mezclados con utilidades propias del proyecto. Se movieron a `Assets/ThirdPartyOverrides/VolumetricLines/` (con `Examples/` como subcarpeta) — ver fix en `03_Cumplimiento_y_Brechas.md` §5. `Scripts/Utils/` queda vacía (con `.gitkeep`, tal como la prescribe la Guía) hasta que el proyecto tenga una utilidad propia que poner ahí.

---

## 2. Los tres scripts de simulación (`VR/`)

### 2.1 `SimulationControllerVR.cs` — el puente Unity↔Python

- Define `enum ExperimentType`, `RunGrangierSimulation()`, la coroutine `RunPythonProcess()` (lanza el subproceso Python con redirección de stdout/stderr y hace streaming en vivo — ver detalle en `00_Overview_Arquitectura.md` §2.1), y `ProcessProgressLine()` (parsea cada línea JSON emitida por Python).
- Define el par de DTOs `ProgressLine` (pública, camelCase, consumida por el resto del código C#) / `ProgressLineWire` (privada, snake_case, espejo exacto de las claves JSON de Python) con un método estático `FromWire()` que traduce una a la otra.

### 2.2 `GrangierDataReader.cs` — lectura de resultados + panel flotante

- Define las DTOs `GrangierOutput` / `GrangierResults` / `SweepPoint` / `DetectorRuns` / `Run`, cada una con su contraparte `...Wire`, siguiendo el mismo patrón `FromWire()`.
- `UpdateFloatingPanels()`: lee `output.json` **una sola vez, en `Start()`**, y actualiza dos paneles flotantes en el mundo (`coincidencesPanel`, `g2Panel`) con el resultado. Usa `hwpSweep[0]` — correcto dado que Python solo escribe un punto en `hwp_sweep` (ver `02_Backend_Python.md`).
- `ShowLiveProgress(ProgressLine p)`: método completo, escrito específicamente para refrescar esos mismos paneles en tiempo real conforme llegan líneas de progreso. **✅ Ahora se invoca**: `Start()` se suscribe por código (`GetComponent<SimulationControllerVR>().OnProgressReceived.AddListener(ShowLiveProgress)`), con `RemoveListener` simétrico en `OnDestroy()` — ver fix en `03_Cumplimiento_y_Brechas.md` §1. Nota aparte: en `Scene_DosDet.unity` los campos `coincidencesPanel`/`g2Panel` siguen sin asignar en el Inspector (no hay UI world-space creada para ellos todavía), así que hoy el método corre pero sale temprano tras loguear un warning — sigue pendiente crear esa UI.
- Presente como componente en `Scene_DosDet.unity` (confirmado por GUID `3400b660265c49a48a8412f5e87ec2ed`). **Ausente** en `Scene_TresDet.unity` y `Scene_1Intro.unity`.

### 2.3 `SimulationUIController.cs` — UI de canvas flotante (loading/terminal/summary)

- Maneja un flujo de 3 vistas: `loadingView` → `terminalView` → `summaryView`.
- `RunExperiment()`: resetea estado y muestra `loadingView` antes de lanzar la simulación. Antes solo `Scene_TresDet.unity` disparaba este método; `Scene_DosDet.unity` saltaba directo a `SimulationControllerVR.RunGrangierSimulation()`. Corregido — ver `03_Cumplimiento_y_Brechas.md` §2 — ambas escenas usan hoy el mismo flujo vía `RunExperiment()`.
- `HandleProgress()`: recibe cada `ProgressLine` y actualiza `terminalView` en vivo.
- `HandleCompletion()`: construye el resumen final — `BuildGrangierSummary()` a partir del último progreso recibido en vivo, o `BuildWaveSummary()` releyendo `output.json` para el experimento de interferencia.
- `HandleError()`: maneja fallos del subproceso.
- Define también `WaveOutput` / `WaveResults` (con su `...Wire`), para el experimento de interferencia.

---

## 3. El pipeline óptico (`Optica/`)

Cadena de activación en cascada, cada script disparando al siguiente:

`LaserSource.cs` → `BboCrystal.cs` → `BeamSplitter.cs` → `DetectorLogic.cs`

- **`LaserSource.cs`**: `Update()` alterna láser + cristal con `Input.GetKeyDown(KeyCode.Space)` — **sigue dependiendo del teclado**. Contraste directo con `DialogueManager.cs`, que documenta explícitamente en comentarios que fue diseñado para no depender del teclado (ver §5).
- **`BboCrystal.cs`**: `ActivateCrystal()` / `DeactivateCrystal()` configuran 3 `LineRenderer` (bombeo, señal, testigo) con colores y posiciones propias, y encadena a `BeamSplitter`.
- **`BeamSplitter.cs`**: `ActivateSplitter()` / `DeactivateSplitter()` configuran los haces transmitido y reflejado, y encadenan a `DetectorLogic`.
- **`DetectorLogic.cs`**: **stub** — `ActivateDetectors()` solo hace 3 `Debug.Log()`, sin lógica real de detección todavía.

---

## 4. Diálogo e introducción (`Dialogos/DialogueManager.cs`)

- Sistema de diálogo tipeado (`TypeLine()` con efecto de máquina de escribir) + sistema de elección de experimento.
- Diseñado explícitamente para VR: el avance de diálogo y la selección de opciones se manejan mediante `Button.onClick`, activados por el ray interactor de los controles VR — **sin ninguna dependencia de teclado**, según el propio comentario del código ("There is no keyboard dependency anywhere in the script").
- `SelectOption(0)` → `SceneManager.LoadScene("Scene_DosDet")`; `SelectOption(1)` → `SceneManager.LoadScene("Scene_TresDet")`.
- Vive como prefab (`Assets/Prefabs/PF_DialogueManager.prefab`, junto con `PF_EventSystem.prefab`) instanciado en `Scene_1Intro.unity` — no como componente suelto en la escena. Confirmado por GUID del prefab (`02b5b195a1b1656449d55689f845e30f`, 64 referencias en la escena, correspondientes al `PrefabInstance` y sus overrides).
- Usa `[FormerlySerializedAs(...)]` en cada campo serializado renombrado (`botonAvanzar`→`advanceButton`, `panelDecisiones`→`choicesPanel`, `botonOpcion1`→`option1Button`, `botonOpcion2`→`option2Button`) — preserva los valores ya asignados en el Inspector a través del refactor de nombres español→inglés. Mismo patrón se repite en prácticamente todos los scripts del proyecto.

---

## 5. Inventario de componentes por escena (resuelto por GUID)

| Script | `Scene_1Intro` | `Scene_DosDet` | `Scene_TresDet` |
|---|---|---|---|
| `DialogueManager` (vía prefab `PF_DialogueManager`) | ✅ | — | — |
| `LaserSource` | ✅ (×1) | ✅ (×1) | ✅ (×1) |
| `BboCrystal` | ✅ (×2) | ✅ (×2) | ✅ (×2) |
| `BeamSplitter` | ✅ (×1) | ✅ (×1) | ✅ (×1) |
| `DetectorLogic` | — | ✅ (×1) | ✅ (×1) |
| `SimulationControllerVR` | — | ✅ (×1) | ✅ (×1) |
| `SimulationUIController` | — | ✅ (×1) | ✅ (×1) |
| `GrangierDataReader` | — | ✅ (×1) | **— (ausente)** |

La presencia de `BboCrystal`/`LaserSource`/`BeamSplitter` en `Scene_1Intro` corresponde a una vista previa del aparato durante el diálogo introductorio (no participa en el cálculo real, que ocurre en las escenas `DosDet`/`TresDet`).

---

## 6. XR: paquetes instalados vs. uso real (🟡 corregido)

**Corrección importante sobre este hallazgo**: la búsqueda original filtraba por un mapa de GUID construido desde `Assets/Scripts/**/*.cs.meta` — un método que **no puede** encontrar componentes de paquetes (XR Interaction Toolkit vive en `Library/PackageCache`, fuera de `Assets/Scripts/`). Es decir, el "0 coincidencias" medía una limitación de la búsqueda, no la ausencia real del componente. Repitiendo la búsqueda identificando el componente por la firma de sus campos serializados (`m_InteractionManager`, `m_SelectMode`, `m_FocusMode`, sin `m_AttachTransform`/`movementType` de un `XRGrabInteractable`) en vez de por GUID:

**El GameObject "Boton", presente en `Scene_DosDet.unity` y `Scene_TresDet.unity`, es un XR Interactable real**, y su evento `m_SelectEntered` es lo que dispara `SimulationUIController.RunExperiment()` — correr el experimento desde el control VR ya funciona vía XRI, sin teclado ni `Button.OnClick` de canvas. Ver `03_Cumplimiento_y_Brechas.md` §6 para el detalle completo de esta corrección.

Lo que sigue siendo cierto: XR Hands 1.6.2 y los paquetes de Meta/Oculus/OpenXR están instalados (`Packages/manifest.json`); no se re-verificó en este pase si la locomoción usa joystick o el sistema de locomoción de XRI (esa parte del hallazgo original se deja como estaba, sin auditar de nuevo).

Esto deja al proyecto en un estado mixto de "VR-readiness", más avanzado de lo que el hallazgo original sugería:
- `DialogueManager.cs` — migrado a interacción por ray interactor + `Button.onClick`, sin teclado.
- GameObject "Boton" (`Scene_DosDet`/`Scene_TresDet`) — XR Interactable real, `m_SelectEntered` sin teclado.
- `LaserSource.cs` — **todavía** usa `Input.GetKeyDown(KeyCode.Space)`. Este es el gap real que queda: a qué interacción VR debería responder es una decisión de diseño (¿un socket?, ¿un botón físico del laboratorio?, ¿el mismo select del botón "Boton"?) que no se resolvió en este pase de fixes — no era un bug de wiring, sino una decisión pendiente del equipo.

---

## 7. Ver también

- Hallazgo crítico (`ShowLiveProgress` nunca conectado) e inconsistencia de flujo (`Scene_DosDet` evita `SimulationUIController.RunExperiment()`): detallados con su análisis de impacto en `03_Cumplimiento_y_Brechas.md`.
- Contrato de datos consumido por estos scripts (`input.json`/`output.json`, formato de `ProgressLine`): `02_Backend_Python.md`.
