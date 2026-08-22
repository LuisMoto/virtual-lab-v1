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
    public string pythonPath = "python";
    [FormerlySerializedAs("nombreScriptPython")]
    public string pythonScriptName = "main.py";

    public enum ExperimentType { GrangierHwp, WaveInterference }
    [Header("Experiment")]
    [FormerlySerializedAs("experimento")]
    public ExperimentType experiment = ExperimentType.GrangierHwp;

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

    private volatile string latestProgressLine = null;

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
                latestProgressLine = args.Data;
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
            string line = latestProgressLine;
            if (line != null)
            {
                latestProgressLine = null;
                ProcessProgressLine(line);
            }
            yield return null;
        }

        string finalLine = latestProgressLine;
        if (finalLine != null)
        {
            latestProgressLine = null;
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
        ProgressLine data;
        try
        {
            data = JsonUtility.FromJson<ProgressLine>(line);
        }
        catch (System.Exception)
        {
            return;
        }

        if (data != null && data.type == "progress")
        {
            OnProgressReceived?.Invoke(data);
        }
    }
}

[System.Serializable]
public class ProgressUnityEvent : UnityEvent<ProgressLine> { }

[System.Serializable]
public class ProgressLine
{
    public string type;
    public string experiment;
    public int num_angles;
    public int total_runs;
    public float angle_deg;
    public int detector_mode;
    public int num_test;
    public int NG;
    public int NGT;
    public int NGR;
    public int NGTR;
    public float g2;
    public bool insufficient_statistics;
    public string status;
}
