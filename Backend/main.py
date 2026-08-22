import sys
import os
import traceback

import utils
import simulator
import wave_simulator

OUTPUT_PATH = "output.json"
INPUT_PATH = "input.json"

EXPERIMENTS = {
    "grangier_hwp": simulator,
    "wave_interference": wave_simulator,
}


def main() -> int:
    if len(sys.argv) < 2:
        result = utils.build_error_response(
            f"Falta el argumento de experimento. Uso: python main.py <{'|'.join(EXPERIMENTS)}>"
        )
        utils.write_json_atomic(OUTPUT_PATH, result)
        return 1

    experiment_name = sys.argv[1].strip().lower()
    module = EXPERIMENTS.get(experiment_name)
    if module is None:
        result = utils.build_error_response(
            f"Experimento desconocido: '{experiment_name}'. "
            f"Válidos: {', '.join(EXPERIMENTS)}."
        )
        utils.write_json_atomic(OUTPUT_PATH, result)
        return 1

    input_data, read_error = utils.read_input_with_retries(INPUT_PATH)
    if read_error is not None:
        result = utils.build_error_response(
            f"No se pudo leer '{INPUT_PATH}': {read_error}",
            experiment=experiment_name,
        )
        utils.write_json_atomic(OUTPUT_PATH, result)
        return 1

    params = utils.extract_parameters(input_data)

    try:
        result = module.run(params)
    except Exception as e:
        result = utils.build_error_response(
            f"Error inesperado ejecutando '{experiment_name}': {e}",
            {"traceback": traceback.format_exc()},
            experiment=experiment_name,
        )

    try:
        utils.write_json_atomic(OUTPUT_PATH, result)
    except Exception as e:
        print(f"[ERROR CRÍTICO] No se pudo escribir '{OUTPUT_PATH}': {e}", file=sys.stderr)
        return 1

    return 0 if result.get("status") == "ok" else 1


if __name__ == "__main__":
    sys.exit(main())
