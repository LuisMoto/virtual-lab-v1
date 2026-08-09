using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Text;

public class ControladorInterfazSimulacion : MonoBehaviour
{
    [Header("Panel raíz")]
    public GameObject canvasFlotante;

    [Header("Paso 1: Estado de Carga")]
    public GameObject vistaCarga;
    public TextMeshProUGUI textoCarga;

    [Header("Paso 2: Terminal en Vivo")]
    public GameObject vistaTerminal;
    public TextMeshProUGUI textoTerminal;
    public ScrollRect scrollTerminal;

    [Header("Paso 3: Resumen Final")]
    public GameObject vistaResumen;
    public TextMeshProUGUI textoResumen;

    [Header("Referencia al integrador")]
    public ControladorSimulacionVR integrador;

    public int modoDetectorEsperado = 2;

    private bool _recibioAlgunaLinea;
    private int _corridasVistas;
    private LineaProgreso _ultimoProgreso;

    public void EjecutarExperimento()
    {
        if (integrador == null)
        {
            Debug.LogError("[ControladorInterfazSimulacion] Falta asignar 'integrador' en el Inspector.");
            return;
        }

        _recibioAlgunaLinea = false;
        _corridasVistas = 0;
        _ultimoProgreso = null;

        canvasFlotante.SetActive(true);
        MostrarSoloVista(vistaCarga);
        textoCarga.text = "Cargando experimento...";
        textoTerminal.text = "";
        textoResumen.text = "";

        integrador.DetonarSimulacionGrangier();
    }

    public void ManejarProgreso(LineaProgreso p)
    {
        bool esGrangier = integrador.experimento == ControladorSimulacionVR.TipoExperimento.GrangierHwp;

        if (esGrangier && p.modo_detectores != modoDetectorEsperado)
            return;

        if (!_recibioAlgunaLinea)
        {
            _recibioAlgunaLinea = true;
            MostrarSoloVista(vistaTerminal);
            AgregarLineaTerminal("> Simulación iniciada.");
            AgregarLineaTerminal("--------------------------------------------------");
        }

        _corridasVistas++;
        _ultimoProgreso = p;

        AgregarLineaTerminal(FormatearLinea(p));
    }

    public void ManejarFin()
    {
        textoResumen.text = integrador.experimento == ControladorSimulacionVR.TipoExperimento.InterferenciaOndas
            ? ConstruirResumenOndas()
            : ConstruirResumenGrangier();

        MostrarSoloVista(vistaResumen);
    }

    public void ManejarError(string mensaje)
    {
        if (_recibioAlgunaLinea)
            AgregarLineaTerminal($"[ERROR] {mensaje}");

        textoResumen.text = ConstruirEncabezadoError(mensaje);
        MostrarSoloVista(vistaResumen);
    }

    private string FormatearLinea(LineaProgreso p)
    {
        string g2Texto = p.estadistica_insuficiente ? "N/A" : $"{p.g2:F4}";

        if (p.modo_detectores == 3)
            return $"[{_corridasVistas}] ángulo={p.angulo_grados:F1}° NG={p.NG} Nc={p.NGTR} g²={g2Texto}";

        return $"[{_corridasVistas}] ángulo={p.angulo_grados:F1}° Nc={p.NGTR} g²={g2Texto}";
    }

    private string ConstruirResumenGrangier()
    {
        if (_ultimoProgreso == null)
            return ConstruirEncabezadoError("No se recibió ninguna corrida durante la simulación.");

        string titulo = modoDetectorEsperado == 3 ? "3 DETECTORES (TESTIGO)" : "LUZ NATURAL (2 DETECTORES)";
        string g2Texto = _ultimoProgreso.estadistica_insuficiente ? "N/A (estadística insuficiente)" : $"{_ultimoProgreso.g2:F4}";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<b>RESUMEN — {titulo}</b>");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine($"Corridas registradas    : {_corridasVistas}");
        sb.AppendLine($"Última corrida           : #{_ultimoProgreso.num_test}");
        sb.AppendLine($"Ángulo HWP                : {_ultimoProgreso.angulo_grados:F1}°");
        sb.AppendLine($"Coincidencias (Nc)        : {_ultimoProgreso.NGTR}");
        sb.AppendLine($"g² calculado              : {g2Texto}");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine("<i>Nota: son los valores de la última corrida recibida en vivo,");
        sb.AppendLine("no del último ángulo consolidado en el archivo final.</i>");
        return sb.ToString();
    }

    private string ConstruirResumenOndas()
    {
        string ruta = Path.Combine(Application.dataPath, "../Backend/output.json");
        if (!File.Exists(ruta))
            return ConstruirEncabezadoError("No se encontró el archivo de resultados (output.json).");

        JsonSalidaOndas datos;
        try
        {
            datos = JsonUtility.FromJson<JsonSalidaOndas>(File.ReadAllText(ruta));
        }
        catch (System.Exception e)
        {
            return ConstruirEncabezadoError($"No se pudo leer el resultado: {e.Message}");
        }

        if (datos == null || datos.status != "ok" || datos.experimento != "interferencia_ondas")
        {
            string status = datos?.status ?? "desconocido";
            return ConstruirEncabezadoError($"Resultado inesperado (status='{status}').");
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>RESUMEN — INTERFERENCIA DE ONDAS</b>");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine($"Fase (ΔΦ)                 : {datos.resultados.fase_grados}°");
        sb.AppendLine($"Intensidad relativa       : {datos.resultados.intensidad_relativa:F3}");
        sb.AppendLine($"Visibilidad               : {datos.resultados.visibilidad:F3}");
        return sb.ToString();
    }

    private void MostrarSoloVista(GameObject vistaActiva)
    {
        vistaCarga.SetActive(vistaActiva == vistaCarga);
        vistaTerminal.SetActive(vistaActiva == vistaTerminal);
        vistaResumen.SetActive(vistaActiva == vistaResumen);
    }

    private void AgregarLineaTerminal(string linea)
    {
        textoTerminal.text += linea + "\n";
        DesplazarTerminalAlFinal();
    }

    private void DesplazarTerminalAlFinal()
    {
        if (scrollTerminal == null) return;
        Canvas.ForceUpdateCanvases();
        scrollTerminal.verticalNormalizedPosition = 0f;
    }

    private string ConstruirEncabezadoError(string mensaje)
    {
        return $"<color=#FF5555><b>ERROR</b></color>\n{mensaje}";
    }
}

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