# 02. Backend Python — Estado Real

**Fecha de auditoría**: 2026-08-26 · **Rama**: `MVP_escenas`

---

## 1. Inventario real de `Backend/`

```
Backend/
├── main.py             (67 líneas — dispatcher CLI)
├── utils.py             (108 líneas — I/O compartido, validación, progreso)
├── simulator.py         (283 líneas — experimento Grangier/HWP)
├── wave_simulator.py     (47 líneas — experimento Wave Interference)
├── input.json            (última entrada real, gitignored)
├── output.json            (última salida real, gitignored)
└── resultados_grangier_hwp_*.csv / results_grangier_hwp_*.csv
    (~50 archivos, 2026-07-29 a 2026-08-23, gitignored)
```

**No existen** `server.py`, `optical_physics.py`, `requirements.txt`, `models/`, `utils/` (como carpeta), ni `tests/` — pese a que la Guía de Estándares del proyecto (`ESTANDARES_DOCUMENTACION_TECNICA.md`, sección 2) describe esa estructura como la del `Backend/`. Ver brecha en `03_Cumplimiento_y_Brechas.md`.

**Housekeeping**: la carpeta acumula decenas de CSV de corridas pasadas (`resultados_grangier_hwp_YYYYMMDD_HHMMSS_<ticks>.csv`, y una variante más reciente sin el prefijo en español, `results_grangier_hwp_...csv` — coincide con el commit de homologación). Están correctamente excluidos de git (`.gitignore` cubre `Backend/*.csv`), pero nada los limpia del disco local; con el tiempo ensucian la carpeta de trabajo. Sugerencia menor: moverlos a una subcarpeta dedicada (p. ej. `Backend/runs/`) o agregar un script de limpieza — no es un problema de repositorio, es higiene de disco local.

---

## 2. `main.py` — dispatcher

- No es un servidor: es un script que se invoca una vez por corrida vía `python main.py <experimento>`.
- `EXPERIMENTS = {"grangier_hwp": simulator, "wave_interference": wave_simulator}` — mapea el nombre de experimento (recibido en `sys.argv[1]`) al módulo correspondiente.
- Flujo: `utils.read_input_with_retries()` → `utils.extract_parameters()` → `module.run(params)` → `utils.write_json_atomic()`.
- Código de salida: `0` si `result.get("status") == "ok"`, `1` en cualquier otro caso — es lo que Unity revisa para decidir si el subproceso "tuvo éxito".

## 3. `utils.py` — utilidades compartidas

| Función | Propósito |
|---|---|
| `write_json_atomic()` | Escribe vía `tempfile.mkstemp` + `os.fsync` + `os.replace` — evita que Unity lea un `output.json` a medio escribir si el polling coincide justo con el momento de guardado. |
| `try_write_text()` | Escritura de texto con manejo de errores (usada para los CSV de exportación). |
| `build_ok_response()` / `build_error_response()` | Construyen el sobre de respuesta estándar (`status`, `experiment`, `results`/`error`, `meta`). |
| `read_input_with_retries()` | Reintenta leer `input.json` hasta 3 veces con backoff exponencial — cubre la carrera en la que Unity todavía no terminó de escribir el archivo cuando Python ya arrancó. |
| `extract_parameters()` | Lee `input_data["configuration"]["parameters"]`. |
| `validate_integer()` / `validate_float_range()` | Validación de parámetros de entrada. |
| `emit_progress()` | `print(json.dumps(payload, ensure_ascii=False), flush=True)` — el mecanismo completo de streaming en vivo hacia Unity se reduce a esta línea; el `flush=True` es lo que garantiza que cada línea llegue a Unity inmediatamente y no se quede bufferizada. |

## 4. `simulator.py` — experimento Grangier (HWP)

### 4.1 Topes de seguridad

```python
MAX_NUM_PULSES = 2_000_000
MAX_RUNS = 200
MAX_ANGLES = 360
MAX_TOTAL_OPERATIONS = 100_000_000
```

Protegen contra una entrada mal formada (o mal intencionada) desde la UI que dispare un cómputo Monte Carlo desproporcionado. Es una madurez que vale la pena preservar explícitamente si se reescribe este módulo en el futuro.

