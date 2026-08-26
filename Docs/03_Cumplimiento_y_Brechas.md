# 03. Cumplimiento y Brechas Reales — virtual-lab-v1

**Fecha de auditoría**: 2026-08-26 · **Rama**: `MVP_escenas` · **Frente a**: `Docs/ESTANDARES_DOCUMENTACION_TECNICA.md` v2.0.0 (preexistente a esta auditoría; actualizada a **v2.1.0** como parte de los fixes de este documento — ver §3)

**Nota de esta revisión**: los hallazgos §1, §2, §3 y §5 se corrigieron directamente en el código/escenas/documentación; §4 se corrigió parcialmente (la parte de idioma); §6 se corrigió porque parte del diagnóstico original era incorrecto (sí existen XR Interactables en uso). §7 se deja explícitamente **sin tocar** — es una decisión de re-adición de assets que el usuario confirmó como intencional, no un bug a revertir. Cada sección abajo indica su estado actual.

Este documento junta todo lo encontrado al revisar el código y las escenas reales de `virtual-lab-v1` contra la Guía de Estándares que el equipo ya tenía escrita. Se ordena por severidad: primero el hallazgo funcional crítico, luego inconsistencias de flujo, luego brechas de la Guía frente al código real, y al final las buenas prácticas confirmadas que vale la pena preservar.

---

## 1. ✅ RESUELTO — Hallazgo crítico: los paneles flotantes de progreso nunca se actualizaban en vivo

**Dónde**: `Assets/Scripts/VR/GrangierDataReader.cs`, método `ShowLiveProgress(ProgressLine p)`.

**Qué pasaba**: este método existe, está completo, y fue escrito específicamente para actualizar dos paneles flotantes en el mundo (`coincidencesPanel`, `g2Panel`) cada vez que llega una línea de progreso desde Python. **Nunca se conectaba a nada.** Se verificó con grep de `m_MethodName:` en las 3 escenas: solo aparecían `HandleCompletion`, `HandleError`, `HandleProgress` y `RunGrangierSimulation`/`RunExperiment` — `ShowLiveProgress` no aparecía ni una vez.

**Efecto observable**: `UpdateFloatingPanels()`, que sí corre, solo lo hacía **una vez, en `Start()`**, leyendo el `output.json` que quedó de la corrida *anterior*. Cuando el usuario disparaba una corrida nueva desde VR, el canvas flotante de `SimulationUIController` sí se actualizaba correctamente en tiempo real — pero los paneles del `GrangierDataReader` se quedaban congelados con el dato viejo hasta el próximo `Start()` de la escena.

### Fix aplicado

En vez de agregar una entrada más al `UnityEvent OnProgressReceived` desde el Inspector (el fix originalmente sugerido, puramente de wiring), se optó por una suscripción **por código**, en `GrangierDataReader.Start()`:

```csharp
_controller = GetComponent<SimulationControllerVR>();
if (_controller != null)
    _controller.OnProgressReceived.AddListener(ShowLiveProgress);
else
    Debug.LogWarning("[GrangierDataReader] No SimulationControllerVR found on this GameObject — live panel updates are disabled.");
```

con el `RemoveListener` simétrico en `OnDestroy()`. Se eligió este camino por tres razones: (1) `SimulationControllerVR`, `GrangierDataReader` y `SimulationUIController` viven en el **mismo GameObject** en `Scene_DosDet.unity` (fileID `1444616075`), así que `GetComponent<T>()` resuelve la referencia sin arrastrar nada en el Inspector; (2) es auto-reparable ante futuros refactors de escena, en vez de depender de que alguien recuerde repetir el wiring manual; (3) es más fácil de revisar en un diff de código que un cambio silencioso en YAML de escena. El wiring por Inspector seguía siendo una alternativa válida — solo se priorizó la opción más robusta.

### Hallazgo adicional descubierto al aplicar este fix (seguimiento pendiente, no resuelto)

Al instrumentar el fix se encontró una causa raíz **más profunda**: en `Scene_DosDet.unity`, los campos `coincidencesPanel`/`g2Panel` del componente `GrangierDataReader` (fileID `1444616077`) están **ambos sin asignar** (`panelCoincidencias: {fileID: 0}`, `panelG2: {fileID: 0}` en el YAML). No existe en la escena ningún objeto de texto world-space (TextMeshProUGUI) que pudiera asignarse a esos campos — los paneles flotantes de `GrangierDataReader` no solo no se actualizaban en vivo: **hoy no tienen ninguna UI real que actualizar**, ni siquiera para la actualización única de `Start()`.

