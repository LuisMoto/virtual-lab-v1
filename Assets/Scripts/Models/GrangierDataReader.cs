using UnityEngine;
using UnityEngine.Serialization;
using System.IO;
using TMPro;

/// <summary>
/// Public, camelCase Grangier output DTO consumed by the rest of the Unity codebase.
/// Built from <see cref="GrangierOutputWire"/>, which mirrors Python's snake_case JSON exactly.
/// </summary>
[System.Serializable]
public class GrangierOutput
{
    public string status;
    public string experiment;
    public GrangierResults results;

    /// <summary>
    /// Maps a wire-format Grangier output (Python snake_case) to the public camelCase DTO.
    /// </summary>
    public static GrangierOutput FromWire(GrangierOutputWire wire)
    {
        if (wire == null) return null;

        return new GrangierOutput
        {
            status = wire.status,
            experiment = wire.experiment,
            results = GrangierResults.FromWire(wire.results)
        };
    }
}
[System.Serializable]
public class GrangierResults
{
    public float coincidenceWindowNs;
    public SweepPoint[] hwpSweep;

    public static GrangierResults FromWire(GrangierResultsWire wire)
    {
        if (wire == null) return null;

        SweepPoint[] hwpSweep = null;
        if (wire.hwp_sweep != null)
        {
            hwpSweep = new SweepPoint[wire.hwp_sweep.Length];
            for (int i = 0; i < wire.hwp_sweep.Length; i++)
                hwpSweep[i] = SweepPoint.FromWire(wire.hwp_sweep[i]);
        }

        return new GrangierResults
        {
            coincidenceWindowNs = wire.coincidence_window_ns,
            hwpSweep = hwpSweep
        };
    }
}
[System.Serializable]
public class SweepPoint
{
    public float angleDeg;
    public DetectorRuns twoDetectors;
    public DetectorRuns threeDetectors;

    public static SweepPoint FromWire(SweepPointWire wire)
    {
        if (wire == null) return null;

        return new SweepPoint
        {
            angleDeg = wire.angle_deg,
            twoDetectors = DetectorRuns.FromWire(wire.two_detectors),
            threeDetectors = DetectorRuns.FromWire(wire.three_detectors)
        };
    }
}
[System.Serializable]
public class DetectorRuns
{
    public Run[] runs;

    public static DetectorRuns FromWire(DetectorRunsWire wire)
    {
        if (wire == null) return null;

        Run[] runs = null;
        if (wire.runs != null)
        {
            runs = new Run[wire.runs.Length];
            for (int i = 0; i < wire.runs.Length; i++)
                runs[i] = Run.FromWire(wire.runs[i]);
        }

        return new DetectorRuns { runs = runs };
    }
}
[System.Serializable]
public class Run
{
    public int coincidences;
    public float g2Calculated;
    public bool insufficientStatistics;

    public static Run FromWire(RunWire wire)
    {
        if (wire == null) return null;

        return new Run
        {
            coincidences = wire.coincidences,
            g2Calculated = wire.g2_calculated,
            insufficientStatistics = wire.insufficient_statistics
        };
    }
}

/// <summary>
/// Wire-format Grangier output matching Python's exact snake_case JSON keys.
/// Used only as the <see cref="JsonUtility.FromJson"/> deserialization target; not consumed directly.
/// </summary>
[System.Serializable]
public class GrangierOutputWire
{
    public string status;
    public string experiment;
    public GrangierResultsWire results;
}
[System.Serializable]
public class GrangierResultsWire
{
    public float coincidence_window_ns;
    public SweepPointWire[] hwp_sweep;
}
[System.Serializable]
public class SweepPointWire
{
    public float angle_deg;
    public DetectorRunsWire two_detectors;
    public DetectorRunsWire three_detectors;
}
[System.Serializable]
public class DetectorRunsWire
{
    public RunWire[] runs;
}
[System.Serializable]
public class RunWire
{
    public int coincidences;
    public float g2_calculated;
    public bool insufficient_statistics;
}

