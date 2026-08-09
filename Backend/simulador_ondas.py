import math
from typing import Any, Dict, Tuple

import utils

EXPERIMENTO = "interferencia_ondas"

VISIBILIDAD_FIJA_MVP = 0.998

def validar_params(params: dict) -> Tuple[bool, str, Dict[str, Any]]:
    try:
        fase_grados = float(params.get("fase_grados", 45.0))
    except (TypeError, ValueError):
        return False, "fase_grados debe ser numérico.", {}

    if not math.isfinite(fase_grados):
        return False, "fase_grados debe ser un número finito.", {}

    return True, "", {"fase_grados": fase_grados}

def ejecutar(params: dict) -> dict:
    ok, mensaje, cfg = validar_params(params)
    if not ok:
        return utils.construir_respuesta_error(mensaje, {"params_recibidos": params},
                                                 experimento=EXPERIMENTO)

    fase_radianes = math.radians(cfg["fase_grados"])
    intensidad = math.cos(fase_radianes / 2.0) ** 2

    resultados = {
        "fase_grados": cfg["fase_grados"],
        "intensidad_relativa": round(intensidad, 6),
        "visibilidad": VISIBILIDAD_FIJA_MVP,
    }

    return utils.construir_respuesta_ok(EXPERIMENTO, resultados)

if __name__ == "__main__":
    datos_entrada, error_lectura = utils.leer_input_con_reintentos("input.json")
    if error_lectura:
        resultado = utils.construir_respuesta_error(error_lectura, experimento=EXPERIMENTO)
    else:
        resultado = ejecutar(utils.extraer_parametros(datos_entrada))

    utils.escribir_json_atomico("output.json", resultado)
    print(f"[simulador_ondas] status={resultado.get('status')}")