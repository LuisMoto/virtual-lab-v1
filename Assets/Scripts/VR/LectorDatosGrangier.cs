using UnityEngine;
using System.IO;
using TMPro;

[System.Serializable]
public class JsonSalida
{
    public string status;
    public string experimento;
    public Resultados resultados;
}
[System.Serializable]
public class Resultados
{
    public float coincidencia_ventana_ns;
    public PuntoBarrido[] barrido_hwp;
}
[System.Serializable]
public class PuntoBarrido
{
    public float angulo_grados;
    public Detectores dos_detectores;
    public Detectores tres_detectores;
}
[System.Serializable]
public class Detectores
{
    public Corrida[] corridas;
}
[System.Serializable]
public class Corrida
{
    public int coincidencias_Nc;
    public float g2_calculado;
    public bool estadistica_insuficiente;
}

public class LectorDatosGrangier : MonoBehaviour
{
    [Header("Pantallas Virtuales (World Space)")]
    public TextMeshProUGUI panelCoincidencias;
    public TextMeshProUGUI panelG2;

    [Header("Modo de detectores de esta escena (2 o 3)")]
    public int modoEsperado = 3;

    private string rutaArchivo;

    void Start()
    {
        rutaArchivo = Path.Combine(Application.dataPath, "../Backend/output.json");
        ActualizarPantallasFlotantes();
    }

    public void ActualizarPantallasFlotantes()
    {
        if (!File.Exists(rutaArchivo))
        {
            Debug.LogError($"No se encontró: {rutaArchivo}");
            return;
        }

        string contenidoJson = File.ReadAllText(rutaArchivo);
        JsonSalida datos = JsonUtility.FromJson<JsonSalida>(contenidoJson);

        if (datos == null || datos.status != "ok" || datos.resultados == null
            || datos.resultados.barrido_hwp == null || datos.resultados.barrido_hwp.Length == 0)
        {
            Debug.LogWarning("El JSON no trae un resultado válido.");
            return;
        }

        PuntoBarrido punto = datos.resultados.barrido_hwp[0];
        Detectores det = modoEsperado == 2 ? punto.dos_detectores : punto.tres_detectores;

        if (det == null || det.corridas == null || det.corridas.Length == 0)
        {
            Debug.LogWarning($"No hay corridas para modo {modoEsperado} en el JSON.");
            return;
        }

        Corrida corridaActual = det.corridas[0];
        panelCoincidencias.text = $"Coincidencias (Nc): {corridaActual.coincidencias_Nc}";
        panelG2.text = corridaActual.estadistica_insuficiente
            ? "Valor g(2): N/A (estadística insuficiente)"
            : $"Valor g(2): {corridaActual.g2_calculado:F4}";
    }

    public void MostrarProgresoEnVivo(LineaProgreso p)
    {
        if (p.modo_detectores != modoEsperado) return;

        panelCoincidencias.text = $"Nc: {p.NGTR}  (ángulo {p.angulo_grados}°, corrida {p.num_test})";
        panelG2.text = p.estadistica_insuficiente
            ? "g(2): N/A (estadística insuficiente)"
            : $"g(2): {p.g2:F4}";
    }
}