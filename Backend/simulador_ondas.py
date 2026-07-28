import math


def ejecutar(params: dict) -> dict:
    # Parámetros de prueba (después puedes conectarlos a tu input.json)
    fase_grados = float(params.get("fase_grados", 45.0))
    fase_radianes = math.radians(fase_grados)

    # Ecuación de interferencia clásica: I = I0 * cos^2(fase / 2)
    intensidad = math.cos(fase_radianes / 2.0) ** 2
    visibilidad = 0.998  # Dato de prueba

    # Retornamos la estructura exacta que C# va a leer
    return {
        "status": "ok",
        "experimento": "interferencia_ondas",
        "resultados": {
            "fase_grados": fase_grados,
            "intensidad_relativa": round(intensidad, 6),
            "visibilidad": visibilidad
        }
    }