using UnityEngine;
using UnityEngine.Events;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class ControladorSimulacionVR : MonoBehaviour
{
    [Header("Conexión con Python")]
    public string rutaPython = "python";
    public string nombreScriptPython = "main.py";

    public enum TipoExperimento { GrangierHwp, InterferenciaOndas }
    [Header("Experimento")]
    public TipoExperimento experimento = TipoExperimento.GrangierHwp;

    [Header("Eventos")]
    public UnityEvent OnSimulacionCompletada;
    public ProgresoUnityEvent OnProgresoRecibido;
    public UnityEvent<string> OnSimulacionError;

    private static readonly Dictionary<TipoExperimento, string> MapaExperimentos = new()
    {
        { TipoExperimento.GrangierHwp, "grangier_hwp" },
        { TipoExperimento.InterferenciaOndas, "interferencia_ondas" }
    };

    private volatile string ultimaLineaProgreso = null;

    public void DetonarSimulacionGrangier()
    {
        UnityEngine.Debug.Log($"Iniciando simulación '{MapaExperimentos[experimento]}' en Python...");
        StartCoroutine(EjecutarPython());
    }

    private IEnumerator EjecutarPython()
    {
        string rutaScript = Path.Combine(Application.dataPath, "../Backend/", nombreScriptPython);

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = rutaPython;
        startInfo.Arguments = $"\"{rutaScript}\" {MapaExperimentos[experimento]}";
        startInfo.WorkingDirectory = Path.GetDirectoryName(rutaScript);
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        Process proceso = new Process();
        proceso.StartInfo = startInfo;

        proceso.OutputDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
                ultimaLineaProgreso = args.Data;
        };

        string erroresPython = "";
        proceso.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
                erroresPython += args.Data + "\n";
        };

        try
        {
            proceso.Start();
            proceso.BeginOutputReadLine();
            proceso.BeginErrorReadLine();
            UnityEngine.Debug.Log($"Proceso Python lanzado con éxito, PID: {proceso.Id}");
        }
        catch (System.Exception e)
        {
            string mensajeLanzamiento = $"No se pudo lanzar Python. Verifica 'rutaPython' en el Inspector. Detalle: {e.Message}";
            UnityEngine.Debug.LogError(mensajeLanzamiento);
            OnSimulacionError?.Invoke(mensajeLanzamiento);
            yield break;
        }

        while (!proceso.HasExited)
        {
            string linea = ultimaLineaProgreso;
            if (linea != null)
            {
                ultimaLineaProgreso = null;
                ProcesarLineaProgreso(linea);
            }
            yield return null;
        }

        string lineaFinal = ultimaLineaProgreso;
        if (lineaFinal != null)
        {
            ultimaLineaProgreso = null;
            ProcesarLineaProgreso(lineaFinal);
        }

        bool huboError = !string.IsNullOrEmpty(erroresPython) || proceso.ExitCode != 0;

        if (huboError)
        {
            string mensajeError = !string.IsNullOrEmpty(erroresPython)
                ? erroresPython
                : $"Python terminó con código de salida {proceso.ExitCode}. Revisa output.json para el detalle.";

            UnityEngine.Debug.LogError($"Python se ejecutó pero reportó un error interno:\n{mensajeError}");
            OnSimulacionError?.Invoke(mensajeError);
        }
        else
        {
            UnityEngine.Debug.Log("Simulación terminada limpiamente. Actualizando estado final...");
            OnSimulacionCompletada?.Invoke();
        }
    }

    private void ProcesarLineaProgreso(string linea)
    {
        LineaProgreso datos;
        try
        {
            datos = JsonUtility.FromJson<LineaProgreso>(linea);
        }
        catch (System.Exception)
        {
            return;
        }

        if (datos != null && datos.tipo == "progreso")
        {
            OnProgresoRecibido?.Invoke(datos);
        }
    }
}

[System.Serializable]
public class ProgresoUnityEvent : UnityEvent<LineaProgreso> { }

[System.Serializable]
public class LineaProgreso
{
    public string tipo;
    public string experimento;
    public int num_angulos;
    public int total_corridas;
    public float angulo_grados;
    public int modo_detectores;
    public int num_test;
    public int NG;
    public int NGT;
    public int NGR;
    public int NGTR;
    public float g2;
    public bool estadistica_insuficiente;
    public string status;
}