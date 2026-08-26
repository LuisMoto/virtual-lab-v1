using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class SimulationControllerVR : MonoBehaviour
{
    [Header("Python connection")]
    [FormerlySerializedAs("rutaPython")]
    [SerializeField] private string pythonPath = "python";
    [FormerlySerializedAs("nombreScriptPython")]
    [SerializeField] private string pythonScriptName = "main.py";

    /// <summary>
    /// Optical experiment implemented by a Python module under Backend/.
    /// </summary>
    public enum ExperimentType { GrangierHwp, WaveInterference }

    [Header("Experiment")]
    [FormerlySerializedAs("experimento")]
    [SerializeField] private ExperimentType experiment = ExperimentType.GrangierHwp;

    /// <summary>
    /// Experiment currently configured to run in this controller.
    /// </summary>
    public ExperimentType Experiment => experiment;

    [Header("Events")]
    [FormerlySerializedAs("OnSimulacionCompletada")]
    public UnityEvent OnSimulationCompleted;
    [FormerlySerializedAs("OnProgresoRecibido")]
    public ProgressUnityEvent OnProgressReceived;
    [FormerlySerializedAs("OnSimulacionError")]
    public UnityEvent<string> OnSimulationError;

    private static readonly Dictionary<ExperimentType, string> ExperimentMap = new()
    {
        { ExperimentType.GrangierHwp, "grangier_hwp" },
        { ExperimentType.WaveInterference, "wave_interference" }
    };

    private volatile string _latestProgressLine = null;

    /// <summary>
    /// Launches the configured experiment as a Python subprocess and streams its progress.
    /// </summary>
    public void RunGrangierSimulation()
    {
        UnityEngine.Debug.Log($"Starting simulation '{ExperimentMap[experiment]}' in Python...");
        StartCoroutine(RunPythonProcess());
    }

    private IEnumerator RunPythonProcess()
    {
        string scriptPath = Path.Combine(Application.dataPath, "../Backend/", pythonScriptName);

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = pythonPath;
        startInfo.Arguments = $"\"{scriptPath}\" {ExperimentMap[experiment]}";
        startInfo.WorkingDirectory = Path.GetDirectoryName(scriptPath);
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        Process process = new Process();
        process.StartInfo = startInfo;

        process.OutputDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
                _latestProgressLine = args.Data;
        };

        string pythonErrors = "";
        process.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
                pythonErrors += args.Data + "\n";
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            UnityEngine.Debug.Log($"Python process launched successfully, PID: {process.Id}");
        }
        catch (System.Exception e)
        {
            string launchMessage = $"No se pudo lanzar Python. Verifica 'pythonPath' en el Inspector. Detalle: {e.Message}";
            UnityEngine.Debug.LogError(launchMessage);
            OnSimulationError?.Invoke(launchMessage);
            yield break;
        }

        while (!process.HasExited)
        {
            string line = _latestProgressLine;
            if (line != null)
            {
                _latestProgressLine = null;
                ProcessProgressLine(line);
            }
            yield return null;
        }

        string finalLine = _latestProgressLine;
        if (finalLine != null)
        {
            _latestProgressLine = null;
            ProcessProgressLine(finalLine);
        }

        bool hadError = !string.IsNullOrEmpty(pythonErrors) || process.ExitCode != 0;

        if (hadError)
        {
            string errorMessage = !string.IsNullOrEmpty(pythonErrors)
                ? pythonErrors
                : $"Python terminó con código de salida {process.ExitCode}. Revisa output.json para el detalle.";

            UnityEngine.Debug.LogError($"Python ran but reported an internal error:\n{errorMessage}");
            OnSimulationError?.Invoke(errorMessage);
        }
        else
        {
            UnityEngine.Debug.Log("Simulation finished cleanly. Updating final state...");
            OnSimulationCompleted?.Invoke();
        }
    }

    private void ProcessProgressLine(string line)
    {
        ProgressLineWire wire;
        try
        {
            wire = JsonUtility.FromJson<ProgressLineWire>(line);
        }
        catch (System.Exception)
        {
            return;
        }

        if (wire != null && wire.type == "progress")
        {
            OnProgressReceived?.Invoke(ProgressLine.FromWire(wire));
        }
    }
}

[System.Serializable]
public class ProgressUnityEvent : UnityEvent<ProgressLine> { }

/// <summary>
/// Public, camelCase progress DTO consumed by the rest of the Unity codebase.
/// Built from <see cref="ProgressLineWire"/>, which mirrors Python's snake_case JSON exactly.
/// </summary>
[System.Serializable]
public class ProgressLine
{
    public string type;
    public string experiment;
    public int numAngles;
    public int totalRuns;
    public float angleDeg;
    public int detectorMode;
    public int numTest;
    public int witnessCount;
    public int transmittedCount;
    public int reflectedCount;
    public int tripleCoincidenceCount;
    public float g2;
    public bool insufficientStatistics;
    public string status;

    /// <summary>
    /// Maps a wire-format progress line (Python snake_case) to the public camelCase DTO.
    /// </summary>
    public static ProgressLine FromWire(ProgressLineWire wire)
    {
        if (wire == null) return null;

        return new ProgressLine
        {
            type = wire.type,
            experiment = wire.experiment,
            numAngles = wire.num_angles,
            totalRuns = wire.total_runs,
            angleDeg = wire.angle_deg,
            detectorMode = wire.detector_mode,
            numTest = wire.num_test,
            witnessCount = wire.witness_count,
            transmittedCount = wire.transmitted_count,
            reflectedCount = wire.reflected_count,
            tripleCoincidenceCount = wire.triple_coincidence_count,
            g2 = wire.g2,
            insufficientStatistics = wire.insufficient_statistics,
            status = wire.status
        };
    }
}

/// <summary>
/// Wire-format progress line matching Python's exact snake_case JSON keys.
/// Used only as the <see cref="JsonUtility.FromJson"/> deserialization target; not consumed directly.
/// </summary>
[System.Serializable]
public class ProgressLineWire
{
    public string type;
    public string experiment;
    public int num_angles;
    public int total_runs;
    public float angle_deg;
    public int detector_mode;
    public int num_test;
    public int witness_count;
    public int transmitted_count;
    public int reflected_count;
    public int triple_coincidence_count;
    public float g2;
    public bool insufficient_statistics;
    public string status;
}
