import math
import random
import time
import datetime
from typing import Any, Dict, List, Tuple

import utils


MAX_NUM_PULSES = 2_000_000
MAX_RUNS = 200
MAX_ANGLES = 360
MAX_TOTAL_OPERATIONS = 100_000_000

EXPERIMENT = "grangier_hwp"

def hwp_transmittance(angle_deg: float, offset_deg: float = 0.0) -> float:
    theta = math.radians(angle_deg + offset_deg)
    return math.cos(2.0 * theta) ** 2

def generate_angles(start: float, end: float, step: float) -> List[float]:
    angles = []
    current = start
    while current <= end + 1e-9:
        angles.append(round(current, 6))
        current += step
    return angles

def validate_params(params: dict) -> Tuple[bool, str, Dict[str, Any]]:
    ok, msg, num_pulses = utils.validate_integer(params.get("num_pulses", 2000),
                                                 "num_pulses", minimum=1, maximum=MAX_NUM_PULSES)
    if not ok:
        return False, msg, {}
    ok, msg, num_runs = utils.validate_integer(params.get("num_runs", 5),
                                               "num_runs", minimum=1, maximum=MAX_RUNS)
    if not ok:
        return False, msg, {}

    values = {"num_pulses": num_pulses, "num_runs": num_runs}
    for name, default in (("witness_prob", 0.25), ("dark_count_rate", 0.008),
                          ("detector_efficiency", 0.85)):
        ok, msg, v = utils.validate_float_range(params.get(name, default), name)
        if not ok:
            return False, msg, {}
        values[name] = v

    try:
        start_angle = float(params.get("hwp_start_angle_deg", 0.0))
        end_angle = float(params.get("hwp_end_angle_deg", 180.0))
        step_angle = float(params.get("hwp_step_angle_deg", 1.0))
        offset_angle = float(params.get("hwp_offset_angle_deg", 0.0))
        coincidence_window_ns = float(params.get("coincidence_window_ns", 5.0))
        test_duration_us = float(params.get("test_duration_us", 500000.0))
    except (TypeError, ValueError):
        return False, "Uno o más parámetros angulares/temporales tienen un tipo inválido.", {}

    if step_angle <= 0:
        return False, "hwp_step_angle_deg debe ser > 0.", {}
    if start_angle > end_angle:
        return False, "hwp_start_angle_deg no puede ser mayor que hwp_end_angle_deg.", {}

    num_angles = int(round((end_angle - start_angle) / step_angle)) + 1
    if num_angles <= 0:
        return False, "El barrido de ángulos no genera ningún punto.", {}
    if num_angles > MAX_ANGLES:
        return False, f"El barrido genera {num_angles} ángulos, excede el máximo permitido ({MAX_ANGLES}).", {}

    total_operations = num_pulses * num_runs * num_angles * 2
    if total_operations > MAX_TOTAL_OPERATIONS:
        return False, (f"La combinación de parámetros implica {total_operations} operaciones, "
                       f"excede el máximo permitido ({MAX_TOTAL_OPERATIONS}). Reduce num_pulses, "
                       f"num_runs o el rango/paso de ángulos."), {}

    values.update({
        "hwp_start_angle_deg": start_angle,
        "hwp_end_angle_deg": end_angle,
        "hwp_step_angle_deg": step_angle,
        "hwp_offset_angle_deg": offset_angle,
        "coincidence_window_ns": coincidence_window_ns,
        "test_duration_us": test_duration_us,
        "num_angles": num_angles,
    })
    return True, "", values

def simulate_physical_experiment(mode: int, num_pulses: int, rng: random.Random,
                                 witness_prob: float, bs_trans: float,
                                 dark_count_rate: float, detector_efficiency: float) -> dict:
    n_t = 0
    n_r = 0
    n_c = 0
    n_i = 0

    for _ in range(num_pulses):
        click_t = False
        click_r = False
        click_i = False

        if mode == 3:
            if rng.random() < witness_prob:
                if rng.random() < detector_efficiency: click_i = True
                if rng.random() < bs_trans:
                    if rng.random() < detector_efficiency: click_t = True
                else:
                    if rng.random() < detector_efficiency: click_r = True
        else:
            if rng.random() < bs_trans and rng.random() < detector_efficiency:
                click_t = True
            if rng.random() < (1.0 - bs_trans) and rng.random() < detector_efficiency:
                click_r = True

        if rng.random() < dark_count_rate: click_i = True
        if rng.random() < dark_count_rate: click_t = True
        if rng.random() < dark_count_rate: click_r = True

        if click_i: n_i += 1
        if click_t: n_t += 1
        if click_r: n_r += 1
        if click_t and click_r: n_c += 1

    g2 = 0.0
    insufficient = True
    if n_t > 0 and n_r > 0:
        if mode == 3 and n_i > 0:
            g2 = (n_c * n_i) / (n_t * n_r)
            insufficient = False
        elif mode == 2:
            g2 = (n_c * num_pulses) / (n_t * n_r)
            insufficient = False

    return {
        "witness_count": n_i,
        "transmitted_count": n_t,
        "reflected_count": n_r,
        "coincidences": n_c,
        "g2_calculated": round(g2, 6),
        "insufficient_statistics": insufficient
    }

def _emit_run_progress(angle: float, mode: int, num_test: int,
                       run: Dict[str, Any], cfg: Dict[str, Any]) -> None:
    utils.emit_progress({
        "type": "progress",
        "experiment": EXPERIMENT,
        "angle_deg": angle,
        "detector_mode": mode,
        "num_test": num_test,
        "coincidence_window_ns": cfg["coincidence_window_ns"],
        "pulse_period_us": cfg["test_duration_us"],
        "witness_count": run["witness_count"] if mode == 3 else 0,
        "transmitted_count": run["transmitted_count"],
        "reflected_count": run["reflected_count"],
        "triple_coincidence_count": run["coincidences"],
        "g2": run["g2_calculated"],
        "insufficient_statistics": run["insufficient_statistics"],
    })

