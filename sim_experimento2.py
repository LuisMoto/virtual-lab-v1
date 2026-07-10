import random
import math
import json
import datetime
import sys

def calcular_g2(Nc, N_total, Nt, Nr):
    if Nt == 0 or Nr == 0:
        return 0.0
    return (Nc * N_total) / (Nt * Nr)

def simular_experimento_grangier(num_detectores, num_pulsos, prob_testigo=0.3, 
                                 bs_trans=0.5, dark_count_rate=0.005, 
                                 detector_efficiency=0.85):
    N_testigo = 0
    Nt = 0
    Nr = 0
    Nc = 0
    
    for _ in range(num_pulsos):
        if num_detectores == 2:
            lambda_poisson = 1.0
            L = math.exp(-lambda_poisson)
            k = 0
            p = 1.0
            while p > L:
                k += 1
                p *= random.random()
            num_fotones = k - 1
            click_T = 0
            click_R = 0
            for _ in range(num_fotones):
                if random.random() < bs_trans:
                    if random.random() < detector_efficiency:
                        click_T += 1
                else:
                    if random.random() < detector_efficiency:
                        click_R += 1
            if click_T > 0: Nt += 1
            if click_R > 0: Nr += 1
            if click_T > 0 and click_R > 0: Nc += 1
                
        elif num_detectores == 3:
            click_testigo = 1 if random.random() < prob_testigo else 0
            click_T_este_pulso = 0
            click_R_este_pulso = 0
            if click_testigo == 1:
                if random.random() < detector_efficiency:
                    N_testigo += 1
                    if random.random() < bs_trans:
                        if random.random() < detector_efficiency:
                            click_T_este_pulso = 1
                    else:
                        if random.random() < detector_efficiency:
                            click_R_este_pulso = 1
                    if random.random() < dark_count_rate:
                        if click_T_este_pulso == 0 and click_R_este_pulso == 0:
                            if random.random() < 0.5: click_T_este_pulso = 1
                            else: click_R_este_pulso = 1
                        elif click_T_este_pulso == 1 and click_R_este_pulso == 0:
                            click_R_este_pulso = 1
                        elif click_R_este_pulso == 1 and click_T_este_pulso == 0:
                            click_T_este_pulso = 1
            if click_T_este_pulso > 0: Nt += 1
            if click_R_este_pulso > 0: Nr += 1
            if click_T_este_pulso > 0 and click_R_este_pulso > 0: Nc += 1

    N_total = num_pulsos if num_detectores == 2 else (N_testigo if N_testigo > 0 else 1)
    g2 = calcular_g2(Nc, N_total, Nt, Nr)
    
    return {
        "escena_detectores": num_detectores,
        "conteo_transmitido_Nt": Nt,
        "conteo_reflejado_Nr": Nr,
        "coincidencias_Nc": Nc,
        "g2_calculado": round(g2, 4)
    }

def calcular_estadisticas(corridas):
    g2_values = [r['g2_calculado'] for r in corridas]
    media = sum(g2_values) / len(g2_values)
    varianza = sum((x - media)**2 for x in g2_values) / len(g2_values)
    return {
        "media": round(media, 4),
        "desv_std": round(math.sqrt(varianza), 4),
        "minimo": round(min(g2_values), 4),
        "maximo": round(max(g2_values), 4)
    }

if __name__ == "__main__":
    config = {
        "prob_testigo": 0.25, 
        "bs_trans": 0.50, 
        "dark_count_rate": 0.008, 
        "detector_efficiency": 0.85
    }
    
    print("Ejecutando simulacion de Grangier...")
    corridas_2d = [simular_experimento_grangier(2, 50000, **config) for _ in range(15)]
    corridas_3d = [simular_experimento_grangier(3, 50000, **config) for _ in range(15)]
    
    stats_2d = calcular_estadisticas(corridas_2d)
    stats_3d = calcular_estadisticas(corridas_3d)
    
    datos_vr = {
        "estadisticas": {"dos_detectores": stats_2d, "tres_detectores": stats_3d},
        "corridas": {"dos_detectores": corridas_2d, "tres_detectores": corridas_3d}
    }
    
    json_file = 'simulacion_grangier_vr.json'
    with open(json_file, 'w', encoding='utf-8') as f:
        json.dump(datos_vr, f, indent=4)
    
    timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    csv_file = f"resultados_grangier_{timestamp}.csv"
    
    json_file_2d = f"simulacion_grangier_2d_{timestamp}.json"
    json_file_3d = f"simulacion_grangier_3d_{timestamp}.json"
    
    datos_2d = {
        "modo": "2 detectores (Luz clásica - Poisson)",
        "estadisticas": stats_2d,
        "corridas": corridas_2d
    }
    
    datos_3d = {
        "modo": "3 detectores (Fotones individuales - Sub-Poisson)",
        "estadisticas": stats_3d,
        "corridas": corridas_3d
    }
    
    with open(json_file_2d, 'w', encoding='utf-8') as f:
        json.dump(datos_2d, f, indent=4)
    
    with open(json_file_3d, 'w', encoding='utf-8') as f:
        json.dump(datos_3d, f, indent=4)
    
    with open(csv_file, "w", encoding="utf-8") as f:
        f.write("Numero_Corrida,Modo_Detectores,Nt,Nr,Nc,g2_calculado\n")
        for i, c in enumerate(corridas_2d, 1):
            f.write(f"{i},2,{c['conteo_transmitido_Nt']},{c['conteo_reflejado_Nr']},{c['coincidencias_Nc']},{c['g2_calculado']}\n")
        for i, c in enumerate(corridas_3d, 1):
            f.write(f"{i+15},3,{c['conteo_transmitido_Nt']},{c['conteo_reflejado_Nr']},{c['coincidencias_Nc']},{c['g2_calculado']}\n")

    print(f"Generado: {json_file_2d}")
    print(f"Generado: {json_file_3d}")
    print(f"Generado: {csv_file}")
    print(f"2D: g(2) = {stats_2d['media']} +/- {stats_2d['desv_std']}")
    print(f"3D: g(2) = {stats_3d['media']} +/- {stats_3d['desv_std']}")