### 4.2 Funciones principales

- `hwp_transmittance()` — transmitancia del half-wave plate en función del ángulo.
- `generate_angles()` — genera la secuencia de ángulos a barrer.
- `validate_params()` — valida `num_pulses`/`num_runs` contra los topes de arriba.
- `simulate_physical_experiment(mode, num_pulses, rng, witness_prob, bs_trans, dark_count_rate, detector_efficiency)` — el núcleo Monte Carlo; calcula `g2` según la fórmula correspondiente al modo 2 (natural) o 3 (testigo) detectores.
- `_emit_run_progress()` — llama a `utils.emit_progress()` tras cada corrida individual.
- `run(params)` — punto de entrada; itera ángulos × corridas × 2 modos, emite progreso por corrida, y **solo guarda el último ángulo barrido** en `results["hwp_sweep"]` (pese al nombre "sweep", no persiste el barrido completo en el JSON final — el barrido completo sí queda en el CSV que genera aparte). Esto es coherente con que `GrangierDataReader.cs` siempre lea `hwpSweep[0]` — es el único punto que existe.

### 4.3 Contrato de salida (`output.json`)

```json
{
  "status": "ok",
  "experiment": "grangier_hwp",
  "results": {
    "hwp_sweep": [
      {
        "angle_deg": 180,
        "two_detectors": { "runs": [ { "coincidences": ..., "g2": ... } ] },
        "three_detectors": { "runs": [ { "coincidences": ..., "g2": ... } ] }
      }
    ]
  },
  "meta": { "execution_time_s": ..., "seed": ..., "config": { ... }, "csv_generated": true }
}
```

**Nota transitoria (no es un bug de repositorio)**: el `Backend/output.json` que hay en disco ahora mismo (generado el 2026-08-23, antes del refactor de homologación) usa la clave `coincidences_Nc` dentro de cada `run`. El código **actual** de `simulator.py` (post-commit `f4d59d6`) escribe `coincidences` — coincide con el DTO `RunWire.coincidences` que espera el C# actual. Confirmado por historial (`coincidences_Nc` aparece 6 veces en versiones anteriores de `simulator.py`). Como `Backend/output.json` está en `.gitignore`, este desfase es puramente local y se autocorrige en la próxima corrida — no viaja en el repositorio ni afecta a otro colaborador que clone el proyecto.

## 5. `wave_simulator.py` — experimento Wave Interference

```python
FIXED_VISIBILITY_MVP = 0.998  # placeholder, no calculado
```

- `validate_params()` valida `phase_deg`.
- `run()` calcula `relative_intensity = cos²(phase_rad / 2)` (fórmula real de interferencia de dos caminos) pero **retorna la visibilidad fija** `FIXED_VISIBILITY_MVP` en vez de calcularla a partir de las amplitudes/contrastes reales.
- Es una limitación **declarada explícitamente en el propio código** como atajo de MVP (de ahí el nombre de la constante) — no es un descuido oculto, pero conviene que quede visible en la documentación del equipo para que no se use el número como si fuera un resultado físico real, y para no perderlo de vista si se planea reemplazarlo antes de cualquier demo con público técnico.
- Ninguna escena actual dispara este experimento desde la UI (ver `00_Overview_Arquitectura.md` §3) — el soporte en `SimulationUIController.cs` (`BuildWaveSummary()`, `WaveOutput`/`WaveResults`) existe pero no se encontró el punto de entrada en las escenas auditadas.

## 6. Contrato `input.json`

```json
{ "configuration": { "parameters": { "num_pulses": 1000, "num_runs": 1 } } }
```

Estructura mínima observada en el archivo real; `extract_parameters()` en `utils.py` asume exactamente esta forma anidada.

## 7. Ver también

- `00_Overview_Arquitectura.md` §2 — ciclo de vida completo de una corrida (Unity ↔ Python).
- `01_Frontend_Unity.md` §2 — cómo consume Unity estas mismas líneas de progreso y el `output.json`.
- `03_Cumplimiento_y_Brechas.md` — brecha entre esta arquitectura real (CLI + stdout + archivos) y la arquitectura FastAPI/WebSocket descrita en la Guía de Estándares.
