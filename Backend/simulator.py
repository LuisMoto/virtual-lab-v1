import math
import random
import time
import datetime
from typing import Any, Dict, List, Tuple

import utils

MAX_NUM_PULSOS = 2_000_000
MAX_CORRIDAS = 200
MAX_ANGULOS = 360
MAX_OPERACIONES_TOTALES = 100_000_000

EXPERIMENTO = "grangier_hwp"


def transmitancia_hwp(angulo_grados: float, offset_grados: float = 0.0) -> float:
    theta = math.radians(angulo_grados + offset_grados)
    return math.cos(2.0 * theta) ** 2


def generar_angulos(inicio: float, fin: float, paso: float) -> List[float]:
    angulos = []
    actual = inicio
    while actual <= fin + 1e-9:
        angulos.append(round(actual, 6))
        actual += paso
    return angulos


def validar_params(params: dict) -> Tuple[bool, str, Dict[str, Any]]:
    ok, msg, num_pulsos = utils.validar_entero(params.get("num_pulsos", 2000),
                                                "num_pulsos", minimo=1, maximo=MAX_NUM_PULSOS)
    if not ok:
        return False, msg, {}
    ok, msg, num_corridas = utils.validar_entero(params.get("num_corridas", 5),
                                                  "num_corridas", minimo=1, maximo=MAX_CORRIDAS)
    if not ok:
        return False, msg, {}

    valores = {"num_pulsos": num_pulsos, "num_corridas": num_corridas}
    for nombre, default in (("prob_testigo", 0.25), ("dark_count_rate", 0.008),
                             ("detector_efficiency", 0.85)):
        ok, msg, v = utils.validar_flotante_rango(params.get(nombre, default), nombre)
        if not ok:
            return False, msg, {}
        valores[nombre] = v

    try:
        angulo_inicio = float(params.get("angulo_hwp_inicio_grados", 0.0))
        angulo_fin = float(params.get("angulo_hwp_fin_grados", 180.0))
        angulo_paso = float(params.get("angulo_hwp_paso_grados", 1.0))
        angulo_offset = float(params.get("angulo_hwp_offset_grados", 0.0))
        coincidencia_ventana_ns = float(params.get("coincidencia_ventana_ns", 5.0))
        duracion_prueba_us = float(params.get("duracion_prueba_us", 500000.0))
    except (TypeError, ValueError):
        return False, "Uno o más parámetros angulares/temporales tienen un tipo inválido.", {}

    if angulo_paso <= 0:
        return False, "angulo_hwp_paso_grados debe ser > 0.", {}
    if angulo_inicio > angulo_fin:
        return False, "angulo_hwp_inicio_grados no puede ser mayor que angulo_hwp_fin_grados.", {}

    num_angulos = int((angulo_fin - angulo_inicio) / angulo_paso) + 1
    if num_angulos <= 0:
        return False, "El barrido de ángulos no genera ningún punto.", {}
    if num_angulos > MAX_ANGULOS:
        return False, f"El barrido genera {num_angulos} ángulos, excede el máximo permitido ({MAX_ANGULOS}).", {}

    total_operaciones = num_pulsos * num_corridas * num_angulos * 2
    if total_operaciones > MAX_OPERACIONES_TOTALES:
        return False, (f"La combinación de parámetros implica {total_operaciones} operaciones, "
                        f"excede el máximo permitido ({MAX_OPERACIONES_TOTALES}). Reduce num_pulsos, "
                        f"num_corridas o el rango/paso de ángulos."), {}

    valores.update({
        "angulo_hwp_inicio_grados": angulo_inicio,
        "angulo_hwp_fin_grados": angulo_fin,
        "angulo_hwp_paso_grados": angulo_paso,
        "angulo_hwp_offset_grados": angulo_offset,
        "coincidencia_ventana_ns": coincidencia_ventana_ns,
        "duracion_prueba_us": duracion_prueba_us,
        "num_angulos": num_angulos,
    })
    return True, "", valores


def _emitir_progreso_corrida(angulo: float, modo: int, num_test: int,
                              corrida: Dict[str, Any], cfg: Dict[str, Any]) -> None:
    utils.emitir_progreso({
        "tipo": "progreso",
        "experimento": EXPERIMENTO,
        "angulo_grados": angulo,
        "modo_detectores": modo,
        "num_test": num_test,
        "CoinWin_ns": cfg["coincidencia_ventana_ns"],
        "Tp_us": cfg["duracion_prueba_us"],
        "NG": corrida["conteo_testigo_Ni"] if modo == 3 else None,
        "NGT": corrida["conteo_transmitido_Nt"],
        "NGR": corrida["conteo_reflejado_Nr"],
        "NGTR": corrida["coincidencias_Nc"],
        "g2": corrida["g2_calculado"],
    })


