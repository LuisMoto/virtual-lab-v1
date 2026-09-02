# Guía de Estándares y Documentación Técnica
## Proyecto: Simulador Óptico VR

---

## 0. Cómo Usar Esta Guía

Este documento es la fuente única de verdad para nomenclatura, estructura y flujo de trabajo del proyecto. Aplica a todo el equipo: Frontend (Unity/C#), Backend (Python/FastAPI) y Diseño 3D (Blender).

- Todo código y asset **nuevo** debe cumplir esta guía desde el día uno.
- El código **existente** que no cumpla debe migrarse siguiendo el plan de la sección 12 (Migración de Código Legacy) — no se reescribe todo de golpe, se prioriza.
- Ante cualquier ambigüedad no cubierta aquí, se resuelve en equipo y se **agrega la regla a este documento** — no debe quedar como decisión implícita de una sola persona.

---

## 1. Reglas Generales del Proyecto

Para garantizar la escalabilidad y evitar conflictos en la integración, todo el equipo debe adherirse a las siguientes normas base:

### 1.1 Idioma

- Todo el código fuente, nombres de archivos, variables, assets, comentarios y contratos de datos (JSON) deben escribirse **estrictamente en inglés**.
- Incluye comentarios, documentación inline, mensajes de commit y nombres visibles en el Inspector de Unity.
- **Excepción única**: texto de interfaz de usuario (UI) destinado al usuario final, si el producto se distribuye en español. Ese texto debe vivir en archivos de localización separados (ver sección 11), nunca hardcodeado en el código.

### 1.2 Formato de Texto

- **Prohibido**: espacios, acentos, eñes o caracteres especiales en nombres de archivos, carpetas o variables.
- **Permitido**: guiones bajos (`_`) o convención de mayúsculas (`PascalCase`, `camelCase`).
- **Ejemplo correcto**: `optical_table`, `OpticalTable`, `maxWavelength`
- **Ejemplo incorrecto**: `optical table`, `óptico_table`, `max-wavelength` (en archivos/variables; en ramas de Git el guion medio sí aplica, ver 8.3)

### 1.3 Codificación de Archivos (Encoding)

- Todo archivo de texto (`.cs`, `.py`, `.json`, `.md`, `.shader`) debe guardarse en **UTF-8 sin BOM**.
- Configurar el editor (VS Code, Rider, Visual Studio) para forzar UTF-8 por defecto en el proyecto.
- Si un archivo muestra caracteres corruptos tipo `�` (mojibake), debe corregirse inmediatamente reabriéndolo con el encoding correcto y regrabándolo — no ignorar este síntoma, indica que el archivo se guardó con un encoding distinto (ej. Windows-1252) en algún punto de su historia.
- Verificación rápida en terminal: `file -i nombre_archivo.cs` debe reportar `charset=utf-8`.

### 1.4 Excepción para Paquetes y Librerías de Terceros

- Carpetas de paquetes importados desde el Package Manager de Unity, Asset Store, o librerías de terceros (ej. `TextMesh Pro/`, `Packages/`) **no están sujetas** a estas reglas de nomenclatura.
- No se debe renombrar, mover ni reestructurar contenido de terceros — esto rompe actualizaciones futuras del paquete y referencias internas.
- Si un paquete de terceros requiere modificación, se debe copiar la parte necesaria a una carpeta propia (ej. `Assets/ThirdPartyOverrides/`) en lugar de editar el original.

---

## 2. Estructura Raíz del Repositorio (Monorepo)

El proyecto combina un cliente Unity (VR) y un motor de física en Python. Ambos viven en el mismo repositorio pero deben mantenerse claramente separados en la raíz.

**Arquitectura en transición (Semana 1/10 del Plan Maestro, iniciada 2026-08-26)**: hoy en producción, Unity lanza `Backend/main.py` como **subproceso** por cada corrida (`python main.py <experimento>`), con `input.json` como entrada, progreso en vivo leído línea por línea desde el `stdout` del proceso (un objeto JSON por línea), y el resultado final en `output.json` (escrito de forma atómica). Este patrón (detallado en `Docs/00_Overview_Arquitectura.md` §2 y `Docs/02_Backend_Python.md`) **sigue vigente y no se retira de golpe** — coexiste con la migración mientras dura la transición de fases.

El **objetivo oficial del proyecto** (Plan Maestro, 10 semanas — ver `Docs/04_Plan_Maestro_Migracion.md` para el plan completo, el cronograma semana a semana y el estado de avance) es migrar ese subproceso local a una arquitectura de red centralizada, en 3 fases: **Fase 1** (esta) — infraestructura de red: `Backend/server.py` expone la misma lógica de `simulator.py`/`utils.py` vía **FastAPI**, primero con un endpoint `POST` síncrono y, como paso intermedio hacia progreso en vivo, un endpoint `StreamingResponse` de **Server-Sent Events (SSE)**; y del lado de Unity, el Singleton `SceneController` (ver 5.11) centraliza la navegación y el estado del experimento como paso previo a alojar ahí el futuro cliente de red. **Fase 2** — sustituir SSE por **WebSockets** (sesión persistente vía una futura `SimulationSession`) para progreso bidireccional en tiempo real. **Fase 3** — expansión de experimentos y fidelidad visual sobre esa base ya centralizada. Mientras el Backend siga aceptando ambos caminos (subproceso CLI y HTTP), agregar un experimento nuevo debe registrarse en ambos dispatchers (`main.py` y `server.py`) para no crear una brecha entre los dos.

```
proyecto_root/
├── Assets/                    # Proyecto Unity — ver sección 3
├── Packages/                  # Generado por Unity Package Manager (no tocar manualmente)
├── ProjectSettings/           # Configuración de Unity (versionar)
├── Backend/                   # Motor de física en Python — subproceso CLI vigente + API FastAPI en migración (Fase 1)
│   ├── main.py                 # Dispatcher CLI (vigente): lee argv/input.json, despacha al módulo del experimento, escribe output.json
│   ├── server.py                # Dispatcher HTTP (Fase 1, en construcción): FastAPI — POST /simulate (funcional) + GET /simulate/stream (borrador SSE)
│   ├── requirements.txt         # Dependencias — fastapi, uvicorn[standard] (agregado en Fase 1)
│   ├── utils.py                 # I/O compartido: escritura atómica, lectura con reintentos, validación, streaming de progreso
│   ├── simulator.py             # Experimento Grangier (HWP)
│   ├── wave_simulator.py        # Experimento Wave Interference
│   ├── input.json               # Entrada de la corrida más reciente (gitignored, la escribe Unity en runtime) — solo lo usa main.py
│   └── output.json              # Salida de la corrida más reciente (gitignored, la escribe Python en runtime) — solo lo usa main.py
├── Source_Models/             # Archivos nativos .blend (NO entran a Assets/)
│   └── optical_components/
├── Source_Design/             # Archivos nativos .psd, .ai (NO entran a Assets/)
│   └── ui_mockups/
├── Docs/                      # Documentación del proyecto
│   ├── ESTANDARES_DOCUMENTACION_TECNICA.md
│   └── architecture/
├── .gitignore
├── .gitattributes
└── README.md
```

**Pendiente recomendado**: `Backend/tests/` con la suite `pytest` de la sección 9.2 (sigue sin existir). `requirements.txt` ya se agregó en esta fase (ver arriba); todavía no fija versiones exactas de `fastapi`/`uvicorn` — evaluar pinning una vez que el endpoint SSE deje de ser un borrador. Cualquier experimento nuevo debería sumar al menos un test ahí antes de integrarse a los dispatchers de `main.py`/`server.py`.

**Regla clave**: nunca mezclar archivos sueltos en la raíz. Si aparece un `.py`, `.json` de datos, o script suelto en `proyecto_root/` fuera de `Backend/`, debe moverse a su carpeta correspondiente en el siguiente commit disponible.

---

## 3. Estructura de Carpetas (Unity `Assets/`)

La organización dentro de `Assets/` debe seguir esta estructura para mantener claridad y evitar caos al escalar:

```
Assets/
├── Scripts/
│   ├── Controllers/          # Controladores principales de escena/simulación (p. ej. LaserSource, BboCrystal, SimulationControllerVR)
│   ├── Managers/              # Gestores (SceneController, DialogueManager, SimulationUIController — ver 5.11)
│   ├── Networking/             # Fase 1.3/1.4: cliente HTTP/SSE hacia Backend/server.py (UnityWebRequest) — vacía hasta que se implemente
│   ├── Interfaces/            # Interfaces (IOpticalComponent, etc.)
│   ├── Models/                 # DTOs y estructuras de datos (p. ej. GrangierDataReader)
│   ├── Utils/                  # Utilidades y helpers
│   ├── WebSocket/              # Fase 2: cliente WebSocket (NativeWebSocket) — vacía hasta esa fase
│   ├── XR/                     # Lógica de interacción VR (sockets, grabbers) — vacía hasta que se muevan/agreguen scripts XR dedicados
│   └── Editor/                 # Scripts exclusivos del editor
├── Models/                    # Archivos .fbx importados (modelos 3D)
├── Materials/                  # Archivos .mat (materiales Unity)
├── Textures/                   # Texturas .png, .tga (organizadas por objeto)
├── Prefabs/                    # Prefabs .prefab (componentes reutilizables)
├── Scenes/                     # Escenas .unity
├── ScriptableObjects/          # Assets de datos (.asset) definidos por SO
├── Animations/                 # Animator Controllers y clips
├── Audio/                      # Clips de audio (SFX, música)
├── Shaders/                    # Shaders custom .shader / .shadergraph
├── UI/                         # Sprites, fuentes e íconos exclusivos de UI
├── Localization/                # Archivos de texto localizado (ver sección 11)
├── Tests/                      # Tests NUnit — estructura espejo de Scripts/
├── Resources/                  # Assets cargados dinámicamente en runtime
├── Settings/                   # Perfiles URP/render (generado por Unity)
└── ThirdPartyOverrides/         # Copias modificadas de assets de paquetes (ver 1.4)
```

**Nota sobre carpetas de paquetes**: `TextMesh Pro/` u otras carpetas creadas automáticamente por paquetes importados se mantienen en su ubicación original y quedan **exentas** de esta estructura (ver 1.4).

---

## 4. Nomenclatura de Arte y Assets (Blender a Unity)

El flujo de trabajo entre Blender y Unity requiere organización estricta para que físicas (XR Sockets), materiales PBR y animaciones funcionen correctamente.

### 4.1 Prefijos Estándar por Tipo de Asset

| Tipo de Asset | Prefijo | Convención | Ejemplo |
|---|---|---|---|
| Modelo 3D / Malla Estática | `SM_` | PascalCase | `SM_OpticalTable.fbx`, `SM_HalfWavePlate.fbx` |
| Malla con animación (skinned) | `SK_` | PascalCase | `SK_RoboticArm.fbx` |
| Material Unity | `M_` | PascalCase | `M_DarkMetal.mat`, `M_OpticalGlass.mat` |
| Prefab | `PF_` | PascalCase | `PF_LaserEmitter.prefab`, `PF_Detector.prefab` |
| Textura – Albedo/Color | `T_` + Nombre + `_Albedo` | snake_case | `T_optical_table_albedo.png` |
| Textura – Normal | `T_` + Nombre + `_Normal` | snake_case | `T_optical_table_normal.png` |
| Textura – Metallic | `T_` + Nombre + `_Metallic` | snake_case | `T_optical_table_metallic.png` |
| Textura – Roughness | `T_` + Nombre + `_Roughness` | snake_case | `T_optical_table_roughness.png` |
| Textura – Ambient Occlusion | `T_` + Nombre + `_AO` | snake_case | `T_optical_table_ao.png` |
| Shader custom | `SH_` | PascalCase | `SH_OpticalGlassRefraction.shader` |
| ScriptableObject (asset de datos) | `SO_` | PascalCase | `SO_ExperimentConfig.asset` |
| Animator Controller | `AC_` | PascalCase | `AC_LaserEmitter.controller` |
| Clip de animación | `ANIM_` | PascalCase | `ANIM_MirrorRotate.anim` |
| Audio – Efecto de sonido | `SFX_` | PascalCase | `SFX_LaserFire.wav` |
| Audio – Música | `MUS_` | PascalCase | `MUS_AmbientLab.mp3` |
| Escena | `Scene_` | PascalCase | `Scene_MainMenu.unity`, `Scene_DoubleSlitLab.unity` |

### 4.2 Reglas de Exportación Blender → Unity

Antes de exportar cualquier archivo `.fbx` desde Blender, garantizar:

1. **Escala 1:1**
   - Aplicar todas las transformaciones (`Ctrl+A` → *Apply All Transforms*) antes de exportar.
   - En Unity debe leerse como `Scale: (1, 1, 1)` donde 1 unidad = 1 metro.
   - Verificar en Blender que las unidades de escena estén en **métrico** (Scene Properties → Units → Metric).
2. **Pivote (Origen)**
   - El punto de origen 3D debe situarse en la **base del objeto** (donde hace contacto con la mesa o superficie), no en el centro geométrico.
   - Usar `Object → Set Origin → Origin to Geometry` como punto de partida, luego ajustar manualmente moviendo el objeto sobre el pivote (`Shift+S` → *Cursor to World Origin*, luego `Object → Set Origin → Origin to 3D Cursor`).
3. **Orientación**
   - Aplicar transformaciones para que, una vez importado a Unity:
     - Eje **Y** apunte hacia arriba (vertical)
     - Eje **Z** apunte hacia el frente (profundidad)
     - Eje **X** apunte hacia la derecha (horizontal)
   - Blender usa Z-up nativamente; al exportar a FBX, usar la opción `Forward: -Z Forward` y `Up: Y Up` en el diálogo de exportación para que la conversión sea automática y consistente.
4. **Topología y Limpieza**
   - Triángulos limpios, sin geometría duplicada ni caras invertidas (revisar normales con `Overlay → Face Orientation`).
   - Eliminar armaduras, empty objects, cámaras y luces de la escena de Blender antes de exportar (a menos que sea una malla con rig intencional).
   - Aplicar todos los modificadores (Subdivision, Mirror, Boolean, etc.) antes de exportar — Unity no los interpreta.
   - Normal maps generadas y aplicadas correctamente en Blender si el modelo las requiere.
5. **UV Mapping**
   - Todo modelo debe tener al menos un UV channel limpio, sin solapamientos que no sean intencionales.
   - Evitar dejar UVs por defecto sin desempaquetar (`Unwrap`) en modelos que llevarán texturas custom.
6. **Configuración de Exportación FBX**
   - Formato: `.fbx` binario, versión FBX 2020 o superior.
   - Opciones recomendadas en el exportador de Blender:
     - `Scale`: 1.0
     - `Apply Scalings`: FBX All
     - `Forward`: -Z Forward
     - `Up`: Y Up
     - `Apply Unit`: activado
     - `Bake Animation`: activado únicamente si el modelo tiene animación
     - `Path Mode`: Copy (para incluir texturas embebidas si aplica, aunque se recomienda mantener texturas separadas)
7. **Configuración de Importación en Unity**
   - Al importar el `.fbx`, verificar en el Inspector:
     - `Scale Factor`: 1 (ajustar solo si el modelo se ve con tamaño incorrecto, y documentar por qué)
     - `Mesh Compression`: Off (o Low si el tamaño de build es crítico)
     - `Read/Write Enabled`: activar solo si el mesh necesita modificarse en runtime
     - `Generate Colliders`: evaluar caso por caso — para simulaciones de física óptica, preferir colliders simples (Box/Capsule) agregados manualmente en vez de Mesh Colliders automáticos

### 4.3 Jerarquía en la Escena (Unity Hierarchy)

Dentro del panel **Hierarchy** de Unity, los objetos no llevan extensiones ni prefijos técnicos. Deben nombrarse de forma **limpia y descriptiva** usando `PascalCase`:

| Correcto | Incorrecto |
|---|---|
| `OpticalTable` | `SM_Table_final_v2` |
| `MainCamera` | `Camera` |
| `LaserEmitter` | `emitter` |
| `Socket_HalfWavePlate` | `Socket_hwp` |
| `DetectorArrow` | `detector_arrow_LOD0` |
| `XRRig` | `XR Rig (1)` |

### 4.4 Nomenclatura de Interacción XR

Componentes de interacción específicos de VR siguen prefijos descriptivos para diferenciarlos de la geometría pasiva:

| Elemento | Prefijo | Ejemplo |
|---|---|---|
| Socket de encaje (XR Socket Interactor) | `Socket_` | `Socket_HalfWavePlate`, `Socket_Mirror01` |
| Objeto agarrable (XR Grab Interactable) | `Grabbable_` | `Grabbable_Polarizer` |
| Zona de interacción/trigger | `Zone_` | `Zone_DetectorRange` |
| Punto de anclaje/anchor | `Anchor_` | `Anchor_OpticalBench` |

---

## 5. Convenciones de Código C# (Frontend – Unity)

El código del Frontend debe ser modular y seguir las convenciones de C# estándar para mantener los scripts legibles.

### 5.1 Archivos y Clases

- Convención: **PascalCase**
- El nombre del archivo `.cs` debe coincidir **exactamente** con el nombre de la clase.
- Un archivo = una clase pública (excepciones: clases de datos pequeñas y estrechamente relacionadas, como un `enum` que solo usa esa clase).

```csharp
// Correcto: SceneController.cs
public class SceneController : MonoBehaviour
{
    // ...
}
// Incorrecto: scene_controller.cs, Controllers.cs, CamaraLibre.cs
```

### 5.2 Métodos y Funciones

- Convención: **PascalCase**
- Nombres verbales que describan la acción, en inglés.

```csharp
public void ProcessProgressLine() { }
public void RequestExperiment() { }
public bool ValidateParameters() { }
public IEnumerator SimulateOpticalPath() { }
```

### 5.3 Propiedades

- Convención: **PascalCase**
- Usar propiedades auto-implementadas cuando sea posible.

```csharp
public float MaxWavelength { get; set; }
public SimulationState CurrentState { get; private set; }
```

### 5.4 Variables Públicas y Serializadas

- Convención: **camelCase**
- Preferir `[SerializeField] private` sobre `public` directo — expone el campo al Inspector sin permitir que otros scripts lo modifiquen libremente.

```csharp
// Mejor práctica
[SerializeField] private string pythonPath;
[SerializeField] private float targetAngle;
[SerializeField] private int maxIterations = 100;
[SerializeField, Range(0f, 1f)] private float reflectivity = 0.95f;
// Evitar cuando no sea estrictamente necesario exponer el campo a otros scripts
public string pythonPath;
public float targetAngle;
```

### 5.5 Variables Privadas

- Convención: **camelCase precedido por guion bajo** `_`

```csharp
private float _totalExpectedRuns;
private string _latestProgressLine;
private List<SimulationResult> _cachedResults;
private bool _isSimulationRunning;
```

### 5.6 Constantes

- Convención: **UPPER_SNAKE_CASE**. Se fija este único formato (en la v1.0 se permitían dos; se estandariza para evitar inconsistencia entre scripts).

```csharp
private const float MAX_WAVELENGTH = 700f;
private const int DEFAULT_ITERATIONS = 50;
private const string CONFIG_PATH = "Assets/Config/simulation.json";
```

### 5.7 Eventos (UnityEvents / Actions)

- Convención: **PascalCase precedido por `On`**

```csharp
public UnityEvent OnSimulationCompleted;
public UnityEvent OnProgressReceived;
public System.Action<float> OnAngleChanged;
public System.Action<SimulationResult> OnResultsReady;
```

### 5.8 Interfaces

- Convención: **PascalCase precedido por `I`**

```csharp
public interface IOpticalComponent
{
    float Interact(float incomingPhotons, ref Vector3 rayDirection, ref Vector3 rayPosition, out bool absorbRay);
}
```

### 5.9 Enums

- Convención: **PascalCase** para el tipo y cada valor.

```csharp
public enum DetectorMode
{
    Aperture,
    FullField,
    SinglePoint
}
```

### 5.10 Documentación Inline (XML Documentation)

- Agregar comentarios XML para todo método público, propiedad pública y clase pública.

```csharp
/// <summary>
/// Procesa una línea de progreso enviada por el servidor Python.
/// </summary>
/// <param name="progressLine">Línea de texto recibida del servidor.</param>
/// <returns>True si la línea fue procesada correctamente.</returns>
public bool ProcessProgressLine(string progressLine) { }
/// <summary>
/// Ángulo objetivo del componente, en grados.
/// </summary>
[SerializeField] private float targetAngle;
```

### 5.11 Singleton Centralizado de Escena (`SceneController`) — estándar de arquitectura (Fase 1.1)

A partir de la Semana 1 del Plan Maestro, `SceneController` (en `Managers/`) es el **punto único de entrada** para dos responsabilidades que antes vivían dispersas en scripts de UI/diálogo: la navegación entre escenas (`SceneManager.LoadScene()`) y el estado de qué experimento eligió el usuario. Ningún script de UI o de diálogo debe llamar a `SceneManager.LoadScene()` directamente — debe pasar por un método público de `SceneController` (`LoadIntro()`, `LoadDosDetectores()`, `LoadTresDetectores()`), incluso si ese método hoy es una envoltura simple.

- Patrón: Singleton con auto-instanciación perezosa (lazy self-instantiation) vía una propiedad estática `Instance` que crea su propio `GameObject` con `DontDestroyOnLoad()` si todavía no existe una instancia — así no es obligatorio arrastrarlo a mano a ninguna escena ni editar los `.unity` para que funcione.
- `Awake()` debe reforzar la unicidad del singleton (`Destroy(gameObject)` si ya existe otra instancia), por si el objeto también se colocó manualmente en una escena.
- Este singleton es también el lugar designado para alojar, en las Fases 1.3/1.4, el futuro cliente de red (HTTP/SSE hacia `Backend/server.py`, y más adelante WebSocket) — ver el `TODO` marcado al final de `SceneController.cs`. Cualquier lógica de comunicación con el Backend que hoy vive dispersa en `SimulationControllerVR.RunPythonProcess()` debe migrar hacia acá cuando se implemente esa fase, no duplicarse en otro script.

```csharp
// Correcto — UI delega la navegación al singleton centralizado
SceneController.Instance.LoadDosDetectores();

// Incorrecto — llamar a SceneManager directamente desde un script de UI/diálogo
SceneManager.LoadScene("Scene_DosDet");
```

---

## 6. Convenciones de Código Python (Backend)

El motor de física en Python debe seguir **PEP 8**, priorizando claridad en el procesamiento matemático y en el contrato de datos que cruza hacia Unity (`input.json` / `output.json` / líneas de progreso por `stdout`, y ahora también el body/response de `server.py`). Todo el código Python vive dentro de `Backend/` (ver sección 2), nunca en la raíz del repositorio.

**Dos dispatchers conviven durante la migración (Fase 1 del Plan Maestro)**: `main.py` es el dispatcher CLI vigente — cada corrida es una invocación de proceso independiente (`python main.py <experimento>`), lanzada por Unity, con vida útil igual a la de esa corrida (arquitectura completa en `Docs/00_Overview_Arquitectura.md` §2 y `Docs/02_Backend_Python.md`). `server.py` es el nuevo dispatcher HTTP (FastAPI), en construcción — expone la misma lógica de los módulos de experimento (`simulator.py`, etc.) vía `POST /simulate`, y trae un borrador de `GET /simulate/stream` (SSE) todavía no conectado al progreso real. Ambos dispatchers deben llamar a la misma función `run(params)` de cada módulo de experimento — nunca duplicar la lógica de física entre los dos.

### 6.1 Archivos, Módulos y Paquetes

- Convención: **snake_case**

```
Backend/
├── main.py              # Dispatcher CLI (vigente) — una invocación por corrida, mapea nombre de experimento → módulo
├── server.py             # Dispatcher HTTP (Fase 1, en construcción) — FastAPI, mismo mapeo EXPERIMENTS → módulo
├── requirements.txt      # fastapi, uvicorn[standard] (agregado en Fase 1)
├── utils.py              # I/O compartido: write_json_atomic(), read_input_with_retries(), validate_*(), emit_progress()
├── simulator.py           # Experimento Grangier (HWP)
├── wave_simulator.py      # Experimento Wave Interference
├── input.json              # Generado en runtime por Unity (gitignored) — solo lo usa main.py
└── output.json             # Generado en runtime por Python (gitignored) — solo lo usa main.py
```

`Backend/tests/` todavía no existe — ver nota de pendientes en la sección 2.

### 6.2 Variables, Funciones y Parámetros

- Convención: **snake_case**
- Formato **obligatorio** también para claves de DTOs/JSON que viajan por red.

```python
num_angles = 45
detector_mode = "aperture"
wavelength_nm = 632.8

def validate_params(params: dict) -> bool:
    pass

def calculate_intensity(amplitude: float, phase: float) -> float:
    pass
```

### 6.3 Clases

- Convención: **PascalCase**

```python
class SimulationSession:
    def __init__(self, session_id: str):
        self.session_id = session_id
        self.parameters = {}

class OpticalElement:
    def __init__(self, element_type: str):
        self.element_type = element_type
```

### 6.4 Constantes Globales

- Convención: **UPPER_SNAKE_CASE**

```python
INPUT_PATH = "/data/experiments"
MAX_SIMULATIONS_PER_SESSION = 1000
DEFAULT_WAVELENGTH_NM = 632.8
EXPERIMENTS_SUPPORTED = ["double_slit", "single_slit", "diffraction"]
```

### 6.5 Documentación Inline (Docstrings)

- Usar docstrings estilo Google para funciones públicas.

```python
def calculate_interference_pattern(
    wavelength_nm: float,
    slit_width_um: float,
    distance_m: float
) -> dict:
    """
    Calcula el patrón de interferencia para un experimento de doble rendija.

    Args:
        wavelength_nm: Longitud de onda en nanómetros.
        slit_width_um: Ancho de la rendija en micrómetros.
        distance_m: Distancia al detector en metros.

    Returns:
        dict: Contiene 'intensity_profile' (lista) e 'intensity_max' (float).

    Raises:
        ValueError: Si los parámetros están fuera de rango.
    """
    pass
```

### 6.6 DTOs y Esquemas JSON

- Convención: **snake_case** para todas las claves — aplica a `input.json`, `output.json`, a cada línea de progreso emitida por `stdout`, y al `dict` que finalmente recibe `module.run()` sin importar por cuál dispatcher haya entrado.
- **`main.py`** sigue sin usar Pydantic: la validación de lo que viene de `input.json` es manual, con funciones que devuelven `(es_valido, mensaje_error, valor_convertido)` en vez de lanzar excepciones (ver `utils.py`, funciones `validate_integer()`/`validate_float_range()`). Este patrón se mantiene ahí sin cambios.
- **`server.py`** (Fase 1) sí usa **Pydantic** (`BaseModel`) para el body de `POST /simulate` — es la puerta de entrada nueva y la razón por la que se agregó `fastapi`/`pydantic` a `requirements.txt`. El modelo Pydantic solo valida forma/tipo del request; una vez convertido a `dict` (con `.model_dump()` en Pydantic v2 o `.dict()` en v1 — no hay versión fijada todavía, así que el código de `server.py` soporta ambas), se le sigue pasando exactamente al mismo `module.run(params)` que usa `main.py`, y las validaciones de rango de `utils.py` (`validate_integer`/`validate_float_range`) se siguen ejecutando ahí adentro sin duplicarse en el modelo Pydantic. No hay una intención de reemplazar `utils.validate_*` por validadores de Pydantic en esta fase — sería trabajo redundante mientras `main.py` siga vigente con el mismo contrato.

```python
# Backend/utils.py — patrón real de validación manual
def validate_integer(value, name: str, minimum=None, maximum=None):
    """Devuelve (es_valido, mensaje_error, valor) en vez de lanzar excepciones."""
    ...

def validate_float_range(value, name: str, minimum=0.0, maximum=1.0):
    ...

# Backend/utils.py — sobres de respuesta estándar
def build_ok_response(experiment: str, results: dict, meta: dict = None) -> dict:
    return {"status": "ok", "experiment": experiment, "results": results, "meta": meta or {}}

def build_error_response(message: str, details=None, experiment: str = None) -> dict:
    return {"status": "error", "experiment": experiment, "message": message,
            "details": details or {}, "timestamp": "..."}
```

```json
// Backend/output.json — contrato real de salida del experimento Grangier (ver 02_Backend_Python.md §4.3)
{
    "status": "ok",
    "experiment": "grangier_hwp",
    "results": {
        "hwp_sweep": [
            {
                "angle_deg": 180,
                "two_detectors": { "runs": [ { "coincidences": 42, "g2_calculated": 0.1234, "insufficient_statistics": false } ] },
                "three_detectors": { "runs": [ { "coincidences": 12, "g2_calculated": 0.0891, "insufficient_statistics": false } ] }
            }
        ]
    },
    "meta": { "execution_time_s": 3.21, "seed": 12345, "config": {}, "csv_generated": true }
}
```

### 6.7 Variables de Entorno y Secretos

- Ninguna clave, ruta absoluta local ni credencial se hardcodea en el código.
- Usar un archivo `.env` (no versionado, ver 8.5) leído con `python-dotenv` o `pydantic-settings`.

```python
# Backend/.env  (NUNCA se commitea — ver .gitignore)
API_KEY=xxxxx
INPUT_PATH=/data/experiments

# Backend/config.py
from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    api_key: str
    input_path: str

    class Config:
        env_file = ".env"
```

---

## 7. Comunicación Frontend–Backend (subprocess + stdout + archivos → red, en migración)

**Vigente hoy en producción**: Unity no abre una conexión de red hacia Python. La comunicación completa ocurre en tres canales, todos locales al mismo host: el archivo `input.json` (Unity → Python, escrito antes de lanzar el proceso), el `stdout` del subproceso (Python → Unity, streaming línea por línea mientras corre) y el archivo `output.json` (Python → Unity, leído una vez el proceso termina). Ver el diagrama completo y el ciclo de vida paso a paso en `Docs/00_Overview_Arquitectura.md` §2. Las subsecciones 7.1–7.3 describen este canal vigente y siguen aplicando sin cambios.

**En construcción (Fase 1 del Plan Maestro)**: ver 7.4 para el canal de red que lo va a reemplazar — hoy limitado a un endpoint `POST` funcional y un borrador de SSE, todavía sin cliente Unity que lo consuma.

### 7.1 Progreso en vivo por `stdout`

- Convención: **snake_case** para todas las claves, igual que en `input.json`/`output.json`.
- Cada línea impresa por Python es un objeto JSON completo, con un campo `type` que actúa de discriminador (Unity descarta cualquier línea cuyo `type` no sea `"progress"` — ver `SimulationControllerVR.ProcessProgressLine()`).
- Del lado de Python, toda emisión de progreso pasa por una única función (`utils.emit_progress()`), nunca por un `print()` suelto en otro módulo — así se garantiza que todas las líneas de progreso tengan la forma esperada y el `flush=True` que evita que se queden bufferizadas.

```python
# Backend/utils.py
def emit_progress(payload: dict) -> None:
    print(json.dumps(payload, ensure_ascii=False), flush=True)

# Backend/simulator.py — una línea de progreso real del experimento Grangier
utils.emit_progress({
    "type": "progress",
    "experiment": "grangier_hwp",
    "num_angles": 45,
    "total_runs": 10,
    "angle_deg": 22.5,
    "detector_mode": 3,
    "num_test": 4,
    "witness_count": 501,
    "transmitted_count": 248,
    "reflected_count": 253,
    "triple_coincidence_count": 12,
    "g2": 0.0891,
    "insufficient_statistics": False,
    "status": "running"
})
```

```csharp
// Frontend (C#) — manejado en SimulationControllerVR.cs
private void ProcessProgressLine(string line)
{
    ProgressLineWire wire = JsonUtility.FromJson<ProgressLineWire>(line);
    if (wire != null && wire.type == "progress")
        OnProgressReceived?.Invoke(ProgressLine.FromWire(wire));
}
```

### 7.2 Contrato de archivos (`input.json` / `output.json`)

- `input.json`: escrito por Unity antes de lanzar el subproceso, con la forma `{ "configuration": { "parameters": { ... } } }` (ver `02_Backend_Python.md` §6). `utils.extract_parameters()` asume exactamente esa forma anidada — si cambia, debe cambiar en ambos lados a la vez.
- `output.json`: escrito por Python al terminar, siempre con `write_json_atomic()` (`tempfile.mkstemp` + `os.fsync` + `os.replace`) — nunca con una escritura directa de archivo, para que Unity no pueda leer un archivo a medio escribir si el polling coincide con el instante de guardado.
- Ambos archivos viven en `Backend/` y están en `.gitignore` — son estado de ejecución local, no versionado.

### 7.3 Manejo de Errores

No hay un evento de error de protocolo (no hay protocolo de red); hay dos señales, y ambas deben revisarse:

1. **Código de salida del proceso**: `main.py` retorna `0` si `result.get("status") == "ok"`, `1` en cualquier otro caso (argumento faltante, experimento desconocido, `input.json` illegible, o excepción no capturada del módulo del experimento — ver `main.py`). Unity lee `process.ExitCode` al terminar el subproceso.
2. **`output.json` con `status: "error"`**: incluso en el camino de error, Python siempre escribe un `output.json` válido, con el sobre que construye `utils.build_error_response()`:

```json
{
    "status": "error",
    "experiment": "grangier_hwp",
    "message": "num_pulses debe ser >= 1 (recibido -5).",
    "details": {},
    "timestamp": "2026-08-26T10:15:00.000000"
}
```

- Del lado de Unity, `SimulationControllerVR` también captura el `stderr` del proceso por separado; si algo llegó por ahí, o si el código de salida no fue `0`, dispara `OnSimulationError` (`UnityEvent<string>`) con ese mensaje — es la señal que debe escucharse para mostrar el error al usuario, no un evento `"error"` de WebSocket.
- `SimulationUIController.HandleError()` es quien hoy consume esa señal para la UI. Cualquier experimento nuevo que agregue sus propios códigos de error debería documentarlos en `Docs/architecture/error_codes.md` si la lista crece lo suficiente para justificar un catálogo separado.

### 7.4 Canal de red — FastAPI + SSE (Fase 1, en construcción) → WebSocket (Fase 2, futuro)

- **`POST /simulate`** (`Backend/server.py`): recibe `{ "experiment": "...", "parameters": { ... } }` validado por Pydantic, ejecuta `module.run(params)` — el mismo `run()` que usa `main.py` — y devuelve el mismo sobre `status: "ok"/"error"` de siempre (`utils.build_ok_response()`/`build_error_response()`), ahora como respuesta HTTP en vez de `output.json`. Es funcional hoy; lo que falta es que Unity lo consuma (Fase 1.4, vía `UnityWebRequest`, desde el lugar marcado con `TODO` en `SceneController.cs`).
- **`GET /simulate/stream`** (SSE, vía `StreamingResponse` con `media_type="text/event-stream"`): **borrador, no funcional todavía**. Hoy emite un único evento de marcador de posición; conectarlo al progreso real de una corrida requiere que los módulos de experimento dejen de depender exclusivamente de `utils.emit_progress()` (que imprime a `stdout`, un supuesto que solo tiene sentido bajo el modelo de subproceso de `main.py`) y puedan alimentar en su lugar una cola/callback consumible por un generador async. Este trabajo queda para la Semana 2 del Plan Maestro — no completar este endpoint antes de esa fase salvo que el equipo decida adelantarlo explícitamente.
- **Fase 2 (futuro, no iniciado)**: SSE es explícitamente un peldaño intermedio, no el destino final — el Plan Maestro define WebSockets (con una futura `SimulationSession` del lado del servidor y `NativeWebSocket` del lado de Unity) como el transporte definitivo para progreso bidireccional en tiempo real. No invertir en hacer que el SSE de 7.4 sea perfecto si WebSocket lo va a reemplazar pronto — el objetivo de la Fase 1 es solo destrabar la ruta de red, no optimizarla.

---

## 8. Control de Versiones (Git y GitHub)

El trabajo simultáneo entre Frontend, Backend y Diseño 3D requiere reglas estrictas para evitar sobrescritura de archivos y pérdida de referencias.

### 8.1 Archivos `.meta` (CRÍTICO)

- **Nunca** hacer commit de un asset de Unity sin su archivo `.meta` correspondiente.
- Unity genera un `.meta` por cada asset (script, modelo, textura, carpeta) para asignarle un **GUID único e irremplazable**.
- Si un `.meta` se pierde o se regenera fuera de sincronía, los materiales y referencias en la escena se rompen.

```
Assets/
├── Models/
│   ├── SM_OpticalTable.fbx
│   └── SM_OpticalTable.fbx.meta  ← DEBE versionarse junto
├── Materials/
│   ├── M_DarkMetal.mat
│   └── M_DarkMetal.mat.meta      ← DEBE versionarse junto
```

**En `.gitignore` NUNCA agregar `*.meta`.**

### 8.2 Archivos Fuente de Diseño

- Archivos nativos (`.blend`, `.psd`, `.ai`) **no se guardan dentro de `Assets/`**.
- Viven en `Source_Models/` o `Source_Design/` en la raíz del repo (ver sección 2).
- Si son demasiado pesados para Git estándar, evaluar Git LFS (ver 8.6).

### 8.3 Nomenclatura de Ramas

Prefijo descriptivo + nombre en **kebab-case** (guiones, no guion bajo):

| Tipo | Patrón | Ejemplo |
|---|---|---|
| Nueva funcionalidad | `feature/nombre-tarea` | `feature/websocket-backend` |
| Corrección de bugs | `fix/nombre-error` | `fix/broken-textures` |
| Hotfix (producción) | `hotfix/nombre-error` | `hotfix/crash-on-export` |
| Release | `release/version` | `release/v1.0.0` |
| Documentación | `docs/nombre-doc` | `docs/optical-physics-guide` |
| Refactorización | `refactor/nombre` | `refactor/websocket-handler` |
| Limpieza/mantenimiento | `chore/nombre` | `chore/remove-desktop-ini` |

### 8.4 Commits y Mensajes

- Mensajes descriptivos en inglés, formato `[tipo] Descripción breve`.

```
[feature] Add WebSocket event handler for simulation progress
[fix] Correct texture mapping for optical table material
[refactor] Simplify JSON deserialization in communication layer
[docs] Update optical physics calculation guide
[chore] Remove tracked desktop.ini files from repository
```

### 8.5 Configuración de `.gitignore`

```gitignore
# ============ Unity ============
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
[Mm]emoryCaptures/
*.userprefs
*.pidb
*.suo
*.user
*.hbcache
crashlytics-build.properties

# Archivos de proyecto de IDE generados por Unity (se regeneran solos)
*.csproj
*.unityproj
*.sln
*.vsconfig
.vs/
.vscode/
.idea/
*.sublime-project
*.sublime-workspace

# ============ Python (Backend) ============
__pycache__/
*.pyc
*.pyo
*.pyd
.Python
Backend/env/
Backend/venv/
Backend/.venv/
*.egg-info/
dist/
build/
.pytest_cache/
.coverage
htmlcov/

# Secretos y config local — NUNCA versionar
.env
Backend/.env
local_config.json

# ============ Blender ============
*.blend~
*.blend1
*.blend2

# ============ Sistema Operativo (basura de Windows/macOS) ============
desktop.ini
.DS_Store
Thumbs.db
*.tmp
*~
*.swp
*.swo

# ============ Otros ============
DerivedData/
.gradle/
```

**Nota**: si el repositorio ya tiene `desktop.ini`, `.vscode/`, `.sln` duplicados u otros archivos de esta lista **versionados desde antes**, agregar la regla a `.gitignore` no los elimina automáticamente del historial — deben removerse explícitamente con `git rm --cached` (ver sección 12, Migración de Código Legacy).

### 8.6 Archivos Grandes (Git LFS)

- Si los modelos `.fbx`, texturas de alta resolución o archivos `.blend` superan ~50 MB de forma recurrente, configurar **Git LFS** para esos tipos de archivo en lugar de versionarlos directamente.

```bash
git lfs install
git lfs track "*.fbx"
git lfs track "*.blend"
git lfs track "*.psd"
```

### 8.7 Pull Requests

- Todo merge a `main` pasa por Pull Request, nunca push directo.
- Título del PR en inglés, formato igual al de commits: `[feature] Add WebSocket event handler`.
- Al menos un reviewer de un área distinta a la del autor cuando el cambio toque la integración Frontend↔Backend.
- El PR debe pasar el checklist de la sección 13 antes de aprobarse.

---

## 9. Testing y Validación

### 9.1 Tests en C# (Unity)

- Carpeta `Assets/Tests/`, con estructura espejo de `Assets/Scripts/`.
- Naming: `[Claseprobada]Tests.cs`.

```csharp
// Assets/Tests/Controllers/SceneControllerTests.cs
using NUnit.Framework;

public class SceneControllerTests
{
    [Test]
    public void ProcessProgressLine_WithValidInput_ReturnsTrue()
    {
        var controller = new SceneController();
        string validLine = "angle:45.0 intensity:0.95";
        bool result = controller.ProcessProgressLine(validLine);
        Assert.IsTrue(result);
    }
}
```

### 9.2 Tests en Python (Backend)

- Carpeta `Backend/tests/`, usando `pytest`.
- Naming: `test_[nombre_modulo].py`.

```python
# Backend/tests/test_wave_simulator.py
from wave_simulator import calculate_interference_pattern

def test_calculate_interference_pattern_with_valid_params():
    result = calculate_interference_pattern(
        wavelength_nm=632.8,
        slit_width_um=10.0,
        distance_m=1.0
    )
    assert "intensity_profile" in result
    assert "intensity_max" in result
    assert result["intensity_max"] > 0
```

### 9.3 Cobertura Mínima Sugerida

- Lógica de física óptica (cálculos matemáticos puros en Python): cobertura alta, ya que son funciones deterministas fáciles de testear.
- Lógica de UI e interacción VR en Unity: priorizar tests manuales en dispositivo + tests automatizados solo en lógica desacoplada de `MonoBehaviour` cuando sea posible.

---

## 10. Versionamiento Semántico

Todo release sigue **Semantic Versioning 2.0.0** (`MAJOR.MINOR.PATCH`):

- **MAJOR**: cambios incompatibles (breaking changes) en el protocolo WebSocket o formato de datos.
- **MINOR**: nueva funcionalidad compatible con versiones anteriores.
- **PATCH**: correcciones de bugs sin cambios de comportamiento esperado.

```bash
git tag -a v1.2.3 -m "Release version 1.2.3"
git push origin v1.2.3
```

---

## 11. Localización (Textos de UI)

Si el simulador se distribuye en más de un idioma (ej. español e inglés para usuarios finales, manteniendo el código siempre en inglés):

- Los textos visibles al usuario viven en `Assets/Localization/`, como archivos JSON o tablas de Unity Localization Package.
- Claves en `UPPER_SNAKE_CASE`, nunca el texto final hardcodeado en un script.

```json
// Assets/Localization/es.json
{
    "UI_START_SIMULATION": "Iniciar simulación",
    "UI_SELECT_WAVELENGTH": "Seleccionar longitud de onda"
}
```

```csharp
// Correcto
uiText.text = LocalizationManager.Get("UI_START_SIMULATION");
// Incorrecto — texto hardcodeado en el script
uiText.text = "Iniciar simulación";
```

---

## 12. Migración de Código Legacy

El proyecto ya tiene código escrito antes de esta guía (scripts en español, archivos sueltos en la raíz, `desktop.ini` versionados, etc.). No se corrige todo de un solo commit — se sigue este plan por fases para no romper referencias de escena:

### Fase 1 — Limpieza de repositorio (sin tocar código)

1. Agregar las reglas nuevas al `.gitignore` (sección 8.5).
2. Remover del control de versiones lo que ya no debería estar tracked, sin borrar el archivo local si Unity lo necesita:
   ```bash
   git rm --cached -r **/desktop.ini
   git rm --cached .vscode/ -r
   git rm --cached "Exp 1.sln"
   git commit -m "[chore] Remove tracked OS/IDE artifacts and duplicate solution file"
   ```
3. Mover `simulador_optica.py`, `input.json`, `output.json` de la raíz a `Backend/`, actualizando cualquier ruta relativa que los referencie.

### Fase 2 — Traducción de scripts C# existentes

Para cada script en español (`CamaraLibre.cs`, `DetectorFotones.cs`, `Espejo.cs`, `FiltroNeutro.cs`, `IComponenteOptico.cs`, `Laser.cs`, `Polarizador.cs`):

1. Renombrar la clase y el archivo al equivalente en inglés (`CamaraLibre` → `FreeCamera`, `Espejo` → `Mirror`, `Laser` → `LaserEmitter`, etc.) usando el **rename refactor del IDE** (Rider/Visual Studio), nunca renombrando el archivo a mano — así se actualizan referencias automáticamente.
2. **Importante**: en Unity, renombrar la clase de un script existente rompe el vínculo con componentes ya asignados en escenas y prefabs si cambia el GUID. Como el `.meta` conserva el GUID mientras el archivo `.cs` se renombre (no se borre y recree), el proceso correcto es:
   - Renombrar el archivo `.cs` y la clase dentro de él.
   - Verificar en Unity que el `.meta` correspondiente se actualizó automáticamente (mismo GUID, nuevo nombre) y no se generó un `.meta` nuevo.
   - Abrir las escenas/prefabs afectados y confirmar que el componente sigue asignado correctamente antes de hacer commit.
3. Traducir variables públicas y privadas dentro del mismo commit que la clase, para no dejar el archivo en un estado mixto.
4. Corregir el encoding del archivo al guardar (ver 1.3) — esto resuelve el mojibake existente en `Polarizador.cs`.

Priorizar esta migración por rama `refactor/translate-scripts-to-english`, un PR por lote de 2–3 scripts relacionados, no todo de una vez.

### Fase 3 — Verificación

- Abrir el proyecto en Unity y confirmar que la escena `SampleScene.unity` carga sin errores de referencias faltantes.
- Correr los tests existentes (si los hay) tras cada lote de renombrado.

---

## 13. Checklist de Integración

Antes de abrir un Pull Request hacia `main`:

- [ ] Todo código nuevo sigue las convenciones de naming de esta guía (inglés, casing correcto).
- [ ] Archivos `.meta` incluidos en el commit para cada asset nuevo o modificado.
- [ ] Ningún archivo de texto tiene caracteres corruptos (mojibake) — guardado en UTF-8.
- [ ] Tests pasan (`dotnet test` o Test Runner de Unity para C#; `pytest` para Python).
- [ ] Documentación XML/docstrings presente en métodos y clases públicas nuevas.
- [ ] Rama nombrada correctamente (`feature/`, `fix/`, `chore/`, etc., en kebab-case).
- [ ] Mensajes de commit descriptivos en inglés, formato `[tipo] Descripción`.
- [ ] No hay archivos sueltos fuera de su carpeta correspondiente (`Assets/`, `Backend/`, `Source_Models/`, etc.).
- [ ] `.gitignore` respetado — sin `Library/`, `__pycache__/`, `desktop.ini`, `.env`, etc. en el diff.
- [ ] Ningún secreto, API key o ruta absoluta local hardcodeada en el código.
- [ ] Si se modificó un modelo 3D: escala 1:1, pivote en la base, orientación correcta verificada en Unity.

---

## Referencias y Recursos

- [C# Coding Conventions (Microsoft)](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [PEP 8 – Python Enhancement Proposal](https://www.python.org/dev/peps/pep-0008/)
- [Semantic Versioning 2.0.0](https://semver.org/)
- [Unity Best Practices](https://docs.unity3d.com/Manual/BestPracticeGuides.html)
- [Python `subprocess` — ejecución de procesos externos](https://docs.python.org/3/library/subprocess.html)
- [.NET `Process` class (usada por Unity para lanzar el subproceso Python)](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process)
- [Git LFS](https://git-lfs.com/)
- [Blender FBX Export Documentation](https://docs.blender.org/manual/en/latest/addons/import_export/scene_fbx.html)
- [FastAPI Documentation](https://fastapi.tiangolo.com/)
- [MDN — Server-Sent Events (SSE)](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events)
- [Unity `UnityWebRequest`](https://docs.unity3d.com/ScriptReference/Networking.UnityWebRequest.html)
- [WebSocket Protocol (RFC 6455)](https://datatracker.ietf.org/doc/html/rfc6455)
- [NativeWebSocket (Unity, cliente WebSocket para Fase 2)](https://github.com/endel/NativeWebSocket)
- [Pydantic Documentation](https://docs.pydantic.dev/)

---

**Versión del documento**: 2.2.0
**Última actualización**: 2026-08-26
**Responsable**: Luis Moto

**Historial de cambios**:
- v2.2.0 — Arranca la Semana 1/Fase 1 del Plan Maestro de migración (10 semanas: subproceso local → arquitectura de red centralizada). FastAPI+SSE (Fase 1) y WebSockets (Fase 2) dejan de estar "removidos por ficticios" (v2.1.0) y pasan a documentarse como el objetivo de arquitectura activo, coexistiendo con el patrón de subproceso vigente durante la transición (secciones 2, 6, 7.4 actualizadas; 7.1–7.3 sin cambios, siguen describiendo el canal vigente). Se restauran en "Referencias y Recursos" los enlaces a FastAPI/SSE/WebSocket removidos en v2.1.0, más `Pydantic` y `NativeWebSocket`. Se documenta el Singleton `SceneController` como estándar de arquitectura C# (nueva sección 5.11) — punto único de navegación/estado de escena y futuro alojamiento del cliente de red. Se actualiza la estructura de `Assets/Scripts/` a organización por capas estricta (se abandona la organización por dominio de v2.1.0/`03_Cumplimiento_y_Brechas.md` §4): se agrega `Networking/` junto a `Controllers/`, `Managers/`, `Models/`, `XR/` ya existentes. Se documenta `Backend/server.py` (nuevo) y `Backend/requirements.txt` (nuevo, ya no pendiente).
- v2.1.0 — Corregidas las secciones 2, 6 y 7: describían un Backend con servidor FastAPI, WebSocket y esquemas Pydantic que nunca se implementó así. Se reemplazaron por la arquitectura real y vigente (Unity lanza `Backend/main.py` como subproceso CLI, progreso en vivo por streaming de `stdout` línea por línea, resultado final en `output.json` escrito atómicamente) — ver `03_Cumplimiento_y_Brechas.md` hallazgo #3. Se documentó además como pendiente la ausencia de `requirements.txt` y `Backend/tests/`.
- v2.0.0 — Se agregó estructura raíz del monorepo, ubicación del Backend, estándar de encoding UTF-8, excepción para paquetes de terceros, nomenclatura de assets adicionales (shaders, ScriptableObjects, animación, audio, escenas, interacción XR), sección de localización, plan de migración de código legacy, y `.gitignore` ampliado (desktop.ini, archivos de IDE, secretos).
- v1.0.0 — Versión inicial: nomenclatura de assets 3D, C#, Python, Git básico.
