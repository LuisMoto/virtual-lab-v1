# 00. Overview y Arquitectura Real — virtual-lab-v1

**Tipo de documento**: As-built (estado real observado en el repositorio, no aspiracional).
**Fecha de auditoría**: 2026-08-26
**Rama auditada**: `MVP_escenas`
**Commit HEAD al momento de auditar**: `f4d59d6` — "refactor: homologacion de codigo, JSON y convenciones a ingles"

> ⚠️ **Advertencia sobre el estado del repositorio**: al momento de esta auditoría hay una reorganización masiva **staged pero sin commitear** (~215 cambios: 57 altas, 97 renombres, 12 modificaciones, 9 borrados, 38 archivos nuevos sin trackear). Este documento describe el **working tree actual** (lo que hay en disco ahora, que es lo más fiel a "qué va a pasar" en el próximo commit), no únicamente el último commit. Donde el working tree difiere de HEAD de forma relevante, se indica explícitamente. Ver `03_Cumplimiento_y_Brechas.md` sección "Estado del commit en curso" para el detalle completo de qué incluye ese cambio pendiente.

---

## 1. Qué es este proyecto

`virtual-lab-v1` es un simulador de laboratorio óptico en VR (Unity) que reproduce dos experimentos reales de óptica cuántica, no un modelo de juguete:

1. **Experimento de Grangier** (anticorrelación de fotón único): una fuente SPDC (cristal no lineal BBO, *spontaneous parametric down-conversion*) genera pares de fotones señal/testigo. Un half-wave plate (HWP) barre un ángulo, la luz pasa por un beamsplitter y se mide en 2 detectores ("luz natural") o 3 detectores ("testigo"/witness). El backend calcula la función de correlación **g²** (parámetro de anticorrelación) que es la cantidad físicamente relevante del experimento.
2. **Wave Interference**: interferencia de dos caminos, calcula intensidad relativa y visibilidad a partir de una diferencia de fase.

El proyecto es multi-colaborador: además de este remoto, el historial de commits muestra merges recurrentes desde `https://github.com/ErnestGlez23/Grangier`, un fork/rama de un compañero de equipo.

---

## 2. Componentes del sistema

```
┌─────────────────────────┐        subprocess          ┌──────────────────────────┐
│   Unity (C#, VR)         │ ───────────────────────────▶│   Python (Backend/)       │
│                          │  python main.py <experimento>│                          │
│  SimulationControllerVR  │                             │  main.py (dispatcher)     │
│  SimulationUIController  │◀──── stdout: JSON por línea ─│  utils.py (I/O, helpers)  │
│  GrangierDataReader      │      (progreso en vivo)      │  simulator.py (Grangier)  │
│                          │                             │  wave_simulator.py        │
│  lee input.json (escrito │                             │                          │
│  antes de lanzar)        │                             │  lee input.json           │
│  lee output.json (al     │◀──── output.json (atómico) ─│  escribe output.json      │
│  terminar el proceso)    │                             │  (tempfile+fsync+replace) │
└─────────────────────────┘                             └──────────────────────────┘
```

