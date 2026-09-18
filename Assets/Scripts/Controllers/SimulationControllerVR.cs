using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using System.Collections.Generic;
using VirtualLab.Networking;

public class SimulationControllerVR : MonoBehaviour
{
    [Header("Backend connection")]
    [SerializeField] private SimulationClient simulationClient;
    [SerializeField] private SSEStreamReader sseStreamReader;

    [Header("Simulation parameters")]
    // Valores por defecto = mismos defaults que Backend/simulator.py::validate_params().
    // POST /simulate (Backend/server.py::SimulationParameters) los exige explícitamente
    // (no tienen default en el Pydantic model), así que ya no pueden quedar implícitos
    // como antes con input.json ausente. Esto cubre, de forma mínima, el entregable
    // "Generación Dinámica de Parámetros" del Plan Maestro (Fase 1.2): quedan en memoria,
    // editables desde el Inspector, sin volver a escribir input.json.
    [SerializeField] private int numPulses = 2000;
    [SerializeField] private int numRuns = 5;

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

    private void Awake()
    {
        if (simulationClient == null)
            simulationClient = GetComponent<SimulationClient>();
        if (sseStreamReader == null)
            sseStreamReader = GetComponent<SSEStreamReader>();

        if (simulationClient != null)
        {
            simulationClient.OnSimulationStart += HandleClientStart;
            simulationClient.OnSimulationComplete += HandleClientComplete;
            simulationClient.OnSimulationError += HandleClientError;
        }

        if (sseStreamReader != null)
        {
            sseStreamReader.OnProgressLine += HandleStreamProgressLine;
            sseStreamReader.OnStreamError += HandleStreamError;
        }
    }

    private void OnDestroy()
    {
        if (simulationClient != null)
        {
            simulationClient.OnSimulationStart -= HandleClientStart;
            simulationClient.OnSimulationComplete -= HandleClientComplete;
            simulationClient.OnSimulationError -= HandleClientError;
        }

        if (sseStreamReader != null)
        {
            sseStreamReader.OnProgressLine -= HandleStreamProgressLine;
            sseStreamReader.OnStreamError -= HandleStreamError;
            sseStreamReader.StopListening();
        }
    }

    /// <summary>
    /// Launches the configured experiment against the backend (POST /simulate)
    /// and opens the SSE stream (GET /simulate/stream) to receive live progress.
    /// </summary>
    public void RunGrangierSimulation()
    {
        if (simulationClient == null || sseStreamReader == null)
        {
            string message = "Faltan componentes de red (SimulationClient / SSEStreamReader) en este GameObject.";
            UnityEngine.Debug.LogError($"[SimulationControllerVR] {message}");
            OnSimulationError?.Invoke(message);
            return;
        }

        string experimentName = ExperimentMap[experiment];
        UnityEngine.Debug.Log($"Starting simulation '{experimentName}' via backend...");

        string parametersJson = $"{{\"num_pulses\": {numPulses}, \"num_runs\": {numRuns}}}";

        // Se abre el stream antes del POST para no perder progreso temprano.
        // (En la práctica, el servidor mantiene la cola de progreso desde el
        // startup, independientemente de cuándo se conecte un cliente GET, así
        // que el orden exacto no es crítico — pero conectar primero da
        // feedback visual inmediato de "conectado" antes de lanzar la corrida.)
        sseStreamReader.StartListening();
        simulationClient.StartSimulation(parametersJson, experimentName);
    }

    private void HandleClientStart(string message)
    {
        UnityEngine.Debug.Log($"[SimulationControllerVR] {message}");
    }

    private void HandleClientComplete(string responseJson)
    {
        sseStreamReader.StopListening();
        UnityEngine.Debug.Log("Simulation finished cleanly. Updating final state...");
        OnSimulationCompleted?.Invoke();
    }

    private void HandleClientError(string errorMessage)
    {
        sseStreamReader.StopListening();
        UnityEngine.Debug.LogError($"Backend reported an error:\n{errorMessage}");
        OnSimulationError?.Invoke(errorMessage);
    }

    private void HandleStreamProgressLine(string jsonLine)
    {
        ProcessProgressLine(jsonLine);
    }

    private void HandleStreamError(string errorMessage)
    {
        // No fatal: el POST puede seguir completándose aunque el stream de
        // progreso falle o no esté disponible todavía (p. ej. mientras
        // GET /simulate/stream sigue en borrador — ver A2 en
        // SEMANA_2_ENTREGABLES.md). Solo se registra como advertencia.
        UnityEngine.Debug.LogWarning($"[SimulationControllerVR] SSE stream error (progreso en vivo no disponible): {errorMessage}");
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
