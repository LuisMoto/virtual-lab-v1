import sys
import os
import traceback

import utils
import simulator

OUTPUT_PATH = "output.json"
INPUT_PATH = "input.json"

EXPERIMENTOS = {
    "grangier_hwp": simulator,
}


def main() -> int:
    if len(sys.argv) < 2:
        resultado = utils.construir_respuesta_error(
            f"Falta el argumento de experimento. Uso: python main.py <{'|'.join(EXPERIMENTOS)}>"
        )
        utils.escribir_json_atomico(OUTPUT_PATH, resultado)
        return 1

    nombre_experimento = sys.argv[1].strip().lower()
    modulo = EXPERIMENTOS.get(nombre_experimento)
    if modulo is None:
        resultado = utils.construir_respuesta_error(
            f"Experimento desconocido: '{nombre_experimento}'. "
            f"Válidos: {', '.join(EXPERIMENTOS)}."
        )
        utils.escribir_json_atomico(OUTPUT_PATH, resultado)
        return 1

    datos_entrada, error_lectura = utils.leer_input_con_reintentos(INPUT_PATH)
    if error_lectura is not None:
        resultado = utils.construir_respuesta_error(
            f"No se pudo leer '{INPUT_PATH}': {error_lectura}",
            experimento=nombre_experimento,
        )
        utils.escribir_json_atomico(OUTPUT_PATH, resultado)
        return 1

    params = utils.extraer_parametros(datos_entrada)

    try:
        resultado = modulo.ejecutar(params)
    except Exception as e:
        resultado = utils.construir_respuesta_error(
            f"Error inesperado ejecutando '{nombre_experimento}': {e}",
            {"traceback": traceback.format_exc()},
            experimento=nombre_experimento,
        )

    try:
        utils.escribir_json_atomico(OUTPUT_PATH, resultado)
    except Exception as e:
        print(f"[ERROR CRÍTICO] No se pudo escribir '{OUTPUT_PATH}': {e}", file=sys.stderr)
        return 1

    return 0 if resultado.get("status") == "ok" else 1


if __name__ == "__main__":
    sys.exit(main())