**Esto NO es una arquitectura cliente-servidor con WebSocket/FastAPI** (aunque la Guía de Estándares del proyecto describe esa arquitectura en sus secciones 2, 6 y 7 — ver brecha #3 en `03_Cumplimiento_y_Brechas.md`). Es un patrón **subprocess + streaming por stdout + intercambio de archivos**, y está bien ejecutado: sencillo, sin dependencias de red, y con las protecciones que se detallan abajo.

### 2.1 Ciclo de vida de una corrida

1. Unity escribe `Backend/input.json` con `{ "configuration": { "parameters": { ... } } }`.
2. Unity lanza `python main.py <grangier_hwp|wave_interference>` como subproceso, con `RedirectStandardOutput = true` y `RedirectStandardError = true`, y se suscribe a `OutputDataReceived`.
3. Una coroutine en Unity (`RunPythonProcess()` en `SimulationControllerVR.cs`) hace `while (!process.HasExited) { ...; yield return null; }`, cediendo un frame por iteración — es decir, el polling del proceso no bloquea el hilo principal de Unity.
4. Python, mientras corre, llama a `utils.emit_progress(payload)` en puntos intermedios del cálculo, que hace `print(json.dumps(payload, ensure_ascii=False), flush=True)`. Cada línea impresa dispara `OutputDataReceived` en Unity **en tiempo real**, no al final.
5. Unity parsea cada línea (`ProcessProgressLine()`) a un DTO `ProgressLine` y dispara los callbacks correspondientes (actualiza la UI flotante vía `SimulationUIController.HandleProgress()`).
6. Al terminar, Python escribe `Backend/output.json` de forma atómica (`tempfile.mkstemp` + `os.fsync` + `os.replace`), y termina con código de salida 0 (éxito) o 1 (`result.get("status") != "ok"`).
7. Unity, al detectar `process.HasExited`, lee `output.json` para reconstruir el resumen final (`HandleCompletion()` en `SimulationUIController.cs`; y, por separado, `GrangierDataReader.UpdateFloatingPanels()` — ver brecha crítica en el doc 03).

### 2.2 Por qué importa esta distinción

El streaming por stdout es la razón por la que la UI de progreso (barra de carga, contador de corridas) puede actualizarse en vivo sin necesidad de sockets ni polling HTTP. Es una solución elegante para un experimento que corre localmente en la misma máquina que el visor VR — no hay razón para pagar la complejidad de un servidor si el consumidor está en el mismo proceso host. Vale la pena que la Guía de Estándares reconozca esto como el patrón vigente (ver doc 03).

---

## 3. Los dos experimentos

| | Grangier (`grangier_hwp`) | Wave Interference (`wave_interference`) |
|---|---|---|
| Módulo backend | `Backend/simulator.py` | `Backend/wave_simulator.py` |
| Física | SPDC + BBO + HWP sweep + beamsplitter + g² | Interferencia de dos caminos, diferencia de fase |
| Parámetros de entrada | `num_pulses`, `num_runs`, ángulos HWP | `phase_deg` |
| Salida principal | `hwp_sweep` (coincidencias, g² por modo 2/3 detectores) | `relative_intensity`, `visibility` |
| Estado de madurez | Motor Monte Carlo completo, con topes de seguridad | `visibility` es un **placeholder fijo** (`FIXED_VISIBILITY_MVP = 0.998`), no calculado — declarado explícitamente como atajo de MVP en el propio código |
| Escenas Unity que lo usan | `Scene_DosDet.unity`, `Scene_TresDet.unity` | Ninguna escena actual lo dispara desde la UI (el módulo existe y `SimulationUIController.cs` sabe construir su resumen, pero no se encontró un botón/flujo de escena que lo invoque) |

---

## 4. Escenas y navegación

Tres escenas, todas registradas en `ProjectSettings/EditorBuildSettings.asset` (en ese orden):

1. **`Scene_1Intro.unity`** — diálogo introductorio + selección de experimento. El diálogo vive en el prefab `Assets/Prefabs/PF_DialogueManager.prefab` (instanciado en la escena, junto con `PF_EventSystem.prefab`), no como componente suelto en la escena. `DialogueManager.SelectOption()` decide `SceneManager.LoadScene("Scene_DosDet")` o `SceneManager.LoadScene("Scene_TresDet")` según la opción elegida. La escena también contiene una vista previa del aparato óptico (`BboCrystal`, `LaserSource`, `BeamSplitter`).
2. **`Scene_DosDet.unity`** — configuración de 2 detectores ("luz natural"). Contiene el pipeline óptico completo (`LaserSource → BboCrystal → BeamSplitter → DetectorLogic`) más los tres scripts de simulación (`SimulationControllerVR`, `SimulationUIController`, `GrangierDataReader`).
3. **`Scene_TresDet.unity`** — configuración de 3 detectores ("testigo"/witness). Mismo pipeline óptico, mismos scripts de simulación **excepto** `GrangierDataReader`, que no está presente en esta escena.

La navegación de vuelta a la introducción, si existe, no se auditó en este pase (queda fuera de alcance; no se encontró evidencia de un botón "volver" en los scripts leídos).

---

## 5. Paquetes de Unity relevantes

Confirmado en `Packages/manifest.json`: `com.unity.xr.interaction.toolkit 3.5.0`, `com.unity.xr.hands 1.6.2`, `com.unity.xr.management 4.6.0`, `com.unity.xr.meta-openxr 1.0.4`, `com.unity.xr.oculus 4.5.4`, `com.unity.xr.openxr 1.14.3`, `com.unity.feature.vr`, `com.unity.splines 2.8.4`, `com.unity.timeline`, `com.unity.visualscripting`. Unity Editor `2022.3.62f3`.

Que estos paquetes estén instalados **no implica que se usen** en las escenas actuales — ver la brecha correspondiente en `03_Cumplimiento_y_Brechas.md`.

---

## 6. Relación con los otros documentos de esta carpeta

- `01_Frontend_Unity.md` — inventario detallado de scripts C#, escenas y el patrón Wire/DTO.
- `02_Backend_Python.md` — detalle de `main.py`, `utils.py`, `simulator.py`, `wave_simulator.py`, contratos de `input.json`/`output.json`.
- `03_Cumplimiento_y_Brechas.md` — brechas encontradas frente a `ESTANDARES_DOCUMENTACION_TECNICA.md` (que ya existía en este repo antes de esta auditoría), incluyendo el hallazgo crítico de esta sesión.
- `ESTANDARES_DOCUMENTACION_TECNICA.md` — la guía de estándares del equipo (v2.0.0, fechada 2026-08-26), preexistente a esta auditoría. No se modificó; se evalúa en el doc 03.