def ejecutar(params: dict) -> Dict[str, Any]:
    inicio = time.time()

    ok, mensaje, cfg = validar_params(params)
    if not ok:
        return utils.construir_respuesta_error(mensaje, {"params_recibidos": params},
                                                 experimento=EXPERIMENTO)

    semilla = params.get("seed")
    semilla = int(semilla) if semilla is not None else time.time_ns()
    rng = random.Random(semilla % (2**63))

    angulos = generar_angulos(cfg["angulo_hwp_inicio_grados"], cfg["angulo_hwp_fin_grados"],
                               cfg["angulo_hwp_paso_grados"])
    total_corridas = len(angulos) * cfg["num_corridas"] * 2

    utils.emitir_progreso({
        "tipo": "inicio", "experimento": EXPERIMENTO,
        "num_angulos": len(angulos), "total_corridas": total_corridas,
    })

    barrido = []
    try:
        for angulo in angulos:
            bs_trans_efectivo = transmitancia_hwp(angulo, cfg["angulo_hwp_offset_grados"])

            corridas_2d = []
            for i in range(cfg["num_corridas"]):
                c = simulacion_grangier.simular_experimento_grangier(
                    2, cfg["num_pulsos"], rng, prob_testigo=cfg["prob_testigo"],
                    bs_trans=bs_trans_efectivo, dark_count_rate=cfg["dark_count_rate"],
                    detector_efficiency=cfg["detector_efficiency"])
                corridas_2d.append(c)
                _emitir_progreso_corrida(angulo, 2, i + 1, c, cfg)

            corridas_3d = []
            for i in range(cfg["num_corridas"]):
                c = simulacion_grangier.simular_experimento_grangier(
                    3, cfg["num_pulsos"], rng, prob_testigo=cfg["prob_testigo"],
                    bs_trans=bs_trans_efectivo, dark_count_rate=cfg["dark_count_rate"],
                    detector_efficiency=cfg["detector_efficiency"])
                corridas_3d.append(c)
                _emitir_progreso_corrida(angulo, 3, i + 1, c, cfg)

            barrido.append({
                "angulo_grados": angulo,
                "bs_trans_efectivo": round(bs_trans_efectivo, 6),
                "dos_detectores": {"corridas": corridas_2d},
                "tres_detectores": {"corridas": corridas_3d},
            })
    except ValueError as e:
        utils.emitir_progreso({"tipo": "fin", "experimento": EXPERIMENTO, "status": "error"})
        return utils.construir_respuesta_error(f"Error en la simulación: {e}",
                                                 experimento=EXPERIMENTO)

    resultados = {
        "coincidencia_ventana_ns": cfg["coincidencia_ventana_ns"],
        "duracion_prueba_us": cfg["duracion_prueba_us"],
        "descripcion": ("Barrido de lamina de media onda (HWP) antes del beam splitter. "
                         "bs_trans_efectivo = cos^2(2*(angulo+offset)). Datos crudos por "
                         "corrida unicamente, sin promedios ni varianza calculados aqui."),
        "barrido_hwp": barrido,
    }
    meta = {
        "tiempo_ejecucion_s": round(time.time() - inicio, 3),
        "semilla": semilla,
        "config": cfg,
    }

    timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    nombre_csv = f"resultados_grangier_hwp_{timestamp}_{semilla}.csv"
    lineas = ["Angulo_grados,Bs_trans_efectivo,Modo_Detectores,NumTest,CoinWin_ns,Tp_us,"
              "NG_testigo,NGT_transmitido,NGR_reflejado,NGTR_coincidencia_triple,"
              "g2_calculado,estadistica_insuficiente"]
    for punto in barrido:
        for i, c in enumerate(punto["dos_detectores"]["corridas"], 1):
            lineas.append(f"{punto['angulo_grados']},{punto['bs_trans_efectivo']},2,{i},"
                           f"{cfg['coincidencia_ventana_ns']},{cfg['duracion_prueba_us']},"
                           f",{c['conteo_transmitido_Nt']},{c['conteo_reflejado_Nr']},"
                           f"{c['coincidencias_Nc']},{c['g2_calculado']},"
                           f"{c['estadistica_insuficiente']}")
        for i, c in enumerate(punto["tres_detectores"]["corridas"], 1):
            lineas.append(f"{punto['angulo_grados']},{punto['bs_trans_efectivo']},3,{i},"
                           f"{cfg['coincidencia_ventana_ns']},{cfg['duracion_prueba_us']},"
                           f"{c['conteo_testigo_Ni']},{c['conteo_transmitido_Nt']},"
                           f"{c['conteo_reflejado_Nr']},{c['coincidencias_Nc']},"
                           f"{c['g2_calculado']},{c['estadistica_insuficiente']}")
    error_csv = utils.intentar_escribir_texto(nombre_csv, "\n".join(lineas) + "\n")
    meta["csv_generado"] = nombre_csv if error_csv is None else None
    if error_csv is not None:
        meta["advertencia_csv"] = f"No se pudo escribir el CSV: {error_csv}"

    utils.emitir_progreso({"tipo": "fin", "experimento": EXPERIMENTO, "status": "ok"})
    return utils.construir_respuesta_ok(EXPERIMENTO, resultados, meta)


if __name__ == "__main__":
    datos_entrada, error_lectura = utils.leer_input_con_reintentos("input.json")
    if error_lectura:
        resultado = utils.construir_respuesta_error(error_lectura, experimento=EXPERIMENTO)
    else:
        resultado = ejecutar(utils.extraer_parametros(datos_entrada))
    utils.escribir_json_atomico("output.json", resultado)
    print(f"[lamina_mediaonda] status={resultado['status']}")
