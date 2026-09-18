"""Backend/server.py — Fase 1.3 del Plan Maestro (Infraestructura de Red y Centralización).

API FastAPI que reemplaza gradualmente el flujo de subproceso + archivos (ver
Backend/main.py, que sigue funcionando igual que siempre para uso por línea de
comandos). Reutiliza los mismos módulos de física (simulator.py) y las mismas
utilidades (utils.py) para no duplicar lógica ya validada durante la transición.

Estado en esta entrega (Semana 2 del Plan Maestro — Pista A completa):

- ``POST /simulate``: mismo contrato desde la Semana 1 (mismo sobre de
  respuesta ``status``/``results``/``meta`` o ``status``/``message``/``details``
  que produce ``main.py``). Ahora además vacía la cola de progreso compartida
  antes de correr (ver ``_drain_progress_queue()``), para que progreso viejo
  sin consumir no se mezcle con el de la corrida nueva.
- ``GET /simulate/stream``: **implementación real** (ya no borrador). Lee la
  cola de progreso que ``simulator.py`` alimenta a través del callback
  registrado en ``init_progress_system()`` (ver A1 en
  ``Backend/utils.py::create_progress_queue()`` y
  ``Backend/simulator.py::set_progress_callback()``) y la traduce a frames SSE
  (``data: {...}\\n\\n``). Usa ``asyncio.to_thread()`` para leer la cola
  (bloqueante por diseño) sin bloquear el event loop — ver el docstring de
  ``_progress_stream()`` para el porqué.

Limitación conocida y aceptada para este MVP (documentada también en
``Docs/02_Backend_Python.md``): existe una sola cola de progreso global,
compartida por todas las requests. Alcanza para el modelo de uso real (un
visor VR, un experimento a la vez, disparado desde el GameObject "Boton" —
ver ``Assets/Scripts/Controllers/SimulationControllerVR.cs``), pero no aísla
el progreso si en el futuro llegan a correr simulaciones concurrentes.

Para correr localmente (una vez instaladas las dependencias de requirements.txt):

    uvicorn server:app --reload
"""

from __future__ import annotations

import asyncio
import json
import queue
from typing import AsyncGenerator, Any, Dict, Optional

from fastapi import FastAPI, HTTPException
from fastapi.responses import StreamingResponse
from pydantic import BaseModel, Field

import simulator
import utils

app = FastAPI(
    title="Simulador Óptico VR — Backend API",
    description=(
        "Fase 1 del Plan Maestro: migración del procesamiento por lotes local "
        "(subproceso + input.json/output.json) a una arquitectura de red "
        "(FastAPI + SSE en la Fase 1, WebSockets en la Fase 2)."
    ),
    version="0.1.0",
)


# ---------------------------------------------------------------------------
# DTOs (Pydantic) — Fase 1.2 del Plan Maestro ("Generación Dinámica de
# Parámetros"): mismo contrato de datos que hoy vive en input.json, pero ahora
# expuesto como esquema validado en el body del request en vez de JSON manual
# escrito a disco.
# ---------------------------------------------------------------------------

class SimulationParameters(BaseModel):
    """Espejo de ``configuration.parameters`` en el input.json actual."""

    num_pulses: int = Field(..., gt=0, description="Número de pulsos a simular.")
    num_runs: int = Field(..., gt=0, description="Número de corridas por ángulo.")


class SimulationRequest(BaseModel):
    experiment: str = Field(
        default="grangier_hwp",
        description="Nombre del experimento (ver EXPERIMENTS en main.py/este módulo).",
    )
    parameters: SimulationParameters


def _model_to_dict(model: BaseModel) -> Dict[str, Any]:
    """Compatibilidad Pydantic v1/v2: .model_dump() no existe en v1, .dict() está
    deprecado (pero sigue funcionando) en v2. Este repo no fija todavía una
    versión exacta en requirements.txt, así que soportamos ambas."""
    if hasattr(model, "model_dump"):
        return model.model_dump()
    return model.dict()


# Mapeo experimento -> módulo de física. Hoy solo migramos "grangier_hwp"
# (wave_interference sigue sin un punto de entrada real desde ninguna escena,
# ver 02_Backend_Python.md §5 — se agrega aquí cuando eso cambie).
EXPERIMENTS = {
    "grangier_hwp": simulator,
}


# ---------------------------------------------------------------------------
# Progreso en vivo (Semana 2 del Plan Maestro, A2) — conecta GET
# /simulate/stream con el callback de progreso que simulator.py soporta desde
# A1 (ver Backend/simulator.py::set_progress_callback y
# Backend/utils.py::create_progress_queue).
# ---------------------------------------------------------------------------

