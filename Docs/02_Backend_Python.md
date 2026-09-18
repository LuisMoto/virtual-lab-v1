# 02. Backend Python — Estado Real

**Fecha de auditoría**: 2026-09-17 · **Rama**: `semana-2/integracion-red`

> Actualiza la auditoría del 2026-08-26 (rama `MVP_escenas`, antes de que existiera `server.py`). Lo de esa fecha sobre `main.py`/`utils.py`/`simulator.py`/`wave_simulator.py` en modo CLI sigue vigente — `main.py` no cambió — y se conserva abajo con las notas mínimas necesarias. Lo nuevo es todo lo de la Semana 2 del Plan Maestro, Pista A completa (A1–A4): el callback de progreso en `simulator.py`/`utils.py`, el servidor FastAPI real (`server.py`, incluyendo `GET /simulate/stream` ya no como borrador), y la Dockerización.

---

## 1. Inventario real de `Backend/`

```
Backend/
├── main.py                          (67 líneas — dispatcher CLI, sin cambios)
├── utils.py                         (133 líneas — I/O compartido, validación, progreso + cola thread-safe)
├── simulator.py                     (319 líneas — experimento Grangier/HWP + callback de progreso)
├── wave_simulator.py                (47 líneas — experimento Wave Interference, sin cambios)
├── server.py                        (236 líneas — API FastAPI: POST /simulate + GET /simulate/stream real)
├── requirements.txt                 (fastapi, uvicorn[standard])
├── Dockerfile                       (contenerización del backend, Fase 1.5)
├── .dockerignore
├── input.json                       (última entrada real, gitignored)
├── output.json                      (última salida real, gitignored)
└── resultados_grangier_hwp_*.csv / results_grangier_hwp_*.csv
    (gitignored)
```

Los dos `.stub` que documentaban el diseño de A1/A2 (`simulator_progress_callback.py.stub`, `server_sse_handler.py.stub`) ya cumplieron su función — sus ideas centrales quedaron implementadas en `simulator.py`/`utils.py`/`server.py`, con algunos ajustes respecto al diseño original (ver §5.2) — pero se dejan en el repo como referencia histórica; no los importa ni ejecuta nada.

**Housekeeping** (sin cambios desde la auditoría anterior): la carpeta sigue acumulando CSVs de corridas pasadas. Sugerencia menor sigue en pie: moverlos a `Backend/runs/` o agregar un script de limpieza — no es un problema de repositorio, es higiene de disco local.

---

## 2. `main.py` — dispatcher CLI (legacy, sigue funcionando igual)

Sin cambios respecto a la auditoría anterior. Sigue siendo el punto de entrada para `python main.py <experimento>` vía subproceso — el modelo original que Unity usaba antes de B1-B3 (Pista B) y que `server.py` no reemplaza, sino que complementa como alternativa en red.

- `EXPERIMENTS = {"grangier_hwp": simulator, "wave_interference": wave_simulator}`.
- Flujo: `utils.read_input_with_retries()` → `utils.extract_parameters()` → `module.run(params)` → `utils.write_json_atomic()`.
- Código de salida `0`/`1` según `result.get("status")`.

## 3. `utils.py` — utilidades compartidas

| Función | Propósito |
|---|---|
| `write_json_atomic()` | Sin cambios — `tempfile.mkstemp` + `os.fsync` + `os.replace`. |
| `try_write_text()` | Sin cambios. |
| `build_ok_response()` / `build_error_response()` | Sin cambios. |
| `read_input_with_retries()` | Sin cambios. |
| `extract_parameters()` | Sin cambios. |
| `validate_integer()` / `validate_float_range()` | Sin cambios. |
| `emit_progress()` | Ya **no** es "el mecanismo completo de streaming en vivo" (como decía la auditoría del 2026-08-26) — ahora es específicamente el *fallback* que usa `simulator.py::_emit_progress()` cuando nadie registró un callback (es decir, cuando corre bajo `main.py` en modo CLI/subproceso). Sigue siendo `print(json.dumps(...), flush=True)` sin cambios de implementación. |
| `create_progress_queue()` **(nuevo, A1)** | Crea un par `(queue.Queue, callback)` thread-safe. `server.py::init_progress_system()` la usa una sola vez al arrancar: el `callback` se registra en `simulator.py` vía `set_progress_callback()`, y la `queue.Queue` la consume `server.py::_progress_stream()` para alimentar `GET /simulate/stream`. `queue.Queue` (no `asyncio.Queue`) porque el productor corre en un hilo del threadpool de FastAPI, no en el hilo del event loop — `asyncio.Queue` no es thread-safe en ese escenario sin `loop.call_soon_threadsafe()` en cada `put()`. |