def run(params: dict) -> Dict[str, Any]:
    start_time = time.time()

    ok, message, cfg = validate_params(params)
    if not ok:
        return utils.build_error_response(message, {"received_params": params},
                                          experiment=EXPERIMENT)

    seed = params.get("seed")
    seed = int(seed) if seed is not None else time.time_ns()
    rng = random.Random(seed % (2**63))

    angles = generate_angles(cfg["hwp_start_angle_deg"], cfg["hwp_end_angle_deg"],
                             cfg["hwp_step_angle_deg"])
    total_runs = len(angles) * cfg["num_runs"] * 2

    utils.emit_progress({
        "type": "start", "experiment": EXPERIMENT,
        "num_angles": len(angles), "total_runs": total_runs,
    })

    sweep = []
    try:
        for angle in angles:
            effective_bs_trans = hwp_transmittance(angle, cfg["hwp_offset_angle_deg"])

            runs_2d = []
            for i in range(cfg["num_runs"]):
                c = simulate_physical_experiment(
                    2, cfg["num_pulses"], rng, witness_prob=cfg["witness_prob"],
                    bs_trans=effective_bs_trans, dark_count_rate=cfg["dark_count_rate"],
                    detector_efficiency=cfg["detector_efficiency"])
                runs_2d.append(c)
                _emit_run_progress(angle, 2, i + 1, c, cfg)

            runs_3d = []
            for i in range(cfg["num_runs"]):
                c = simulate_physical_experiment(
                    3, cfg["num_pulses"], rng, witness_prob=cfg["witness_prob"],
                    bs_trans=effective_bs_trans, dark_count_rate=cfg["dark_count_rate"],
                    detector_efficiency=cfg["detector_efficiency"])
                runs_3d.append(c)
                _emit_run_progress(angle, 3, i + 1, c, cfg)

            sweep.append({
                "angle_deg": angle,
                "effective_bs_trans": round(effective_bs_trans, 6),
                "two_detectors": {"runs": runs_2d},
                "three_detectors": {"runs": runs_3d},
            })
    except ValueError as e:
        utils.emit_progress({"type": "end", "experiment": EXPERIMENT, "status": "error"})
        return utils.build_error_response(f"Error en la simulación: {e}",
                                          experiment=EXPERIMENT)

    last_point = sweep[-1]
    last_run_2d = last_point["two_detectors"]["runs"][-1]
    last_run_3d = last_point["three_detectors"]["runs"][-1]

    results = {
        "coincidence_window_ns": cfg["coincidence_window_ns"],
        "hwp_sweep": [
            {
                "angle_deg": last_point["angle_deg"],
                "two_detectors": {
                    "runs": [
                        {
                            "coincidences": last_run_2d["coincidences"],
                            "g2_calculated": last_run_2d["g2_calculated"],
                            "insufficient_statistics": last_run_2d["insufficient_statistics"],
                        }
                    ]
                },
                "three_detectors": {
                    "runs": [
                        {
                            "coincidences": last_run_3d["coincidences"],
                            "g2_calculated": last_run_3d["g2_calculated"],
                            "insufficient_statistics": last_run_3d["insufficient_statistics"],
                        }
                    ]
                },
            }
        ],
    }
    meta = {
        "execution_time_s": round(time.time() - start_time, 3),
        "seed": seed,
        "config": cfg,
    }

    timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    csv_name = f"results_grangier_hwp_{timestamp}_{seed}.csv"
    lines = ["angle_deg,effective_bs_trans,detector_mode,num_test,coincidence_window_ns,pulse_period_us,"
             "witness_count,transmitted_count,reflected_count,coincidences,"
             "g2_calculated,insufficient_statistics"]
    for point in sweep:
        for i, c in enumerate(point["two_detectors"]["runs"], 1):
            lines.append(f"{point['angle_deg']},{point['effective_bs_trans']},2,{i},"
                         f"{cfg['coincidence_window_ns']},{cfg['test_duration_us']},"
                         f",{c['transmitted_count']},{c['reflected_count']},"
                         f"{c['coincidences']},{c['g2_calculated']},"
                         f"{c['insufficient_statistics']}")
        for i, c in enumerate(point["three_detectors"]["runs"], 1):
            lines.append(f"{point['angle_deg']},{point['effective_bs_trans']},3,{i},"
                         f"{cfg['coincidence_window_ns']},{cfg['test_duration_us']},"
                         f"{c['witness_count']},{c['transmitted_count']},"
                         f"{c['reflected_count']},{c['coincidences']},"
                         f"{c['g2_calculated']},{c['insufficient_statistics']}")
    csv_error = utils.try_write_text(csv_name, "\n".join(lines) + "\n")
    meta["csv_generated"] = csv_name if csv_error is None else None
    if csv_error is not None:
        meta["csv_warning"] = f"No se pudo escribir el CSV: {csv_error}"

    utils.emit_progress({"type": "end", "experiment": EXPERIMENT, "status": "ok"})
    return utils.build_ok_response(EXPERIMENT, results, meta)

if __name__ == "__main__":
    input_data, read_error = utils.read_input_with_retries("input.json")
    if read_error:
        result = utils.build_error_response(read_error, experiment=EXPERIMENT)
    else:
        result = run(utils.extract_parameters(input_data))

    utils.write_json_atomic("output.json", result)
    print(f"[simulator] status={result.get('status')}")
