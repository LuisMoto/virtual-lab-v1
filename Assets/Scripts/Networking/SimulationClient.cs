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
