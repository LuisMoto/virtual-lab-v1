// SimulationClient.cs
// Tarea B1 (Semana 2): cliente HTTP que reemplaza System.Diagnostics.Process.
// Implementado a partir de SimulationClient.cs.stub.

using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

namespace VirtualLab.Networking
{
    /// <summary>
    /// Cliente que conecta a FastAPI backend en lugar de lanzar subproceso local.
    /// Reemplaza SimulationControllerVR.RunPythonProcess() con llamadas HTTP a POST /simulate.
    /// </summary>
    public class SimulationClient : MonoBehaviour
    {
        // ====================================================================
        // CONFIGURACIÓN
        // ====================================================================

        [Tooltip(
            "URL del backend FastAPI, sin barra final. 'localhost' solo " +
            "funciona si el backend corre en ESTA misma máquina (Editor o " +
            "Quest Link). Para el visor VR conectado por Wi-Fi (standalone), " +
            "cambia esto a la IP LAN del equipo que corre el backend, p. ej. " +
            "http://192.168.1.50:8000 -- esa IP aparece impresa en la consola " +
            "al arrancar server.py. Ver Docs/02_Backend_Python.md §10.")]
        [SerializeField]
        private string backendUrl = "http://localhost:8000";

        [SerializeField]
        private float requestTimeoutSecs = 300f; // 5 minutos para experimentos largos


        // ====================================================================
        // EVENTOS
        // ====================================================================

        /// <summary>Se dispara justo antes de enviar el POST. Parámetro: mensaje descriptivo.</summary>
        public event Action<string> OnSimulationStart;

        /// <summary>
        /// Se dispara cuando el backend responde con éxito (HTTP 2xx y status != "error").
        /// Parámetro: cuerpo JSON completo de la respuesta (sin parsear), tal como lo
        /// consumía antes SimulationControllerVR con el subproceso.
        /// </summary>
        public event Action<string> OnSimulationComplete;

        /// <summary>
        /// Se dispara ante cualquier falla: error de red/HTTP, o un HTTP 200 cuyo
        /// cuerpo trae "status": "error" (así responde Backend/utils.build_error_response).
        /// </summary>
        public event Action<string> OnSimulationError;


        // ====================================================================
        // CICLO DE VIDA
        // ====================================================================

        private void Awake()
        {
            WarnIfBackendUrlLooksLocalOnDevice();
        }

        /// <summary>
        /// En un build standalone de Android instalado en el propio visor
        /// (Quest desconectado, sin cable), <c>Application.platform</c> es
        /// <c>RuntimePlatform.Android</c> y "localhost" se refiere al visor
        /// mismo -- jamás va a encontrar ahí un backend Python corriendo en
        /// la PC. Este caso es fácil de dejar pasar sin darse cuenta (compila
        /// y corre perfecto en el Editor, donde "localhost" sí es válido) y
        /// falla en silencio (o con un error de red poco claro) ya instalado
        /// en el visor. Si en cambio Unity corre en el Editor o vía Quest
        /// Link/Air Link, <c>Application.platform</c> es el del PC (no
        /// Android) -- ahí "localhost" sigue siendo válido y esta advertencia
        /// no aplica. Solo loguea (Debug.LogWarning); no bloquea nada.
        /// </summary>
        private void WarnIfBackendUrlLooksLocalOnDevice()
        {
            bool isStandaloneAndroidBuild = Application.platform == RuntimePlatform.Android && !Application.isEditor;
            bool looksLocal = backendUrl.Contains("localhost") || backendUrl.Contains("127.0.0.1");

            if (isStandaloneAndroidBuild && looksLocal)
            {
                Debug.LogWarning(
                    "[SimulationClient] backendUrl sigue apuntando a "
                    + backendUrl
                    + " en un build standalone de Android -- 'localhost' aquí es "
                    + "el visor mismo, no va a encontrar el backend. Cambia "
                    + "backendUrl a la IP LAN de la máquina que corre server.py "
                    + "(aparece impresa en su consola al arrancar). Ver "
                    + "Docs/02_Backend_Python.md §10.");
            }
        }


        // ====================================================================
        // MÉTODOS PÚBLICOS
        // ====================================================================

