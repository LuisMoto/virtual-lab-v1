"""Backend/tests/test_sse_client.py — A4 (Plan Maestro, Semana 2, Pista A).

Cliente de prueba de integración contra un servidor real: POST /simulate +
GET /simulate/stream. Requiere que el backend ya esté corriendo en
http://127.0.0.1:8000 antes de ejecutar este archivo — no lo levanta por sí
mismo. Para levantarlo:

    cd Backend && uvicorn server:app --reload
    # o, equivalente:
    cd Backend && docker compose up --build

Deliberadamente usa solo la librería estándar (http.client, json,
threading), sin requests/httpx/pytest: Backend/requirements.txt declara
únicamente fastapi + uvicorn[standard] (lo mínimo para correr el servidor),
y no vale la pena forzar una dependencia nueva solo para probarlo —
http.client ya sabe leer una respuesta chunked/streaming línea por línea,
que es todo lo que hace falta para parsear SSE.

Cómo correr:
    - Con pytest instalado:      pytest Backend/tests/test_sse_client.py -v
    - Sin pytest (solo stdlib):  python Backend/tests/test_sse_client.py

En ambos casos son pruebas de integración contra un servidor real, no
pruebas unitarias con mocks — por diseño: lo que hay que validar aquí es
justamente que HTTP + SSE de punta a punta funcionan, no solo la lógica en
aislamiento (esa ya se cubre indirectamente al probar simulator.py/utils.py
por separado).

Nota de esta entrega: este archivo se escribió y revisó con cuidado, pero
no pudo ejecutarse en el entorno donde se escribió (sandbox sin acceso de
red saliente hacia pypi.org, así que no se pudo instalar fastapi/uvicorn
para levantar el servidor ahí mismo — ver Docs/02_Backend_Python.md §9).
Ejecutarlo una vez en una máquina normal, con el servidor corriendo, es el
único paso de validación que falta.
"""

import http.client
import json
import threading
import time

HOST = "127.0.0.1"
PORT = 8000
TIMEOUT_S = 10


def _post_simulate(num_pulses=300, num_runs=2, experiment="grangier_hwp"):
    """Hace POST /simulate y devuelve (status_code, cuerpo_json_parseado)."""
    conn = http.client.HTTPConnection(HOST, PORT, timeout=TIMEOUT_S)
    body = json.dumps({
        "experiment": experiment,
        "parameters": {"num_pulses": num_pulses, "num_runs": num_runs},
    })
    try:
        conn.request("POST", "/simulate", body=body,
                      headers={"Content-Type": "application/json"})
        resp = conn.getresponse()
        data = json.loads(resp.read().decode("utf-8"))
        return resp.status, data
    finally:
        conn.close()


def _read_sse_events(resp, max_events=500):
    """Lee frames `data: {...}` de una respuesta SSE ya abierta (resp =
    http.client.HTTPResponse de un GET /simulate/stream en curso) y los
    devuelve como lista de dicts. Se detiene al ver type == "end", al
    agotar max_events, o al cerrarse la conexión."""
    events = []
    while len(events) < max_events:
        line = resp.readline()
        if not line:
            break  # conexión cerrada por el servidor
        line = line.decode("utf-8").strip()
        if not line or line.startswith(":"):
            continue  # línea vacía (separador SSE) o comentario keep-alive
        if line.startswith("data:"):
            payload = json.loads(line[len("data:"):].strip())
            events.append(payload)
            if payload.get("type") == "end":
                break
    return events


def test_post_simulate_returns_ok():
    """POST /simulate con parámetros válidos debe devolver 200 y
    status: "ok" en el cuerpo, con al menos un punto en hwp_sweep."""
    status_code, data = _post_simulate()
    assert status_code == 200, f"esperaba 200, recibí {status_code}: {data}"
    assert data.get("status") == "ok", f"respuesta inesperada: {data}"
    sweep = data.get("results", {}).get("hwp_sweep", [])
    assert len(sweep) >= 1, f"hwp_sweep vacío: {data}"


def test_post_simulate_rejects_unknown_experiment():
    """POST /simulate con un experimento inexistente debe devolver 400 (no
    un 500 ni un "ok" ambiguo) — ver HTTPException en server.py::run_simulation."""
    status_code, data = _post_simulate(experiment="experimento_que_no_existe")
    assert status_code == 400, f"esperaba 400, recibí {status_code}: {data}"


def test_stream_yields_progress_events_in_order():
    """Abre GET /simulate/stream, dispara POST /simulate en paralelo, y
    valida que el stream entregue, en orden: un evento "start", al menos un
    evento "progress" con las claves esperadas, y un evento final "end" con
    status "ok" — sin quedarse colgado."""
    conn = http.client.HTTPConnection(HOST, PORT, timeout=TIMEOUT_S)
    conn.request("GET", "/simulate/stream")
    resp = conn.getresponse()

    result = {}

    def consume_stream():
        result["events"] = _read_sse_events(resp)

    stream_thread = threading.Thread(target=consume_stream)
    stream_thread.start()

    # Pequeño margen para que el GET ya esté conectado antes de disparar la
    # simulación. No es estrictamente necesario -- server.py mantiene la
    # cola de progreso desde el startup, no desde que se conecta un cliente
    # -- pero da margen en máquinas lentas o bajo carga.
    time.sleep(0.2)

    try:
        status_code, post_data = _post_simulate(num_pulses=300, num_runs=2)
        assert status_code == 200
        assert post_data.get("status") == "ok"

        stream_thread.join(timeout=TIMEOUT_S)
    finally:
        conn.close()

    events = result.get("events", [])
    assert len(events) >= 2, f"se esperaban al menos start+end, se recibió: {events}"
    assert events[0]["type"] == "start", f"primer evento inesperado: {events[0]}"
    assert events[-1]["type"] == "end", f"último evento inesperado: {events[-1]}"
    assert events[-1]["status"] == "ok", f"la corrida no terminó en ok: {events[-1]}"

    progress_events = [e for e in events if e["type"] == "progress"]
    assert len(progress_events) > 0, "no llegó ningún evento de progreso"

    expected_keys = {
        "type", "experiment", "angle_deg", "detector_mode", "num_test",
        "witness_count", "transmitted_count", "reflected_count",
        "triple_coincidence_count", "g2", "insufficient_statistics",
    }
    missing = expected_keys - set(progress_events[0].keys())
    assert not missing, f"faltan claves en el evento de progreso: {missing}"


_ALL_TESTS = [
    test_post_simulate_returns_ok,
    test_post_simulate_rejects_unknown_experiment,
    test_stream_yields_progress_events_in_order,
]


if __name__ == "__main__":
    # Modo standalone (sin pytest instalado): corre cada test y reporta
    # PASS/FAIL/ERROR, con código de salida != 0 si algo falló. Pensado
    # para probar a mano contra `uvicorn server:app` (o el contenedor
    # Docker) corriendo en otra terminal, sin depender de tener pytest.
    failures = 0
    for test_fn in _ALL_TESTS:
        name = test_fn.__name__
        try:
            test_fn()
            print(f"PASS   {name}")
        except AssertionError as e:
            failures += 1
            print(f"FAIL   {name}: {e}")
        except OSError as e:
            failures += 1
            print(f"ERROR  {name}: no se pudo conectar a http://{HOST}:{PORT} "
                  f"-- ¿está corriendo `uvicorn server:app` o el contenedor Docker? ({e})")

    total = len(_ALL_TESTS)
    if failures:
        print(f"\n{failures}/{total} pruebas fallaron.")
        raise SystemExit(1)
    print(f"\n{total}/{total} pruebas OK.")
