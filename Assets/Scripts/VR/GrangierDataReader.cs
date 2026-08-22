using UnityEngine;
using UnityEngine.Serialization;
using System.IO;
using TMPro;

[System.Serializable]
public class GrangierOutput
{
    public string status;
    public string experiment;
    public GrangierResults results;
}
[System.Serializable]
public class GrangierResults
{
    public float coincidence_window_ns;
    public SweepPoint[] hwp_sweep;
}
[System.Serializable]
public class SweepPoint
{
    public float angle_deg;
    public DetectorRuns two_detectors;
    public DetectorRuns three_detectors;
}
[System.Serializable]
public class DetectorRuns
{
    public Run[] runs;
}
[System.Serializable]
public class Run
{
    public int coincidences_Nc;
    public float g2_calculated;
    public bool insufficient_statistics;
}

public class GrangierDataReader : MonoBehaviour
{
    [Header("Virtual displays (World Space)")]
    [FormerlySerializedAs("panelCoincidencias")]
    public TextMeshProUGUI coincidencesPanel;
    [FormerlySerializedAs("panelG2")]
    public TextMeshProUGUI g2Panel;

    [Header("Detector mode for this scene (2 or 3)")]
    [FormerlySerializedAs("modoEsperado")]
    public int expectedMode = 3;

    private string filePath;

    void Start()
    {
        filePath = Path.Combine(Application.dataPath, "../Backend/output.json");
        UpdateFloatingPanels();
    }

    public void UpdateFloatingPanels()
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"Not found: {filePath}");
            return;
        }

        string jsonContent = File.ReadAllText(filePath);
        GrangierOutput data = JsonUtility.FromJson<GrangierOutput>(jsonContent);

        if (data == null || data.status != "ok" || data.results == null
            || data.results.hwp_sweep == null || data.results.hwp_sweep.Length == 0)
        {
            Debug.LogWarning("The JSON does not contain a valid result.");
            return;
        }

        SweepPoint point = data.results.hwp_sweep[0];
        DetectorRuns detectors = expectedMode == 2 ? point.two_detectors : point.three_detectors;

        if (detectors == null || detectors.runs == null || detectors.runs.Length == 0)
        {
            Debug.LogWarning($"No runs for mode {expectedMode} in the JSON.");
            return;
        }

        Run currentRun = detectors.runs[0];
        coincidencesPanel.text = $"Coincidencias (Nc): {currentRun.coincidences_Nc}";
        g2Panel.text = currentRun.insufficient_statistics
            ? "Valor g(2): N/A (estadística insuficiente)"
            : $"Valor g(2): {currentRun.g2_calculated:F4}";
    }

    public void ShowLiveProgress(ProgressLine p)
    {
        if (p.detector_mode != expectedMode) return;

        coincidencesPanel.text = $"Nc: {p.NGTR}  (ángulo {p.angle_deg}°, corrida {p.num_test})";
        g2Panel.text = p.insufficient_statistics
            ? "g(2): N/A (estadística insuficiente)"
            : $"g(2): {p.g2:F4}";
    }
}
