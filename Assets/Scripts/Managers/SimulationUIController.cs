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
    [SerializeField] private GameObject floatingCanvas;

    [Header("Step 1: Loading state")]
    [FormerlySerializedAs("vistaCarga")]
    [SerializeField] private GameObject loadingView;
    [FormerlySerializedAs("textoCarga")]
    [SerializeField] private TextMeshProUGUI loadingText;

    [Header("Step 2: Live terminal")]
    [FormerlySerializedAs("vistaTerminal")]
    [SerializeField] private GameObject terminalView;
    [FormerlySerializedAs("textoTerminal")]
    [SerializeField] private TextMeshProUGUI terminalText;
    [FormerlySerializedAs("scrollTerminal")]
    [SerializeField] private ScrollRect terminalScroll;

    [Header("Step 3: Final summary")]
    [FormerlySerializedAs("vistaResumen")]
    [SerializeField] private GameObject summaryView;
    [FormerlySerializedAs("textoResumen")]
    [SerializeField] private TextMeshProUGUI summaryText;

    [Header("Controller reference")]
    [FormerlySerializedAs("integrador")]
    [SerializeField] private SimulationControllerVR controller;

    [FormerlySerializedAs("modoDetectorEsperado")]
    [SerializeField] private int expectedDetectorMode = 2;

    private bool _receivedAnyLine;
    private int _runsSeen;
    private ProgressLine _lastProgress;

    /// <summary>
    /// Shows the loading view and delegates the run to the configured <see cref="SimulationControllerVR"/>.
    /// </summary>
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

    /// <summary>
    /// Appends a live progress line received from <see cref="SimulationControllerVR.OnProgressReceived"/>.
    /// </summary>
    public void HandleProgress(ProgressLine p)
    {
        bool isGrangier = controller.Experiment == SimulationControllerVR.ExperimentType.GrangierHwp;

        if (isGrangier && p.detectorMode != expectedDetectorMode)
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

    /// <summary>
    /// Builds and shows the final summary once the simulation completes successfully.
    /// </summary>
    public void HandleCompletion()
    {
        summaryText.text = controller.Experiment == SimulationControllerVR.ExperimentType.WaveInterference
            ? BuildWaveSummary()
            : BuildGrangierSummary();

        ShowOnlyView(summaryView);
    }

    /// <summary>
    /// Shows an error summary when the simulation fails to launch or reports an internal error.
    /// </summary>
    public void HandleError(string message)
    {
        if (_receivedAnyLine)
            AppendTerminalLine($"[ERROR] {message}");

        summaryText.text = BuildErrorHeader(message);
        ShowOnlyView(summaryView);
    }

    private string FormatLine(ProgressLine p)
    {
        string g2Text = p.insufficientStatistics ? "N/A" : $"{p.g2:F4}";

        if (p.detectorMode == 3)
            return $"[{_runsSeen}] ángulo={p.angleDeg:F1}° NG={p.witnessCount} Nc={p.tripleCoincidenceCount} g²={g2Text}";

        return $"[{_runsSeen}] ángulo={p.angleDeg:F1}° Nc={p.tripleCoincidenceCount} g²={g2Text}";
    }

    private string BuildGrangierSummary()
    {
        if (_lastProgress == null)
            return BuildErrorHeader("No se recibió ninguna corrida durante la simulación.");

        string title = expectedDetectorMode == 3 ? "3 DETECTORES (TESTIGO)" : "LUZ NATURAL (2 DETECTORES)";
        string g2Text = _lastProgress.insufficientStatistics ? "N/A (estadística insuficiente)" : $"{_lastProgress.g2:F4}";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<b>RESUMEN — {title}</b>");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine($"Corridas registradas    : {_runsSeen}");
        sb.AppendLine($"Última corrida           : #{_lastProgress.numTest}");
        sb.AppendLine($"Ángulo HWP                : {_lastProgress.angleDeg:F1}°");
        sb.AppendLine($"Coincidencias (Nc)        : {_lastProgress.tripleCoincidenceCount}");
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

        WaveOutputWire wire;
        try
        {
            wire = JsonUtility.FromJson<WaveOutputWire>(File.ReadAllText(path));
        }
        catch (System.Exception e)
        {
            return BuildErrorHeader($"No se pudo leer el resultado: {e.Message}");
        }

        WaveOutput data = WaveOutput.FromWire(wire);

        if (data == null || data.status != "ok" || data.experiment != "wave_interference")
        {
            string status = data?.status ?? "desconocido";
            return BuildErrorHeader($"Resultado inesperado (status='{status}').");
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>RESUMEN — INTERFERENCIA DE ONDAS</b>");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine($"Fase (ΔΦ)                 : {data.results.phaseDeg}°");
        sb.AppendLine($"Intensidad relativa       : {data.results.relativeIntensity:F3}");
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

/// <summary>
/// Public, camelCase wave-interference output DTO consumed by the rest of the Unity codebase.
/// Built from <see cref="WaveOutputWire"/>, which mirrors Python's snake_case JSON exactly.
/// </summary>
[System.Serializable]
public class WaveOutput
{
    public string status;
    public string experiment;
    public WaveResults results;

    /// <summary>
    /// Maps a wire-format wave output (Python snake_case) to the public camelCase DTO.
    /// </summary>
    public static WaveOutput FromWire(WaveOutputWire wire)
    {
        if (wire == null) return null;

        return new WaveOutput
        {
            status = wire.status,
            experiment = wire.experiment,
            results = WaveResults.FromWire(wire.results)
        };
    }
}
[System.Serializable]
public class WaveResults
{
    public float phaseDeg;
    public float relativeIntensity;
    public float visibility;

    public static WaveResults FromWire(WaveResultsWire wire)
    {
        if (wire == null) return null;

        return new WaveResults
        {
            phaseDeg = wire.phase_deg,
            relativeIntensity = wire.relative_intensity,
            visibility = wire.visibility
        };
    }
}

/// <summary>
/// Wire-format wave-interference output matching Python's exact snake_case JSON keys.
/// Used only as the <see cref="JsonUtility.FromJson"/> deserialization target; not consumed directly.
/// </summary>
[System.Serializable]
public class WaveOutputWire
{
    public string status;
    public string experiment;
    public WaveResultsWire results;
}
[System.Serializable]
public class WaveResultsWire
{
    public float phase_deg;
    public float relative_intensity;
    public float visibility;
}
