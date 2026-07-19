import json
import os
import sys
import time
import tempfile
import datetime
from typing import Any, Dict, Optional, Tuple

BACKOFF_BASE_S = 0.05


def escribir_json_atomico(path: str, data: dict) -> None:
    directorio = os.path.dirname(os.path.abspath(path)) or "."
    fd, tmp_path = tempfile.mkstemp(dir=directorio, prefix=".tmp_", suffix=".json")
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


def intentar_escribir_texto(path: str, contenido: str) -> Optional[str]:
    try:
        with open(path, "w", encoding="utf-8", newline="") as f:
            f.write(contenido)
        return None
    except Exception as e:
        return str(e)


def construir_respuesta_ok(experimento: str, resultados: Dict[str, Any],
                            meta: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
    return {
        "status": "ok",
        "experimento": experimento,
        "resultados": resultados,
        "meta": meta or {},
    }


def construir_respuesta_error(mensaje: str, detalles: Any = None,
                               experimento: Optional[str] = None) -> Dict[str, Any]:
    return {
        "status": "error",
        "experimento": experimento,
        "mensaje": mensaje,
        "detalles": detalles or {},
        "timestamp": datetime.datetime.now().isoformat(),
    }


def leer_input_con_reintentos(path: str, intentos: int = 3) -> Tuple[Optional[dict], Optional[str]]:
    if not os.path.exists(path):
        return None, None

    ultimo_error = "desconocido"
    for intento in range(intentos):
        try:
            with open(path, "r", encoding="utf-8") as f:
                return json.load(f), None
        except FileNotFoundError:
            ultimo_error = f"No se encontró el archivo '{path}'."
        except json.JSONDecodeError as e:
            ultimo_error = f"'{path}' contiene JSON inválido: {e}"
        except Exception as e:
            ultimo_error = str(e)

        if intento < intentos - 1:
            time.sleep(BACKOFF_BASE_S * (3 ** intento))

    return None, ultimo_error


def extraer_parametros(datos_entrada: Optional[dict]) -> dict:
    if not datos_entrada:
        return {}
    try:
        return datos_entrada.get("configuracion", {}).get("parametros", {}) or {}
    except AttributeError:
        return {}


def validar_entero(valor: Any, nombre: str, minimo: Optional[int] = None,
                    maximo: Optional[int] = None) -> Tuple[bool, str, Optional[int]]:
    try:
        v = int(valor)
    except (TypeError, ValueError):
        return False, f"{nombre} debe ser un entero.", None
    if minimo is not None and v < minimo:
        return False, f"{nombre} debe ser >= {minimo} (recibido {v}).", None
    if maximo is not None and v > maximo:
        return False, f"{nombre} excede el máximo permitido ({maximo}, recibido {v}).", None
    return True, "", v


def validar_flotante_rango(valor: Any, nombre: str, minimo: float = 0.0,
                            maximo: float = 1.0) -> Tuple[bool, str, Optional[float]]:
    try:
        v = float(valor)
    except (TypeError, ValueError):
        return False, f"{nombre} debe ser numérico.", None
    if not (minimo <= v <= maximo):
        return False, f"{nombre} debe estar en [{minimo}, {maximo}] (recibido {v}).", None
    return True, "", v


def emitir_progreso(payload: Dict[str, Any]) -> None:
    print(json.dumps(payload, ensure_ascii=False), flush=True)