_progress_queue: Optional["queue.Queue"] = None


def init_progress_system() -> None:
    """Crea la cola de progreso y la registra como callback en cada módulo de
    experimento (hoy solo simulator.py/grangier_hwp; queda listo para cuando
    se agregue wave_interference u otro experimento a EXPERIMENTS, siempre
    que también implemente set_progress_callback()).

    Se llama una sola vez, en el evento startup de FastAPI — no en cada
    request — porque la cola tiene que sobrevivir entre POST /simulate y
    GET /simulate/stream, que llegan como requests HTTP independientes.
    """
    global _progress_queue
    _progress_queue, callback = utils.create_progress_queue()
    for module in EXPERIMENTS.values():
        module.set_progress_callback(callback)


def _drain_progress_queue() -> None:
    """Vacía cualquier progreso sin consumir de una corrida anterior.

    Se llama al arrancar POST /simulate: si nadie llegó a abrir GET
    /simulate/stream a tiempo la vez pasada (o el cliente se desconectó a
    medias), no queremos que esos eventos viejos se mezclen con el progreso
    de la corrida nueva la próxima vez que alguien sí se conecte.
    """
    if _progress_queue is None:
        return
    while True:
        try:
            _progress_queue.get_nowait()
        except queue.Empty:
            break


@app.on_event("startup")
def _on_startup() -> None:
    init_progress_system()


# ---------------------------------------------------------------------------
# POST /simulate — versión síncrona sobre red del flujo que hoy corre
# Backend/main.py como subproceso.
# ---------------------------------------------------------------------------

@app.post("/simulate")
def run_simulation(request: SimulationRequest) -> Dict[str, Any]:
    module = EXPERIMENTS.get(request.experiment)
    if module is None:
        raise HTTPException(
            status_code=400,
            detail=f"Experimento desconocido: '{request.experiment}'. Válidos: {', '.join(EXPERIMENTS)}.",
        )

    params = _model_to_dict(request.parameters)
    _drain_progress_queue()

    try:
        result = module.run(params)
    except Exception as exc:  # pragma: no cover - mismo catch-all defensivo que main.py
        return utils.build_error_response(
            message=f"Error inesperado ejecutando '{request.experiment}': {exc}",
            experiment=request.experiment,
        )

    return result


# ---------------------------------------------------------------------------
# GET /simulate/stream — SSE real (Semana 2 del Plan Maestro, A2).
# ---------------------------------------------------------------------------

_STREAM_POLL_TIMEOUT_S = 1.0  # cada cuánto se manda un keep-alive si no hay progreso nuevo


async def _progress_stream() -> AsyncGenerator[str, None]:
    """Traduce la cola de progreso (alimentada por simulator.py vía el
    callback registrado en init_progress_system()) a frames SSE
    ``data: {...}\\n\\n``.

    ``_progress_queue.get(timeout=...)`` es una llamada BLOQUEANTE de
    queue.Queue — a propósito, ver utils.create_progress_queue(). Llamarla
    directamente aquí adentro de este generador async bloquearía el event
    loop entero de asyncio hasta _STREAM_POLL_TIMEOUT_S segundos en cada
    iteración, lo cual serializaría TODAS las requests concurrentes mientras
    tanto (incluido POST /simulate de otros clientes). ``asyncio.to_thread()``
    corre esa llamada bloqueante en un hilo del threadpool en vez del event
    loop, evitando el problema.
    """
    if _progress_queue is None:
        # No debería pasar en la práctica (init_progress_system() corre en
        # startup antes de aceptar requests), pero cubrimos el caso por si
        # alguien llega a llamar este generador en un contexto de pruebas
        # que no dispara el evento startup.
        payload = {"type": "end", "status": "error",
                    "message": "Sistema de progreso no inicializado (init_progress_system no corrió)."}
        yield f"data: {json.dumps(payload, ensure_ascii=False)}\n\n"
        return

    while True:
        try:
            payload = await asyncio.to_thread(_progress_queue.get, True, _STREAM_POLL_TIMEOUT_S)
        except queue.Empty:
            yield ": keep-alive\n\n"
            continue

        yield f"data: {json.dumps(payload, ensure_ascii=False)}\n\n"

        if payload.get("type") == "end":
            break


@app.get("/simulate/stream")
async def stream_simulation_progress() -> StreamingResponse:
    return StreamingResponse(
        _progress_stream(),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "X-Accel-Buffering": "no",  # evita que nginx/proxies hagan buffering del stream
        },
    )


if __name__ == "__main__":
    import uvicorn

    uvicorn.run("server:app", host="127.0.0.1", port=8000, reload=True)
