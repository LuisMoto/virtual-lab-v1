using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using System.IO;
using System.Text;

public class SimulationUIController : MonoBehaviour
{
    [Header("Root panel")]
    [FormerlySerializedAs("canvasFlotante")]
    public GameObject floatingCanvas;

    [Header("Step 1: Loading state")]
    [FormerlySerializedAs("vistaCarga")]
    public GameObject loadingView;
    [FormerlySerializedAs("textoCarga")]
    public TextMeshProUGUI loadingText;

    [Header("Step 2: Live terminal")]
    [FormerlySerializedAs("vistaTerminal")]
    public GameObject terminalView;
    [FormerlySerializedAs("textoTerminal")]
    public TextMeshProUGUI terminalText;
    [FormerlySerializedAs("scrollTerminal")]
    public ScrollRect terminalScroll;

    [Header("Step 3: Final summary")]
    [FormerlySerializedAs("vistaResumen")]
    public GameObject summaryView;
    [FormerlySerializedAs("textoResumen")]
    public TextMeshProUGUI summaryText;

    [Header("Controller reference")]
    [FormerlySerializedAs("integrador")]
    public SimulationControllerVR controller;

    [FormerlySerializedAs("modoDetectorEsperado")]
    public int expectedDetectorMode = 2;

    private bool _receivedAnyLine;
    private int _runsSeen;
    private ProgressLine _lastProgress;

    public void RunExperiment()
    {
        if (controller == null)
        {
            Debug.LogError("[SimulationUIController] Missing 'controller' assignment in the Inspector.");
            return;
        }

        _receivedAnyLine = false;
        _runsSeen = 0;
        _lastProgress = null;

        floatingCanvas.SetActive(true);
        ShowOnlyView(loadingView);
        loadingText.text = "Cargando experimento...";
        terminalText.text = "";
        summaryText.text = "";

        controller.RunGrangierSimulation();
    }

    public void HandleProgress(ProgressLine p)
    {
        bool isGrangier = controller.experiment == SimulationControllerVR.ExperimentType.GrangierHwp;

        if (isGrangier && p.detector_mode != expectedDetectorMode)
            return;

        if (!_receivedAnyLine)
        {
            _receivedAnyLine = true;
            ShowOnlyView(terminalView);
            AppendTerminalLine("> Simulación iniciada.");
            AppendTerminalLine("--------------------------------------------------");
        }

        _runsSeen++;
        _lastProgress = p;

        AppendTerminalLine(FormatLine(p));
    }

    public void HandleCompletion()
    {
        summaryText.text = controller.experiment == SimulationControllerVR.ExperimentType.WaveInterference
            ? BuildWaveSummary()
            : BuildGrangierSummary();

        ShowOnlyView(summaryView);
    }

    public void HandleError(string message)
    {
        if (_receivedAnyLine)
            AppendTerminalLine($"[ERROR] {message}");

        summaryText.text = BuildErrorHeader(message);
        ShowOnlyView(summaryView);
    }

    private string FormatLine(ProgressLine p)
    {
        string g2Text = p.insufficient_statistics ? "N/A" : $"{p.g2:F4}";

        if (p.detector_mode == 3)
            return $"[{_runsSeen}] ángulo={p.angle_deg:F1}° NG={p.NG} Nc={p.NGTR} g²={g2Text}";

        return $"[{_runsSeen}] ángulo={p.angle_deg:F1}° Nc={p.NGTR} g²={g2Text}";
    }

    private string BuildGrangierSummary()
    {
        if (_lastProgress == null)
            return BuildErrorHeader("No se recibió ninguna corrida durante la simulación.");

        string title = expectedDetectorMode == 3 ? "3 DETECTORES (TESTIGO)" : "LUZ NATURAL (2 DETECTORES)";
        string g2Text = _lastProgress.insufficient_statistics ? "N/A (estadística insuficiente)" : $"{_lastProgress.g2:F4}";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<b>RESUMEN — {title}</b>");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine($"Corridas registradas    : {_runsSeen}");
        sb.AppendLine($"Última corrida           : #{_lastProgress.num_test}");
        sb.AppendLine($"Ángulo HWP                : {_lastProgress.angle_deg:F1}°");
        sb.AppendLine($"Coincidencias (Nc)        : {_lastProgress.NGTR}");
        sb.AppendLine($"g² calculado              : {g2Text}");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine("<i>Nota: son los valores de la última corrida recibida en vivo,");
        sb.AppendLine("no del último ángulo consolidado en el archivo final.</i>");
        return sb.ToString();
    }

    private string BuildWaveSummary()
    {
        string path = Path.Combine(Application.dataPath, "../Backend/output.json");
        if (!File.Exists(path))
            return BuildErrorHeader("No se encontró el archivo de resultados (output.json).");

        WaveOutput data;
        try
        {
            data = JsonUtility.FromJson<WaveOutput>(File.ReadAllText(path));
        }
        catch (System.Exception e)
        {
            return BuildErrorHeader($"No se pudo leer el resultado: {e.Message}");
        }

        if (data == null || data.status != "ok" || data.experiment != "wave_interference")
        {
            string status = data?.status ?? "desconocido";
            return BuildErrorHeader($"Resultado inesperado (status='{status}').");
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>RESUMEN — INTERFERENCIA DE ONDAS</b>");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine($"Fase (ΔΦ)                 : {data.results.phase_deg}°");
        sb.AppendLine($"Intensidad relativa       : {data.results.relative_intensity:F3}");
        sb.AppendLine($"Visibilidad               : {data.results.visibility:F3}");
        return sb.ToString();
    }

    private void ShowOnlyView(GameObject activeView)
    {
        loadingView.SetActive(activeView == loadingView);
        terminalView.SetActive(activeView == terminalView);
        summaryView.SetActive(activeView == summaryView);
    }

    private void AppendTerminalLine(string line)
    {
        terminalText.text += line + "\n";
        ScrollTerminalToEnd();
    }

    private void ScrollTerminalToEnd()
    {
        if (terminalScroll == null) return;
        Canvas.ForceUpdateCanvases();
        terminalScroll.verticalNormalizedPosition = 0f;
    }

    private string BuildErrorHeader(string message)
    {
        return $"<color=#FF5555><b>ERROR</b></color>\n{message}";
    }
}

[System.Serializable]
public class WaveOutput
{
    public string status;
    public string experiment;
    public WaveResults results;
}
[System.Serializable]
public class WaveResults
{
    public float phase_deg;
    public float relative_intensity;
    public float visibility;
}