No se creó UI nueva para resolver esto — construir esos dos paneles en el espacio 3D (posición, escala, estilo dentro del laboratorio VR) es una decisión de diseño/arte que le corresponde a quien esté a cargo de esa escena, no algo para improvisar dentro de un fix de wiring. En su lugar, `GrangierDataReader.cs` ahora falla de forma segura: un helper `PanelsReady()` valida ambas referencias antes de tocar `.text`, y si faltan, emite **un solo** `Debug.LogWarning` (no spam por frame, no `NullReferenceException`) indicando exactamente qué asignar.

**Pendiente para el equipo**: crear (o decidir explícitamente que no se necesitan) los dos objetos `TextMeshProUGUI` en world-space dentro de `Scene_DosDet.unity` y asignarlos en el Inspector del componente `GrangierDataReader`. Hasta entonces, el warning en consola es la señal de que falta ese paso — ya no es un fallo silencioso.

---

## 2. ✅ RESUELTO — Inconsistencia de flujo entre `Scene_DosDet` y `Scene_TresDet`

- `Scene_TresDet.unity` conecta su botón de "correr experimento" a `SimulationUIController.RunExperiment()` — el flujo correcto, que resetea el estado y muestra `loadingView` antes de lanzar la simulación.
- `Scene_DosDet.unity` conectaba el mismo botón directamente a `SimulationControllerVR.RunGrangierSimulation()`, **saltándose** ese reseteo/vista de carga.

**Corrección de precisión sobre el hallazgo original**: el "botón" en ambas escenas no es un `Button` de Canvas (`Button.OnClick`), sino un **XR Interactable** (Interaction Toolkit) sobre un GameObject llamado "Boton" — el evento real es `m_SelectEntered`, disparado por el select del controlador VR. No cambia el diagnóstico (la inconsistencia era real), pero corrige el mecanismo exacto — ver también la corrección al hallazgo §6, que depende de esta misma observación.

**Efecto que tenía**: si un usuario corría el experimento dos veces seguidas en `Scene_DosDet` sin recargar la escena, la UI no mostraba el estado de "cargando" ni limpiaba el resultado anterior antes de mostrar el nuevo.

**Fix aplicado**: se cambió el `m_SelectEntered` del GameObject "Boton" en `Scene_DosDet.unity` para que apunte a `SimulationUIController.RunExperiment()` (fileID `1444616079`), igual que en `Scene_TresDet.unity` — antes apuntaba a `SimulationControllerVR.RunGrangierSimulation()` (fileID `1444616076`). Cambio de wiring puro en el YAML de la escena, sin tocar código. Verificado por grep que ya no queda ninguna referencia a `RunGrangierSimulation` en el `m_PersistentCalls` de esa escena.

---

## 3. ✅ RESUELTO — La Guía de Estándares describía una arquitectura de Backend que no era la real

Las secciones 2, 6 y 7 de `ESTANDARES_DOCUMENTACION_TECNICA.md` describían el Backend como un servicio **FastAPI + WebSocket + Pydantic** (`server.py`, `optical_physics.py`, carpetas `models/`/`utils/`/`tests/`, eventos `request_simulation_start`/`response_progress_update` sobre WebSocket, esquemas `SimulationRequest` con Pydantic) — nada de eso existe en el código real (ver `02_Backend_Python.md`).

**Fix aplicado**: se tomó la opción (a) de la sugerencia original — se reescribieron las tres secciones para documentar el patrón real y vigente (subproceso CLI `main.py` + streaming de progreso por líneas JSON en `stdout` + `input.json`/`output.json` con escritura atómica), dejando explícito que si el equipo migra a un servicio persistente en el futuro, la Guía debe actualizarse *antes* de ese cambio, no después. Se aprovechó el mismo pase para: reemplazar el ejemplo de validación con Pydantic por el patrón real de `utils.py` (funciones que devuelven `(válido, error, valor)` en vez de excepciones), reemplazar los ejemplos de eventos WebSocket por el contrato real de `stdout`/`input.json`/`output.json` con nombres de campo reales (`type`, `angle_deg`, `g2`, `triple_coincidence_count`, etc.), documentar el manejo de errores real (código de salida del proceso + `output.json` con `status: "error"` + `stderr` capturado por Unity vía `OnSimulationError`), y anotar como pendiente explícito la ausencia de `requirements.txt` y `Backend/tests/`. Versión del documento actualizada de 2.0.0 a **2.1.0**, con su propia entrada en el historial de cambios al final de ese archivo.

