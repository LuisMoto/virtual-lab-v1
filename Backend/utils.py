import json
import os
import sys
import time
import tempfile
import datetime
from typing import Any, Dict, Optional, Tuple

BACKOFF_BASE_S = 0.05

def write_json_atomic(path: str, data: dict) -> None:
    directory = os.path.dirname(os.path.abspath(path)) or "."
    fd, tmp_path = tempfile.mkstemp(dir=directory, prefix=".tmp_", suffix=".json")
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=4)
            f.flush()
            os.fsync(f.fileno())
        os.replace(tmp_path, path)
    except Exception:
        if os.path.exists(tmp_path):
            try:
                os.remove(tmp_path)
            except OSError:
                pass
        raise

def try_write_text(path: str, content: str) -> Optional[str]:
    try:
        with open(path, "w", encoding="utf-8", newline="") as f:
            f.write(content)
        return None
    except Exception as e:
        return str(e)

def build_ok_response(experiment: str, results: Dict[str, Any],
                       meta: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
    return {
        "status": "ok",
        "experiment": experiment,
        "results": results,
        "meta": meta or {},
    }

def build_error_response(message: str, details: Any = None,
                          experiment: Optional[str] = None) -> Dict[str, Any]:
    return {
        "status": "error",
        "experiment": experiment,
        "message": message,
        "details": details or {},
        "timestamp": datetime.datetime.now().isoformat(),
    }

def read_input_with_retries(path: str, attempts: int = 3) -> Tuple[Optional[dict], Optional[str]]:
    if not os.path.exists(path):
        return None, None

    last_error = "desconocido"
    for attempt in range(attempts):
        try:
            with open(path, "r", encoding="utf-8") as f:
                return json.load(f), None
        except FileNotFoundError:
            last_error = f"No se encontró el archivo '{path}'."
        except json.JSONDecodeError as e:
            last_error = f"'{path}' contiene JSON inválido: {e}"
        except Exception as e:
            last_error = str(e)

        if attempt < attempts - 1:
            time.sleep(BACKOFF_BASE_S * (3 ** attempt))

    return None, last_error

def extract_parameters(input_data: Optional[dict]) -> dict:
    if not input_data:
        return {}
    try:
        return input_data.get("configuration", {}).get("parameters", {}) or {}
    except AttributeError:
        return {}

def validate_integer(value: Any, name: str, minimum: Optional[int] = None,
                      maximum: Optional[int] = None) -> Tuple[bool, str, Optional[int]]:
    try:
        v = int(value)
    except (TypeError, ValueError):
        return False, f"{name} debe ser un entero.", None
    if minimum is not None and v < minimum:
        return False, f"{name} debe ser >= {minimum} (recibido {v}).", None
    if maximum is not None and v > maximum:
        return False, f"{name} excede el máximo permitido ({maximum}, recibido {v}).", None
    return True, "", v

def validate_float_range(value: Any, name: str, minimum: float = 0.0,
                          maximum: float = 1.0) -> Tuple[bool, str, Optional[float]]:
    try:
        v = float(value)
    except (TypeError, ValueError):
        return False, f"{name} debe ser numérico.", None
    if not (minimum <= v <= maximum):
        return False, f"{name} debe estar en [{minimum}, {maximum}] (recibido {v}).", None
    return True, "", v

def emit_progress(payload: Dict[str, Any]) -> None:
    print(json.dumps(payload, ensure_ascii=False), flush=True)
