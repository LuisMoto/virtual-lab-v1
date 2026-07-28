using UnityEngine;
using UnityEngine.Events;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class ControladorSimulacionVR : MonoBehaviour
{
    [Header("Conexión con Python")]
    [Tooltip("Pon 'python' si está registrado en el PATH, o la ruta absoluta a python.exe")]
    public string rutaPython = "python";
    public string nombreScriptPython = "main.py";

    public enum TipoExperimento { GrangierHwp, InterferenciaOndas }
    [Header("Experimento")]
    public TipoExperimento experimento = TipoExperimento.GrangierHwp;

    [Header("Eventos")]
    [Tooltip("Se dispara UNA vez, cuando Python termina y el archivo final está listo.")]
    public UnityEvent OnSimulacionCompletada;

    [Tooltip("Se dispara repetidamente MIENTRAS Python corre, con la última corrida disponible.")]
    public ProgresoUnityEvent OnProgresoRecibido;

    private static readonly Dictionary<TipoExperimento, string> MapaExperimentos = new()
    {
        { TipoExperimento.GrangierHwp, "grangier_hwp" },
        { TipoExperimento.InterferenciaOndas, "interferencia_ondas" }
    };

    // Solo guardamos la última línea recibida: con miles de corridas por segundo,
    // no queremos procesar cada una, solo mostrar el estado más reciente por frame.
    private volatile string ultimaLineaProgreso = null;

    public void DetonarSimulacionGrangier()
    {
        UnityEngine.Debug.Log($"Iniciando simulación '{MapaExperimentos[experimento]}' en Python...");
        StartCoroutine(EjecutarPython());
    }

    private IEnumerator EjecutarPython()
    {
        string rutaScript = Path.Combine(Application.dataPath, "../Backend/", nombreScriptPython);
        UnityEngine.Debug.Log($"[DEBUG] Script: {rutaScript}");

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

        // OJO: estos handlers corren en un hilo secundario de .NET, NO en el hilo
        // principal de Unity. Por eso aquí solo guardamos el texto; el procesamiento
        // real (parseo de JSON, actualizar UI) pasa en el loop de abajo.
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
            UnityEngine.Debug.LogError($"Error CRÍTICO al lanzar Python. Verifica 'rutaPython' en el Inspector. Detalle: {e.Message}");
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

        if (!string.IsNullOrEmpty(erroresPython))
        {
            UnityEngine.Debug.LogError($"Python se ejecutó pero reportó un error interno:\n{erroresPython}");
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
            return; // línea incompleta o no es JSON (no debería pasar, pero por seguridad)
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
    public string tipo;           // "inicio" | "progreso" | "fin"
    public string experimento;
    public int num_angulos;       // solo en "inicio"
    public int total_corridas;    // solo en "inicio"
    public float angulo_grados;   // solo en "progreso"
    public int modo_detectores;   // solo en "progreso": 2 o 3
    public int num_test;
    public int NG;                // solo válido si modo_detectores == 3
    public int NGT;
    public int NGR;
    public int NGTR;
    public float g2;
    public string status;         // solo en "fin"
}