        /// <summary>
        /// Lanza una simulación vía POST /simulate.
        /// </summary>
        /// <param name="parameters">Parámetros en formato JSON: {"num_pulses": 1000, "num_runs": 5}</param>
        /// <param name="experiment">Nombre del experimento: "grangier_hwp" o "wave_interference"</param>
        public void StartSimulation(string parameters, string experiment = "grangier_hwp")
        {
            StartCoroutine(SimulateCoroutine(parameters, experiment));
        }


        // ====================================================================
        // IMPLEMENTACIÓN INTERNA
        // ====================================================================

        private IEnumerator SimulateCoroutine(string parameters, string experiment)
        {
            string requestBody = BuildRequestBody(parameters, experiment);
            string url = $"{backendUrl}/simulate";

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = (int)requestTimeoutSecs;

                OnSimulationStart?.Invoke($"Simulando {experiment}...");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseBody = request.downloadHandler.text;

                    // El backend puede responder HTTP 200 con "status": "error"
                    // (ver Backend/utils.build_error_response) — no es un error de
                    // transporte, así que hay que revisar el sobre antes de dar por
                    // exitosa la simulación.
                    SimulationResponseEnvelopeWire envelope = TryParseEnvelope(responseBody);

                    if (envelope != null && envelope.status == "error")
                    {
                        string message = !string.IsNullOrEmpty(envelope.message)
                            ? envelope.message
                            : "El backend reportó un error sin mensaje.";
                        OnSimulationError?.Invoke(message);
                    }
                    else
                    {
                        OnSimulationComplete?.Invoke(responseBody);
                    }
                }
                else
                {
                    string detail = TryExtractDetail(request.downloadHandler?.text);
                    string error = !string.IsNullOrEmpty(detail)
                        ? $"HTTP {request.responseCode}: {detail}"
                        : $"HTTP Error: {request.responseCode} - {request.error}";
                    OnSimulationError?.Invoke(error);
                }
            }
        }

        /// <summary>
        /// Construye el cuerpo JSON esperado por POST /simulate.
        ///
        /// Contrato real (Backend/server.py::SimulationRequest), plano, SIN envoltura
        /// "configuration": { "experiment": "...", "parameters": { "num_pulses": ..., "num_runs": ... } }
        ///
        /// Nota: el stub original de este archivo envolvía esto en
        /// {"configuration": {"parameters": ...}}, que es el formato legado de
        /// input.json (Backend/main.py + utils.extract_parameters), no el que usa
        /// server.py hoy. Ver Docs/ESTANDARES_DOCUMENTACION_TECNICA.md §6.6 y §7.4.
        /// </summary>
        private string BuildRequestBody(string parameters, string experiment)
        {
            return $"{{\"experiment\": \"{experiment}\", \"parameters\": {parameters}}}";
        }

        private static SimulationResponseEnvelopeWire TryParseEnvelope(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<SimulationResponseEnvelopeWire>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string TryExtractDetail(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                HttpErrorDetailWire wire = JsonUtility.FromJson<HttpErrorDetailWire>(json);
                return wire?.detail;
            }
            catch (Exception)
            {
                return null;
            }
        }


        // ====================================================================
        // DEBUGGING
        // ====================================================================

        [ContextMenu("Test: POST /simulate")]
        public void DebugPost()
        {
            string testParams = "{\"num_pulses\": 100, \"num_runs\": 1}";
            StartSimulation(testParams);
        }
    }

    /// <summary>
    /// Sobre mínimo de la respuesta de /simulate, usado solo para distinguir
    /// éxito ("status" != "error") de error de aplicación, sin acoplarse a la
    /// forma de "results" (que varía por experimento). Ver Backend/utils.py
    /// (build_ok_response / build_error_response).
    /// </summary>
    [Serializable]
    internal class SimulationResponseEnvelopeWire
    {
        public string status;
        public string message;
    }

    /// <summary>
    /// Cuerpo de error estándar de FastAPI para HTTPException, p. ej.
    /// {"detail": "Unknown experiment: ..."} cuando /simulate recibe un
    /// nombre de experimento no registrado en EXPERIMENTS.
    /// </summary>
    [Serializable]
    internal class HttpErrorDetailWire
    {
        public string detail;
    }
}
