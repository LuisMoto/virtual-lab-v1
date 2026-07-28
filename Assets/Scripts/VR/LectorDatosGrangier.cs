using UnityEngine;
using System.IO;
using TMPro;

// Estructuras que calcan exactamente el output.json liviano de simulator.py
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
}

public class LectorDatosGrangier : MonoBehaviour
{
    [Header("Pantallas Virtuales (World Space)")]
    public TextMeshProUGUI panelCoincidencias;
    public TextMeshProUGUI panelG2;

    private string rutaArchivo;

    void Start()
    {
        // El JSON vive en Backend/, no en la raíz del proyecto
        rutaArchivo = Path.Combine(Application.dataPath, "../Backend/output.json");
        ActualizarPantallasFlotantes(); // muestra el último resultado guardado, si existe
    }

    /// <summary>
    /// Lectura del estado FINAL. Conectar al evento OnSimulacionCompletada
    /// del ControladorSimulacionVR (se dispara una sola vez, al terminar Python).
    /// </summary>
    public void ActualizarPantallasFlotantes()
    {
        Debug.Log($"[DEBUG] Buscando JSON en: {rutaArchivo}");

        if (!File.Exists(rutaArchivo))
        {
            Debug.LogError($"Aún no hay simulación. No se encontró: {rutaArchivo}");
            return;
        }

        string contenidoJson = File.ReadAllText(rutaArchivo);
        Debug.Log($"[DEBUG] JSON crudo: {contenidoJson}");

        JsonSalida datos = JsonUtility.FromJson<JsonSalida>(contenidoJson);
        Debug.Log($"[DEBUG] status='{datos.status}' | barrido_hwp.Length={datos.resultados?.barrido_hwp?.Length ?? -1}");

        bool tieneCorridas = datos.status == "ok"
            && datos.resultados.barrido_hwp.Length > 0
            && datos.resultados.barrido_hwp[0].tres_detectores.corridas.Length > 0;

        if (!tieneCorridas)
        {
            Debug.LogWarning("El JSON fue leído, pero el status es error, está vacío, o no trae corridas.");
            return;
        }

        Corrida corridaActual = datos.resultados.barrido_hwp[0].tres_detectores.corridas[0];
        panelCoincidencias.text = $"Coincidencias (Nc): {corridaActual.coincidencias_Nc}";
        panelG2.text = $"Valor g(2): {corridaActual.g2_calculado:F3}";
        Debug.Log("Datos cuánticos leídos e inyectados con éxito.");
    }

    /// <summary>
    /// Lectura EN VIVO. Conectar al evento OnProgresoRecibido del
    /// ControladorSimulacionVR (se dispara muchas veces mientras Python corre).
    /// </summary>
    public void MostrarProgresoEnVivo(LineaProgreso p)
    {
        if (p.modo_detectores != 3) return; // esta escena muestra el modo de 3 detectores

        panelCoincidencias.text = $"Nc: {p.NGTR}  (ángulo {p.angulo_grados}°, corrida {p.num_test})";
        panelG2.text = $"g(2): {p.g2:F3}";
    }
}