---

## 4. 🟡 PARCIALMENTE RESUELTO — Estructura de `Assets/Scripts/`: por dominio, no por capa — y con carpetas en español

La Guía (sección 3) prescribe subcarpetas por **capa arquitectónica** dentro de `Scripts/`: `Controllers/`, `Managers/`, `Interfaces/`, `Models/`, `Utils/`, `WebSocket/`, `XR/`, `Editor/`.

El código real se organiza por **dominio**: `Dialogue/`, `Optics/`, `VR/`, `Utils/`. Es una decisión de organización razonable para el tamaño actual del proyecto (y quizás más legible que la de la Guía, dado que agrupa lo que cambia junto), pero sigue siendo una desviación real frente a la sección 3.

**Fix aplicado (la parte de idioma, de bajo riesgo)**: se renombraron `Dialogos/`→`Dialogue/` y `Optica/`→`Optics/` con `git mv` (preserva el historial de cada archivo; los `.meta` viajan con la carpeta, así que los GUID no cambian y ninguna referencia de escena/prefab se rompe). Esto cierra la brecha de idioma de la regla 1.1.

**Pendiente, requiere decisión de equipo (no se resolvió unilateralmente en este fix)**: si el equipo prefiere formalizar la organización por dominio en vez de por capa, la sección 3 de la Guía debería actualizarse para reflejarlo explícitamente — hoy sigue prescribiendo `Controllers/`/`Managers/`/etc., que no es cómo está organizado el código real. No se tocó la sección 3 en este pase porque es una decisión de arquitectura de equipo, no un bug de wiring o una brecha de idioma mecánica.

---

## 5. ✅ RESUELTO — Plugin de terceros (`VolumetricLines`) integrado dentro de `Scripts/Utils/`

La Guía (sección 1.4) es explícita: contenido de terceros no se reestructura ni se mezcla con código propio; si necesita modificarse, la copia modificada va a `Assets/ThirdPartyOverrides/`. Los 3 scripts de `VolumetricLines` (más sus 4 ejemplos de demo) estaban reubicados dentro de `Assets/Scripts/Utils/`, junto a utilidades propias del proyecto — exactamente la mezcla que la regla busca evitar.

**Fix aplicado**: los 7 archivos (+ sus `.meta`) se movieron a `Assets/ThirdPartyOverrides/VolumetricLines/` (con `Examples/` como subcarpeta, misma estructura que trae el plugin originalmente). Los GUID de los 3 scripts principales se preservaron sin cambio (`VolumetricLineBehavior`=`0884dceb0d4308c4eb8bc763c8f8deae`, `VolumetricLineStripBehavior`=`c9efc33befc68374c889396a73a00a5d`, `VolumetricLineVertexData`=`21887c148ed9f8143a28d8e6c7efcbd2`). Se dejó un `.gitkeep` en `Scripts/Utils/` (quedó vacía, pero es una carpeta prescrita por la Guía) y se retiró el `.gitkeep` que ya no aplicaba en `ThirdPartyOverrides/` al dejar de estar vacía.

**Nota al margen (informativa, no una brecha nueva)**: se verificó por GUID que ninguna de las 3 escenas (`Scene_1Intro`, `Scene_DosDet`, `Scene_TresDet`) tiene un componente de `VolumetricLines` adjunto a ningún GameObject actualmente — los haces láser se renderizan hoy con `LineRenderer` estándar configurado desde `BboCrystal.cs`/`BeamSplitter.cs`, no con este plugin. La reubicación fue, por lo tanto, sin ningún riesgo de romper referencias de escena; que el plugin esté integrado pero sin uso activo es una observación aparte que el equipo puede considerar (¿se planea usar más adelante, o se puede remover del proyecto?), no algo que se necesitara resolver en este fix.

---

## 6. 🟡 CORREGIDO — XR: sí hay Interactables reales en uso; el teclado es una dependencia más acotada de lo que se pensó

