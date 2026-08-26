"""Backend/server.py — Fase 1.3 del Plan Maestro (Infraestructura de Red y Centralización).

Borrador inicial de la API FastAPI que va a reemplazar gradualmente el flujo actual
de subproceso + archivos (ver Backend/main.py). Por ahora **coexiste** con ese flujo:
reutiliza los mismos módulos de física (simulator.py) y las mismas utilidades
(utils.py) para no duplicar lógica ya validada durante la transición.

Estado en esta entrega (Semana 1 del Plan Maestro):

- ``POST /simulate``: endpoint funcional. Recibe los parámetros validados por
  Pydantic (en vez de leerlos de ``input.json``) y devuelve el mismo sobre de
  respuesta (``status``/``results``/``meta`` o ``status``/``message``/``details``)
  que hoy produce ``main.py`` — es el equivalente síncrono del ciclo actual, pero
  sin tocar disco para la entrada.
- ``GET /simulate/stream``: **borrador** de ``StreamingResponse`` (SSE). Todavía
  NO está conectado al progreso real que hoy emite ``simulator.py`` por stdout
  (vía ``utils.emit_progress()``). Conectar esto correctamente implica que
  ``simulator.py`` pueda alimentar una cola/callback en vez de solo imprimir a
  stdout — eso es trabajo de la Semana 2. Este endpoint por ahora solo deja
  documentada la forma del contrato SSE que Unity va a consumir vía
  ``UnityWebRequest`` (Fase 1.4).

Para correr localmente (una vez instaladas las dependencias de requirements.txt):

    uvicorn server:app --reload
"""

from __future__ import annotations

import asyncio
import json
from typing import AsyncGenerator, Any, Dict

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

    try:
        result = module.run(params)
    except Exception as exc:  # pragma: no cover - mismo catch-all defensivo que main.py
        return utils.build_error_response(
            message=f"Error inesperado ejecutando '{request.experiment}': {exc}",
            experiment=request.experiment,
        )

    return result


# ---------------------------------------------------------------------------
# GET /simulate/stream — DRAFT de SSE (Fase 1.3, progreso en vivo).
# ---------------------------------------------------------------------------

async def _draft_progress_stream() -> AsyncGenerator[str, None]:
    """Placeholder: emite un único evento SSE de marcador de posición.

    TODO (Semana 2 del Plan Maestro): reemplazar por progreso real. Probablemente
    haga falta que simulator.py acepte un callback/asyncio.Queue para alimentar
    este generador corrida por corrida, en vez de depender de
    utils.emit_progress() imprimiendo a stdout (que solo tiene sentido en el
    modelo de subproceso de main.py, no en un servidor persistente).
    """
    payload = {"type": "progress", "status": "draft-not-yet-implemented"}
    yield f"data: {json.dumps(payload, ensure_ascii=False)}\n\n"
    await asyncio.sleep(0)


@app.get("/simulate/stream")
async def stream_simulation_progress() -> StreamingResponse:
    return StreamingResponse(_draft_progress_stream(), media_type="text/event-stream")


if __name__ == "__main__":
    import uvicorn

    uvicorn.run("server:app", host="127.0.0.1", port=8000, reload=True)
