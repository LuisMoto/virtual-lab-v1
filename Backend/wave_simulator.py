import math
from typing import Any, Dict, Tuple

import utils

EXPERIMENT = "wave_interference"

FIXED_VISIBILITY_MVP = 0.998

def validate_params(params: dict) -> Tuple[bool, str, Dict[str, Any]]:
    try:
        phase_deg = float(params.get("phase_deg", 45.0))
    except (TypeError, ValueError):
        return False, "phase_deg debe ser numérico.", {}

    if not math.isfinite(phase_deg):
        return False, "phase_deg debe ser un número finito.", {}

    return True, "", {"phase_deg": phase_deg}

def run(params: dict) -> dict:
    ok, message, cfg = validate_params(params)
    if not ok:
        return utils.build_error_response(message, {"received_params": params},
                                          experiment=EXPERIMENT)

    phase_radians = math.radians(cfg["phase_deg"])
    intensity = math.cos(phase_radians / 2.0) ** 2

    results = {
        "phase_deg": cfg["phase_deg"],
        "relative_intensity": round(intensity, 6),
        "visibility": FIXED_VISIBILITY_MVP,
    }

    return utils.build_ok_response(EXPERIMENT, results)

if __name__ == "__main__":
    input_data, read_error = utils.read_input_with_retries("input.json")
    if read_error:
        result = utils.build_error_response(read_error, experiment=EXPERIMENT)
    else:
        result = run(utils.extract_parameters(input_data))

    utils.write_json_atomic("output.json", result)
    print(f"[wave_simulator] status={result.get('status')}")