## 4. `simulator.py` — experimento Grangier (HWP)

### 4.1 Topes de seguridad

Sin cambios: `MAX_NUM_PULSES`, `MAX_RUNS`, `MAX_ANGLES`, `MAX_TOTAL_OPERATIONS`.

### 4.2 Progreso: callback en vez de `print()` directo **(nuevo, A1 — Semana 2)**

Antes, las 4 llamadas de progreso dentro de `simulator.py` (`_emit_run_progress()`, y los 3 puntos de `run()`: inicio/error/fin) llamaban directamente a `utils.emit_progress()` — siempre imprimían a stdout, sin importar quién estuviera corriendo el módulo.

Ahora todas pasan por una capa nueva:

```python
progress_callback = None

def set_progress_callback(fn):
    global progress_callback
    progress_callback = fn

def _emit_progress(payload):
    if progress_callback is not None:
        progress_callback(payload)
    else:
        utils.emit_progress(payload)
```

- **Modo CLI (`main.py`)**: nadie llama a `set_progress_callback()`, así que `_emit_progress()` cae exactamente al mismo `print()` de siempre — cero cambio de comportamiento para el flujo legacy.
- **Modo servidor (`server.py`)**: `init_progress_system()` (ver §5.2) registra un callback una sola vez al arrancar, que empuja cada payload a una `queue.Queue` — de ahí sale el progreso real que consume `GET /simulate/stream`.

El **contrato del payload no cambió** (mismos campos `type`/`experiment`/`angle_deg`/etc.) — la "Invarianza del Contrato de Datos" del Plan Maestro (§4) aplicada en la práctica: cambió el transporte, no el dato. Se verificó campo por campo contra `ProgressLineWire` en `Assets/Scripts/Controllers/SimulationControllerVR.cs` antes de tocar este archivo.

### 4.3 Funciones principales

Sin cambios respecto a la auditoría anterior (`hwp_transmittance()`, `generate_angles()`, `validate_params()`, `simulate_physical_experiment()`, `run()`), salvo que las llamadas de progreso ahora son `_emit_progress()` en vez de `utils.emit_progress()` directo (ver §4.2).

### 4.4 Contrato de salida (`output.json` / respuesta de `POST /simulate`)

Sin cambios — mismo sobre `status`/`experiment`/`results`/`meta` de siempre. `POST /simulate` en `server.py` devuelve exactamente este mismo diccionario como cuerpo de la respuesta HTTP, sin transformarlo.

**Nota histórica sin cambios**: la discrepancia `coincidences_Nc` vs. `coincidences` documentada en la auditoría anterior seguía siendo puramente local (archivo gitignored); no se volvió a observar.

## 5. `server.py` — API FastAPI **(A2, Semana 2 — ya no es borrador)**

La auditoría del 2026-08-26 señalaba que `server.py` no existía. Se agregó en Semana 1 con `POST /simulate` funcional y `GET /simulate/stream` como placeholder explícito; en Semana 2 el stream se conectó de verdad.

### 5.1 `POST /simulate`

Sin cambios de contrato desde Semana 1: recibe `{"experiment": "...", "parameters": {"num_pulses": ..., "num_runs": ...}}` validado por Pydantic (`SimulationRequest`/`SimulationParameters`), llama a `EXPERIMENTS[experiment].run(params)`, devuelve el mismo sobre que `main.py`. Nuevo en Semana 2: antes de correr, vacía la cola de progreso compartida (`_drain_progress_queue()`) para que progreso sin consumir de una corrida anterior no se mezcle con la nueva.

### 5.2 `GET /simulate/stream` — implementación real

