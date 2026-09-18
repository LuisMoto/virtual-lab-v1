// SSEStreamReader.cs
// Tarea B2 (Semana 2): cliente SSE (Server-Sent Events) para progreso en vivo.
// Implementado a partir de SSEStreamReader.cs.stub.

using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;

namespace VirtualLab.Networking
{
    /// <summary>
    /// Lee eventos SSE desde GET /simulate/stream y emite un callback por cada
    /// línea de progreso recibida, a medida que llegan (streaming real, no
    /// buffering al final).
    ///
    /// Nota de implementación: el stub sugería como opción principal abrir un
    /// socket TCP crudo (System.Net.Sockets) y parsear el HTTP a mano, por
    /// considerar que un DownloadHandler personalizado era "complejo". En la
    /// práctica, subclasificar DownloadHandlerScript y sobrescribir
    /// ReceiveData(byte[], int) es la vía idiomática de Unity para streaming
    /// incremental: reutiliza el stack HTTP de UnityWebRequest (headers, chunked
    /// transfer-encoding, timeouts, Abort()) en vez de reimplementarlo, y el
    /// manejo de líneas partidas entre llamadas es solo un buffer de texto
    /// pendiente (ver SseDownloadHandler abajo). Por eso se optó por esta ruta.
    /// </summary>
    public class SSEStreamReader : MonoBehaviour
    {
        // ====================================================================
        // CONFIGURACIÓN
        // ====================================================================

        [SerializeField]
        private string backendUrl = "http://localhost:8000";


        // ====================================================================
        // EVENTOS
        // ====================================================================

        /// <summary>Dispara por cada línea "data: ..." recibida. Parámetro: el JSON, sin el prefijo.</summary>
        public event Action<string> OnProgressLine;

        public event Action OnStreamConnected;
        public event Action<string> OnStreamError;


        // ====================================================================
        // MÉTODOS PÚBLICOS
        // ====================================================================

        /// <summary>
        /// Abre el stream SSE y comienza a leer eventos. Ignorado (con warning)
        /// si ya hay una lectura en curso.
        /// </summary>
        public void StartListening()
        {
            if (_currentRequest != null)
            {
                Debug.LogWarning("[SSEStreamReader] Ya hay un stream activo; se ignora StartListening() duplicado.");
                return;
            }

            StartCoroutine(ListenCoroutine());
        }

        /// <summary>
        /// Detiene la lectura del stream de forma limpia (no dispara OnStreamError).
        /// </summary>
        public void StopListening()
        {
            if (_currentRequest == null) return;

            _stoppedManually = true;
            _currentRequest.Abort();
        }


        // ====================================================================
        // IMPLEMENTACIÓN INTERNA
        // ====================================================================

        private UnityWebRequest _currentRequest;
        private bool _stoppedManually;

        private IEnumerator ListenCoroutine()
        {
            string url = $"{backendUrl}/simulate/stream";

            _currentRequest = new UnityWebRequest(url, "GET");
            _currentRequest.downloadHandler = new SseDownloadHandler(OnLineReceived);
            _currentRequest.disposeDownloadHandlerOnDispose = true;

            OnStreamConnected?.Invoke();

            yield return _currentRequest.SendWebRequest();

            bool wasStoppedManually = _stoppedManually;

            // Abort() (desde StopListening) también deja result != Success; eso es
            // esperado y no debe reportarse como error.
            if (_currentRequest.result != UnityWebRequest.Result.Success && !wasStoppedManually)
            {
                OnStreamError?.Invoke($"Stream Error: {_currentRequest.error}");
            }

            _currentRequest.Dispose();
            _currentRequest = null;
            _stoppedManually = false;
        }

        private void OnLineReceived(string rawLine)
        {
            if (string.IsNullOrEmpty(rawLine))
                return; // línea vacía = separador de evento SSE; no hay nada que emitir aquí.

            // Comentario/keep-alive: ": keep-alive"
            if (rawLine.StartsWith(":"))
                return;

            if (rawLine.StartsWith("data: "))
            {
                OnProgressLine?.Invoke(rawLine.Substring(6));
            }
            else if (rawLine.StartsWith("data:"))
            {
                // Tolera "data:" sin espacio, válido según la especificación SSE.
                OnProgressLine?.Invoke(rawLine.Substring(5).TrimStart());
            }
        }


        // ====================================================================
        // INTEGRACIÓN CON SIMULATIONUICONTROLLER
        // ====================================================================

        // 1. Obtener referencia a SSEStreamReader:
        //    SSEStreamReader reader = GetComponent<SSEStreamReader>();
        //
        // 2. Suscribirse al evento:
        //    reader.OnProgressLine += (jsonLine) => {
        //        ProgressLineWire wire = JsonUtility.FromJson<ProgressLineWire>(jsonLine);
        //        if (wire != null && wire.type == "progress")
        //            HandleProgress(ProgressLine.FromWire(wire));
        //    };
        //
        // 3. Iniciar lectura (idealmente antes o junto con el POST de SimulationClient):
        //    reader.StartListening();
        //
        // 4. Parar lectura cuando termine la simulación:
        //    reader.StopListening();
        //
        // (Implementado en SimulationControllerVR.cs — ver B3.)


        // ====================================================================
        // DEBUGGING
        // ====================================================================

        [ContextMenu("Test: GET /simulate/stream")]
        public void DebugStream()
        {
            StartListening();
        }
    }

    /// <summary>
    /// DownloadHandler personalizado que entrega cada línea completa recibida
    /// del stream tan pronto llega, en vez de esperar a que la respuesta
    /// termine (que es lo que hace DownloadHandlerBuffer).
    ///
    /// Los bytes de una línea SSE pueden llegar partidos entre dos llamadas a
    /// ReceiveData (p. ej. "data: {\"sta" en una llamada y "tus\":...}\n\n" en
    /// la siguiente); por eso se acumula en _pendingBuffer y solo se emite lo
    /// que ya forma una línea completa (delimitada por '\n'), dejando el resto
    /// pendiente para la próxima llamada.
    /// </summary>
    internal class SseDownloadHandler : DownloadHandlerScript
    {
        private readonly Action<string> _onLine;
        private string _pendingBuffer = "";

        public SseDownloadHandler(Action<string> onLine) : base()
        {
            _onLine = onLine;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength == 0)
                return false;

            _pendingBuffer += Encoding.UTF8.GetString(data, 0, dataLength);

            int newlineIndex;
            while ((newlineIndex = _pendingBuffer.IndexOf('\n')) >= 0)
            {
                string line = _pendingBuffer.Substring(0, newlineIndex);
                _pendingBuffer = _pendingBuffer.Substring(newlineIndex + 1);

                if (line.EndsWith("\r"))
                    line = line.Substring(0, line.Length - 1);

                _onLine?.Invoke(line);
            }

            return true; // true = seguir recibiendo datos
        }
    }
}
