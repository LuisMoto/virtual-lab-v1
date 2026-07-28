using UnityEngine;
using System.IO;
using TMPro;

[System.Serializable]
public class JsonSalidaOndas
{
    public string status;
    public string experimento;
    public ResultadosOndas resultados;
}
[System.Serializable]
public class ResultadosOndas
{
    public float fase_grados;
    public float intensidad_relativa;
    public float visibilidad;
}

public class LectorDatosOndas : MonoBehaviour
{
    [Header("Pantallas Virtuales (Escena 3)")]
    public TextMeshProUGUI panelIntensidad;
    public TextMeshProUGUI panelFase;

    private string rutaArchivo;

    void Start()
    {
        rutaArchivo = Path.Combine(Application.dataPath, "../Backend/output.json");
        ActualizarPantallasFlotantes();
    }

    /// <summary>
    /// Conectar al evento OnSimulacionCompletada del ControladorSimulacionVR
    /// (con TipoExperimento = InterferenciaOndas en esta escena).
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
        JsonSalidaOndas datos = JsonUtility.FromJson<JsonSalidaOndas>(contenidoJson);

        if (datos.status == "ok" && datos.experimento == "interferencia_ondas")
        {
            panelIntensidad.text = $"Intensidad: {datos.resultados.intensidad_relativa:F3}";
            panelFase.text = $"Fase (\u0394\u03A6): {datos.resultados.fase_grados}\u00B0";
            Debug.Log("Datos de ondas leídos e inyectados.");
        }
        else
        {
            Debug.LogWarning($"JSON leído pero no corresponde a 'interferencia_ondas' (llegó experimento='{datos.experimento}', status='{datos.status}'). ¿Corriste el experimento correcto en esta escena?");
        }
    }
}