- `init_progress_system()` corre una sola vez, en `@app.on_event("startup")`: crea la cola (`utils.create_progress_queue()`) y registra el callback en cada módulo de `EXPERIMENTS` (hoy solo `simulator`/`grangier_hwp`).
- El generador `_progress_stream()` traduce cada payload de la cola a un frame SSE (`data: {...}\n\n`), y termina el stream cuando ve `{"type": "end", ...}`.
- **Detalle de diseño importante, corregido respecto al `.stub` original**: `queue.Queue.get(timeout=...)` es bloqueante. Llamarlo directamente dentro de una función `async def` bloquearía el event loop de asyncio entero durante ese tiempo — congelando *todas* las requests concurrentes, no solo el stream (eso es justo lo que hacía `server_sse_handler.py.stub`, llamando `.get()` directo dentro del generador async). La implementación real usa `await asyncio.to_thread(_progress_queue.get, True, timeout)`: corre la llamada bloqueante en un hilo del threadpool, dejando el event loop libre para atender otras requests mientras espera. Verificado con una prueba dedicada — ver §9.
- Si no hay progreso nuevo dentro de `_STREAM_POLL_TIMEOUT_S` (1 segundo), se manda un comentario SSE `: keep-alive\n\n` — evita que proxies/load balancers cierren la conexión por inactividad, sin que Unity lo interprete como progreso real (`SSEStreamReader.cs` ya ignora líneas que no empiezan con `data:`).
- Headers de la respuesta (`Cache-Control: no-cache`, `Connection: keep-alive`, `X-Accel-Buffering: no`) evitan que un proxy intermedio bufferee el stream completo antes de entregarlo — sin esto, Unity podría recibir todo el progreso de golpe al final en vez de en vivo.

### 5.3 Limitación conocida (aceptada para este MVP)

Una sola cola de progreso **global**, compartida por todas las requests — no hay una `SimulationSession` por cliente todavía (eso es Fase 2 del Plan Maestro, §2.1). Si dos clientes corrieran simulaciones al mismo tiempo verían una mezcla del progreso de ambas. Se acepta porque el modelo de uso real es un único visor VR disparando un experimento a la vez desde el GameObject "Boton" (`SimulationControllerVR.RunGrangierSimulation()`), no un servicio multi-usuario. Documentado también como comentario en el propio `server.py`.

## 6. `wave_simulator.py` — experimento Wave Interference

Sin cambios respecto a la auditoría anterior (`FIXED_VISIBILITY_MVP`, sin punto de entrada en ninguna escena). No se le agregó soporte de callback de progreso en A1 porque nada lo invoca todavía vía red — si se activa en el futuro, seguiría el mismo patrón que `simulator.py` (§4.2) y debe agregarse también a `server.py::EXPERIMENTS` para heredar el callback automáticamente vía `init_progress_system()`.

## 7. Contrato `input.json` (solo modo CLI)

Sin cambios — sigue aplicando únicamente a `main.py`. `server.py` no lee `input.json`; recibe los parámetros en el body de `POST /simulate`.

## 8. Dockerización **(A3, Semana 2)**

`Backend/Dockerfile` empaqueta el servidor FastAPI:

- Base `python:3.10-slim` — misma versión de Python que corre localmente en esta auditoría, elegida a propósito para no introducir diferencias de comportamiento entre el entorno de desarrollo y el contenedor.
- Copia `requirements.txt` antes que el resto del código, para cachear la capa de `pip install` mientras no cambien las dependencias.
- Corre como usuario sin privilegios (`appuser`), no como root.
- `CMD` usa `uvicorn server:app --host 0.0.0.0 --port 8000` directamente — **no** el bloque `if __name__ == "__main__"` de `server.py` (que usa `host="127.0.0.1"` y `reload=True`, pensado para desarrollo local). `--host 0.0.0.0` es lo que hace que el puerto publicado sea alcanzable desde fuera del contenedor.

```bash
cd Backend
docker build -t virtual-lab-backend .
docker run --rm -p 8000:8000 virtual-lab-backend

# Equivalente, vía docker-compose.yml (incluido junto al Dockerfile):
docker compose up --build
```