public class GrangierDataReader : MonoBehaviour
{
    [Header("Virtual displays (World Space)")]
    [FormerlySerializedAs("panelCoincidencias")]
    [SerializeField] private TextMeshProUGUI coincidencesPanel;
    [FormerlySerializedAs("panelG2")]
    [SerializeField] private TextMeshProUGUI g2Panel;

    [Header("Detector mode for this scene (2 or 3)")]
    [FormerlySerializedAs("modoEsperado")]
    [SerializeField] private int expectedMode = 3;

    private string _filePath;
    private SimulationControllerVR _controller;
    private bool _warnedMissingPanels;

    void Start()
    {
        _filePath = Path.Combine(Application.dataPath, "../Backend/output.json");
        UpdateFloatingPanels();

        // Subscribe to the same live-progress stream that already drives SimulationUIController,
        // so these world-space panels refresh in real time instead of only once at Start().
        // SimulationControllerVR lives on this same GameObject, so no Inspector wiring is needed.
        _controller = GetComponent<SimulationControllerVR>();
        if (_controller != null)
            _controller.OnProgressReceived.AddListener(ShowLiveProgress);
        else
            Debug.LogWarning("[GrangierDataReader] No SimulationControllerVR found on this GameObject — live panel updates are disabled.");
    }

    void OnDestroy()
    {
        if (_controller != null)
            _controller.OnProgressReceived.RemoveListener(ShowLiveProgress);
    }

    /// <summary>
    /// True when both world-space panels are assigned in the Inspector. Logs a warning once if not,
    /// instead of throwing a NullReferenceException the first time a panel update is attempted.
    /// </summary>
    private bool PanelsReady()
    {
        if (coincidencesPanel != null && g2Panel != null) return true;

        if (!_warnedMissingPanels)
        {
            _warnedMissingPanels = true;
            Debug.LogWarning("[GrangierDataReader] 'coincidencesPanel'/'g2Panel' are not assigned in the Inspector on this GameObject — the floating world-space panels won't update. Assign both TextMeshProUGUI references on the GrangierDataReader component in this scene.");
        }
        return false;
    }

    /// <summary>
    /// Reads the last saved output.json and refreshes the world-space panels with its final values.
    /// </summary>
    public void UpdateFloatingPanels()
    {
        if (!File.Exists(_filePath))
        {
            Debug.LogError($"Not found: {_filePath}");
            return;
        }

        string jsonContent = File.ReadAllText(_filePath);
        GrangierOutputWire wire = JsonUtility.FromJson<GrangierOutputWire>(jsonContent);
        GrangierOutput data = GrangierOutput.FromWire(wire);

        if (data == null || data.status != "ok" || data.results == null
            || data.results.hwpSweep == null || data.results.hwpSweep.Length == 0)
        {
            Debug.LogWarning("The JSON does not contain a valid result.");
            return;
        }

        SweepPoint point = data.results.hwpSweep[0];
        DetectorRuns detectors = expectedMode == 2 ? point.twoDetectors : point.threeDetectors;

        if (detectors == null || detectors.runs == null || detectors.runs.Length == 0)
        {
            Debug.LogWarning($"No runs for mode {expectedMode} in the JSON.");
            return;
        }

        if (!PanelsReady()) return;

        Run currentRun = detectors.runs[0];
        coincidencesPanel.text = $"Coincidencias (Nc): {currentRun.coincidences}";
        g2Panel.text = currentRun.insufficientStatistics
            ? "Valor g(2): N/A (estadística insuficiente)"
            : $"Valor g(2): {currentRun.g2Calculated:F4}";
    }

    /// <summary>
    /// Updates the world-space panels in real time from a streamed <see cref="ProgressLine"/>.
    /// </summary>
    public void ShowLiveProgress(ProgressLine p)
    {
        if (p.detectorMode != expectedMode) return;
        if (!PanelsReady()) return;

        coincidencesPanel.text = $"Nc: {p.tripleCoincidenceCount}  (ángulo {p.angleDeg}°, corrida {p.numTest})";
        g2Panel.text = p.insufficientStatistics
            ? "g(2): N/A (estadística insuficiente)"
            : $"g(2): {p.g2:F4}";
    }
}
