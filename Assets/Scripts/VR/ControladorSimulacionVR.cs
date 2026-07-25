using UnityEngine;
using System.Diagnostics;
using System.Collections;
using System.IO;

public class ControladorSimulacionVR : MonoBehaviour
{
    [Header("Conexión con Python")]
    public string rutaPython = "python";
    public string nombreScriptPython = "main.py";

    [Header("Lector de Datos")]
    public LectorDatosGrangier lector;

    public void DetonarSimulacionGrangier()
    {
        UnityEngine.Debug.Log("Iniciando simulación cuántica en Python...");
        StartCoroutine(EjecutarPython());
    }

    private IEnumerator EjecutarPython()
    {
        string rutaScript = Path.Combine(Application.dataPath, "../", nombreScriptPython);

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = rutaPython;
        startInfo.Arguments = $"\"{rutaScript}\"";

        // SOLUCIÓN 3: Le decimos a Python exactamente en qué carpeta trabajar
        startInfo.WorkingDirectory = Path.GetDirectoryName(rutaScript);

        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        // Atrapamos los errores internos de Python (Punto 4)
        startInfo.RedirectStandardError = true;

        Process proceso = new Process();
        proceso.StartInfo = startInfo;

        try
        {
            proceso.Start();
            // SOLUCIÓN 1: Confirmamos que el proceso realmente arrancó
            UnityEngine.Debug.Log($"Proceso Python lanzado con éxito, PID: {proceso.Id}");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Error CRÍTICO al lanzar Python. Probablemente la ruta en el Inspector esté mal. Detalle: {e.Message}");
            yield break; // Detenemos la rutina para no causar más errores
        }

        // Esperamos a que Python termine sin congelar Unity
        while (!proceso.HasExited)
        {
            yield return null;
        }

        // Leemos si el código de Python tronó por alguna razón
        string erroresPython = proceso.StandardError.ReadToEnd();

        if (!string.IsNullOrEmpty(erroresPython))
        {
            UnityEngine.Debug.LogError($"Python se ejecutó pero reportó este error interno:\n{erroresPython}");
        }
        else
        {
            UnityEngine.Debug.Log("Simulación de Python terminada limpiamente. Actualizando paneles VR...");
            if (lector != null)
            {
                lector.ActualizarPantallasFlotantes();
            }
        }
    }
}