**Conexión con el objetivo de "visor inalámbrico"**: una vez el contenedor corre en una máquina de la red local, cualquier dispositivo en la misma red Wi-Fi puede llegar a `http://<IP-local-de-esa-máquina>:8000` — incluido el visor VR. El único cambio pendiente del lado de Unity es actualizar el campo `backendUrl` de `SimulationClient`/`SSEStreamReader` (hoy `http://localhost:8000` en `Scene_DosDet.unity`/`Scene_TresDet.unity`) a esa IP; no hace falta tocar ningún script — ambos componentes ya exponen `backendUrl` como campo serializado editable desde el Inspector. `localhost` solo seguiría funcionando si el propio visor corriera el contenedor, que no es el caso de un visor standalone conectado por Wi-Fi a una PC.

## 9. Estado de las pruebas (A4, Semana 2)

Esta sandbox tiene una restricción de red saliente hacia `pypi.org` (confirmada, no intermitente — ver nota de infraestructura en `04_Plan_Maestro_Migracion.md`), así que no fue posible instalar `fastapi`/`uvicorn` aquí ni levantar `server.py` de verdad para probar `GET /simulate/stream` con una request HTTP real de punta a punta. Esto es una limitación del entorno donde se escribió este código, no del código en sí — se documenta explícitamente en vez de dar por hecho que "ya se probó".

Lo que **sí** se verificó en esta sesión sin depender de FastAPI/uvicorn:

- `python3 -m py_compile` sobre `simulator.py`, `utils.py` y `server.py` — sintaxis válida.
- Integración real del callback de progreso (A1): se registró un callback con `utils.create_progress_queue()`, se corrió `simulator.run()` de verdad, y se confirmó que la cola recibe exactamente los eventos esperados (`start` → N × `progress` → `end`) en el orden correcto, y que `run()` sigue devolviendo `status: "ok"` con normalidad.
- La lógica exacta de `_progress_stream()` (misma implementación, sin FastAPI alrededor) corriendo `simulator.run()` en un hilo de fondo mientras un "ticker" de asyncio corre concurrentemente en el mismo event loop: confirma que el loop **no** se bloquea (15/15 ticks del ticker se completaron durante el streaming) y que los 18 eventos esperados (`start` + 16 `progress` + `end`) llegan en orden — la prueba concreta de que `asyncio.to_thread()` sí resuelve el problema descrito en §5.2.

Lo que **no** se pudo probar en esta sandbox (pendiente de correr una sola vez en una máquina con acceso normal a internet — no requiere nada más que estos comandos):

```bash
# 1. Instalar dependencias
cd Backend
pip install -r requirements.txt

# 2. Levantar el servidor
uvicorn server:app --reload

# 3. En otra terminal: disparar una simulación
curl -X POST http://127.0.0.1:8000/simulate \
  -H "Content-Type: application/json" \
  -d '{"experiment": "grangier_hwp", "parameters": {"num_pulses": 500, "num_runs": 2}}'

# 4. En otra terminal más (abrirla antes o durante el paso 3): ver el stream SSE real
curl -N http://127.0.0.1:8000/simulate/stream
```

Con el paso 4 corriendo, se debería ver aparecer en vivo cada línea `data: {...}` a medida que el paso 3 avanza, terminando en `data: {"type": "end", "status": "ok", ...}`. Si en cambio todo llega de golpe al final, revisar que ningún proxy intermedio esté bufferizando (ver headers en §5.2). Este mismo comando de Docker (§8) es una alternativa equivalente a los pasos 1-2.

## 10. Ver también

- `00_Overview_Arquitectura.md` §2 — ciclo de vida completo de una corrida (Unity ↔ Python).
- `01_Frontend_Unity.md` §2 — cómo consume Unity estas mismas líneas de progreso y el `output.json`.
- `03_Cumplimiento_y_Brechas.md` — brecha entre esta arquitectura y la descrita en la Guía de Estándares. **Nota**: no se auditó en esta pasada; puede estar desactualizado respecto a lo de Semana 2 documentado aquí.
- `04_Plan_Maestro_Migracion.md` §5 (Fase 1) — entregables 1.3 (FastAPI+SSE) y 1.5 (Docker), documentados aquí ya como completos, no como pendientes.
