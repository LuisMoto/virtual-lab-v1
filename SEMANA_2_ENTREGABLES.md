# SEMANA 2 — División de Trabajo: Pista A (Python) y Pista B (Unity)

**Rama**: `semana-2/integracion-red`
**Duración**: 5 días de trabajo (lunes-viernes)
**Objetivo**: Backend + Cliente con SSE en vivo, sin dependencia de disco

---

## PISTA A — Backend/Python (para tu compañera)

**Responsable**: Compañera  
**Archivos principales**: `Backend/server.py`, `Backend/simulator.py`, `Backend/utils.py`  
**Objetivo final**: servidor FastAPI con SSE funcional, Dockerfile listo

### A1 — Refactorizar progreso a callback (Día 1-2)
- **Archivo**: `Backend/simulator.py` + `Backend/utils.py`
- **Qué hacer**: cambiar `utils.emit_progress()` de stdout-only a sistema de callback/cola
- **Por qué**: SSE necesita consumir el progreso desde una cola persistente, no desde stdout
- **Entregable**: `simulator.py` emite a callback registrable; `utils.py` expone el mecanismo
- **Template**: `Backend/simulator_progress_callback.py.stub`

### A2 — SSE en FastAPI (Día 2-3)
- **Archivo**: `Backend/server.py`
- **Qué hacer**: conectar `GET /simulate/stream` a la cola de progreso de A1
- **Testing**: `curl --stream http://localhost:8000/simulate/stream` mientras corre
- **Entregable**: endpoint SSE funcional, líneas de progreso en tiempo real
- **Template**: `Backend/server_sse_handler.py.stub`

### A3 — Docker (Día 3-4)
- **Archivos**: `Dockerfile`, `docker-compose.yml` (opcional: `.dockerignore`)
- **Qué hacer**: contenerizar backend, exponer puerto 8000
- **Testing**: `docker build -t virtual-lab-backend . && docker run -p 8000:8000 virtual-lab-backend`
- **Entregable**: imagen reproducible, compatible con Windows/Mac/Linux

### A4 — Testing + Docs (Día 4-5)
- **Archivo**: `Backend/tests/test_sse_client.py` (nuevo)
- **Qué hacer**: cliente simple que se conecte a SSE, parsee eventos, valide formato
- **Actualizar**: `Backend/README.md` con instrucciones de setup local + Docker
- **Entregable**: tests verdes, documentación clara

---

## PISTA B — Frontend/Unity (para Luis)

**Responsable**: Luis  
**Archivos principales**: `Assets/Scripts/Networking/SimulationClient.cs`, `SSEStreamReader.cs`  
**Objetivo final**: Cliente HTTP+SSE conectado, UI reactiva, sin lectura de disco

### B1 — UnityWebRequest POST (Día 1-2)
- **Archivo**: `Assets/Scripts/Networking/SimulationClient.cs` (nuevo)
- **Qué hacer**: reemplazar `System.Diagnostics.Process` por `UnityWebRequest.Post()`
- **Target**: `http://localhost:8000/simulate` (o variable en inspector)
- **Entregable**: POST funcional, respuesta parseada, error handling
- **Template**: `Assets/Scripts/Networking/SimulationClient.cs.stub`

### B2 — SSE Cliente (Día 2-3)
- **Archivo**: `Assets/Scripts/Networking/SSEStreamReader.cs` (nuevo)
- **Qué hacer**: conexión a `GET /simulate/stream`, parseo de eventos, callback de líneas
- **Nota**: Unity no tiene EventSource nativo; usar coroutine + `UnityWebRequest` con `chunked=true`
- **Entregable**: eventos parseados, callback por línea de progreso
- **Template**: `Assets/Scripts/Networking/SSEStreamReader.cs.stub`

### B3 — Integración UI (Día 3-4)
- **Archivo**: `Assets/Scripts/Managers/SimulationUIController.cs` (modificar existente)
- **Qué hacer**: conectar B1+B2 al flujo de UI existente
- **Cambios**: reemplazar lectura de disco por actualización desde callback SSE
- **Entregable**: barras de progreso actualizadas en vivo, sin polling de archivos

### B4 — Pruebas End-to-End (Día 4-5)
- **Qué hacer**: backend corriendo → cliente lanza simulación → progreso en vivo → resumen
- **Testing**: ambas escenas (DosDet, TresDet), validar UI + logs
- **Entregable**: video o log de ejecución limpia

---

## Puntos de Sincronización

| Hito | Día | Responsable | Acción |
|------|-----|-------------|--------|
| A1 completo + prueba local | Miércoles (Día 3) | Compañera | Push a rama, notifica a Luis |
| B1 listo, puede conectar a A1 | Miércoles (Día 3) | Luis | Integra POST, testa con backend local |
| A2+SSE funcional | Jueves (Día 4) | Compañera | Habilita SSE, comparte endpoint |
| B2 parseando eventos | Jueves (Día 4) | Luis | Conecta SSE, valida líneas recibidas |
| A3 Docker | Viernes (Día 5) | Compañera | Dockerfile probado localmente |
| B3 UI reactiva | Viernes (Día 5) | Luis | Flujo end-to-end corriendo |
| Cierre: PR → main | Viernes (Día 5) | Ambos | Review + merge |

---

## Estructura de Archivos a Crear/Modificar

```
Backend/
├── server.py                          (modificar: agregar SSE funcional)
├── simulator.py                       (modificar: agregar callback/cola)
├── utils.py                           (modificar: callback mechanism)
├── requirements.txt                   (ya existe)
├── Dockerfile                         (crear)
├── docker-compose.yml                 (crear, opcional)
├── tests/
│   └── test_sse_client.py            (crear)
└── README.md                          (actualizar)

Assets/Scripts/Networking/
├── SimulationClient.cs                (crear)
├── SSEStreamReader.cs                 (crear)
└── (vacía, lista para expansión)

Assets/Scripts/Managers/
└── SimulationUIController.cs          (modificar: conectar a callbacks)
```

---

## Criterios de "Listo para Semana 3"

- [ ] Backend: `POST /simulate` + `GET /simulate/stream` ambos funcionales
- [ ] Backend: Dockerfile build y run sin errores
- [ ] Unity: `SimulationClient` conecta y obtiene respuesta del servidor
- [ ] Unity: `SSEStreamReader` parsea eventos en tiempo real
- [ ] UI: barras de progreso actualizadas sin lectura de disco
- [ ] Tests: suite verde (backend + cliente)
- [ ] Documentación: README actualizado con setup local + Docker
- [ ] Rama: todos los commits pusheados a `semana-2/integracion-red`

---

## Templates Disponibles

Ver archivos `.stub` en esta rama para plantillas de código comentadas:
- `Backend/simulator_progress_callback.py.stub` — cómo exponer callbacks
- `Backend/server_sse_handler.py.stub` — handler SSE en FastAPI
- `Assets/Scripts/Networking/SimulationClient.cs.stub` — cliente HTTP
- `Assets/Scripts/Networking/SSEStreamReader.cs.stub` — lector SSE

---

## Contacto / Escalación

Si hay bloqueos entre Pista A y Pista B, resuelve en el acto vía:
1. Chat rápido (5 min)
2. Checkpoint en la rama (push intermedios)
3. PR draft si necesitas review temprano