Ver detalle completo (y la misma corrección) en `01_Frontend_Unity.md` §6.

**Corrección al hallazgo original**: la auditoría inicial reportó "0 coincidencias" de `XRGrabInteractable`/`XRSocketInteractor`/`XRBaseInteractable`/cualquier `m_Script` con "Interactable" en el nombre, en las 3 escenas — buscando por un mapa de GUID construido únicamente desde `Assets/Scripts/**/*.cs.meta`. Ese método no puede encontrar componentes de **paquetes** (XR Interaction Toolkit vive en `Library/PackageCache`, no en `Assets/Scripts/`), así que la búsqueda nunca podía haber encontrado un Interactable aunque existiera — el "0" medía una herramienta de búsqueda incompleta, no la ausencia real del componente. Al corregir esto (identificando el componente por la firma de sus campos serializados — `m_InteractionManager`, `m_SelectMode`, `m_FocusMode`, ausencia de campos de grab como `m_AttachTransform`/`movementType` — en vez de por GUID), se encontró que el GameObject **"Boton"**, presente tanto en `Scene_DosDet.unity` como en `Scene_TresDet.unity`, sí es un XR Interactable real, y su evento `m_SelectEntered` es justamente lo que dispara `SimulationUIController.RunExperiment()` (ver fix §2) — **correr el experimento desde el control VR ya funciona vía XRI**, no por teclado ni por un `Button.OnClick` de canvas.

**Lo que sigue siendo cierto y sigue pendiente**: `LaserSource.cs` todavía depende de `Input.GetKeyDown(KeyCode.Space)` para alternar el láser/cristal — a diferencia de `DialogueManager.cs` (ray interactor + `Button.onClick`, sin teclado) y del botón "Boton" (XRI `m_SelectEntered`, sin teclado), este script no se ha migrado. No se tocó como parte de este fix: a qué interacción VR debería responder (¿un socket?, ¿un botón físico del laboratorio?, ¿el mismo select que ya dispara la corrida?) es una decisión de diseño de interacción, no algo para resolver por inferencia dentro de un fix de wiring.

**No verificado de nuevo en este pase**: la afirmación de que la locomoción es por joystick y no por el sistema de locomoción de XRI. Solo se corrigió específicamente el conteo de Interactables, que es lo que se investigó a fondo al resolver los hallazgos §1 y §2 — el resto del hallazgo original se deja como estaba.

---

## 7. 🟡 Re-adición de "3D Laboratory Environment with Appratus"

El commit `a4f2b02` ("Elimina permanentemente paquetes verificados sin uso...") borró deliberadamente este paquete de assets por considerarlo sin uso, tras verificación. El estado pendiente actual (sin commitear) lo **vuelve a agregar** — 45 archivos nuevos bajo `Assets/3D Laboratory Environment with Appratus/`.

Puede ser perfectamente intencional (alguien decidió que sí se necesita después de todo), pero dado que contradice una decisión de limpieza explícita y ya documentada en el historial, vale la pena que quede confirmado por quien lo esté re-agregando antes de que ese commit se cierre — para que no sea una reversión accidental (por ejemplo, de restaurar una copia de trabajo vieja sobre la nueva).

---

## 8. 🟢 Detalle transitorio, no persistente: `coincidences_Nc` vs `coincidences`

Ver `02_Backend_Python.md` §4.3. El `output.json` en disco (del 2026-08-23, previo al refactor) usa `coincidences_Nc`; el código actual escribe `coincidences`. Como el archivo está en `.gitignore`, se autocorrige en la siguiente corrida y no afecta a nadie que clone el repo. Se documenta únicamente para que no sorprenda a quien inspeccione ese archivo puntual.

---

## 9. Estado del commit en curso (contexto para todo lo anterior)

Al momento de esta auditoría, `git status` reporta **~215 cambios pendientes sin commitear**: 57 altas, 97 renombres, 12 modificaciones, 9 borrados, 38 archivos nuevos sin trackear. Es, en esencia, la aplicación retroactiva de las convenciones de nomenclatura de la Guía (secciones 3 y 4) sobre todo el árbol de `Assets/`: modelos `.fbx`→`SM_*`, materiales→`M_*`, prefabs→`PF_*`, shaders→`SH_*`, texturas→`T_*_ui.png`, escenas→`Scene_*`, más la creación de las carpetas prescritas por la Guía (incluyendo varias todavía vacías, marcadas con `.gitkeep`) y la relocalización de `VolumetricLines` señalada en el punto 5. `README.md` y toda la carpeta `Docs/` (incluida la propia Guía) son también, en este momento, contenido nuevo sin trackear.

**Nota post-fix**: tras aplicar los fixes de este documento (§1 código, §2 escena, §4 parcial, §5 reubicación, más la reescritura de la Guía en §3), el conteo subió a ~221 (las líneas nuevas son el `.gitkeep` agregado en `Scripts/Utils/`, el `.gitkeep` retirado de `ThirdPartyOverrides/`, el `.meta` nuevo de `ThirdPartyOverrides/VolumetricLines/`, y las modificaciones de `GrangierDataReader.cs`, `Scene_DosDet.unity` y `ESTANDARES_DOCUMENTACION_TECNICA.md`). La recomendación de dividir en varios commits lógicos (siguiente párrafo) sigue aplicando igual; estos fixes pueden viajar como commits adicionales en la misma tanda, o fusionarse con el commit de reorganización correspondiente a cada uno.

**Sugerencia práctica**: dado el tamaño del cambio, conviene dividirlo en varios commits lógicos en vez de uno solo gigante — por ejemplo: (1) reorganización de `Assets/` según la Guía, (2) relocalización de `VolumetricLines`, (3) la re-adición de "3D Laboratory Environment with Appratus" **aislada en su propio commit**, con mensaje explícito de por qué se revierte la eliminación previa, y (4) `README.md` + `Docs/`. Aislar el punto 3 en particular facilita que cualquier reviewer note y confirme esa decisión específica, en vez de que quede enterrada dentro de un commit de miles de líneas.

---

## 10. Buenas prácticas confirmadas (para preservar, no solo brechas)

- Escritura atómica de JSON (`tempfile` + `fsync` + `os.replace`) en `utils.py`.
- Lectura con reintentos y backoff exponencial (`read_input_with_retries()`) para la carrera Unity-escribe/Python-lee.
- Topes de seguridad explícitos en el Monte Carlo (`MAX_NUM_PULSES`, `MAX_RUNS`, `MAX_ANGLES`, `MAX_TOTAL_OPERATIONS`).
- Patrón Wire/DTO (`XxxWire` snake_case + `FromWire()` estático → DTO público camelCase) consistente en los tres scripts VR — más limpio que el ejemplo de mapeo manual que trae la propia Guía en su sección 7.1; vale la pena que la Guía adopte este patrón real como el estándar recomendado.
- `[FormerlySerializedAs(...)]` aplicado sistemáticamente en el refactor español→inglés, preservando las asignaciones del Inspector — la Guía (sección 12, Fase 2) no menciona esta técnica explícitamente; conviene agregarla como paso recomendado para futuras traducciones de campos serializados.
- `.vscode/` mantenido trackeado deliberadamente (con comentario explicativo en `.gitignore`) para compartir configuración de equipo, en vez de ignorarlo a ciegas.
- Historial de uso de una carpeta `_ToDelete/` como paso intermedio antes de borrar assets de forma permanente (commit `878cd9e`) — buena práctica que podría formalizarse en la Guía.
- Los renombres masivos de la reorganización en curso (`1Intro.unity`→`Scene_1Intro.unity`, `DialogueManager.prefab`→`PF_DialogueManager.prefab`, etc.) se hicieron como renombres de git reales (`R`), no como borrar+crear — preserva el historial de cada archivo.

---

## 11. Sobre la documentación anterior (carpeta equivocada)

Antes de auditar este repositorio, se produjo un set de documentos equivalente (Guía de Estándares + 4 documentos as-built) sobre la carpeta `Exp 1 v.1`, que resultó ser un proyecto distinto y desactualizado — no `virtual-lab-v1`. Esos archivos siguen existiendo en `Exp 1 v.1\Docs\` y no se tocaron ni se borraron. Quedan como lo que son: documentación de otro proyecto. Si se quiere, se puede dejar constancia ahí mismo (una nota en ese `README`/`Docs`) de que ese trabajo no aplica a `virtual-lab-v1`, para que nadie lo confunda más adelante — pero es una decisión que le corresponde al usuario, no algo que se resuelva desde este repositorio.
