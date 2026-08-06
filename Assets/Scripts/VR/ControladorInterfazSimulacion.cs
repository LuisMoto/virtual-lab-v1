using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Text;

/// <summary>
/// Controla el panel flotante inmersivo (Carga -> Terminal en vivo -> Resumen)
/// para UNA escena de experimento (DosDet o TresDet).
///
/// IMPORTANTE — esto NO se auto-conecta como la versión anterior:
/// ControladorSimulacionVR usa UnityEvents cableados desde el Inspector, no
/// eventos estáticos de C#. Hay que arrastrar los métodos públicos de abajo
/// a los eventos del componente ControladorSimulacionVR en la escena:
///
///   ControladorSimulacionVR.OnProgresoRecibido     -> ManejarProgreso
///   ControladorSimulacionVR.OnSimulacionCompletada  -> ManejarFin
///   ControladorSimulacionVR.OnSimulacionError       -> ManejarError
/// </summary>
public class ControladorInterfazSimulacion : MonoBehaviour
{
    [Header("Panel raíz")]
    public GameObject canvasFlotante;

    [Header("Paso 1: Estado de Carga")]
    public GameObject vistaCarga;
    public TextMeshProUGUI textoCarga;

    [Header("Paso 2: Terminal en Vivo")]
    public GameObject vistaTerminal;
    public TextMeshProUGUI textoTerminal;
    public ScrollRect scrollTerminal;

    [Header("Paso 3: Resumen Final")]
    public GameObject vistaResumen;
    public TextMeshProUGUI textoResumen;

    [Header("Referencia al integrador")]
    public ControladorSimulacionVR integrador;

    [Header("Solo aplica si integrador.experimento == GrangierHwp")]
    [Tooltip("2 = DosDet (luz natural). 3 = una futura escena de testigo/3 detectores. " +
             "Ignora cualquier línea de progreso que no sea de este modo.")]
    public int modoDetectorEsperado = 2;

    private bool _recibioAlgunaLinea;
    private int _corridasVistas;
    private LineaProgreso _ultimoProgreso;

    public void EjecutarExperimento()
    {
        if (integrador == null)
        {
            Debug.LogError("[ControladorInterfazSimulacion] Falta asignar 'integrador' en el Inspector.");
            return;
        }

        _recibioAlgunaLinea = false;
        _corridasVistas = 0;
        _ultimoProgreso = null;

        canvasFlotante.SetActive(true);
        MostrarSoloVista(vistaCarga);
        textoCarga.text = "Cargando experimento...";
        textoTerminal.text = "";
        textoResumen.text = "";

        // Nota: el método se llama "DetonarSimulacionGrangier" en ControladorSimulacionVR
        // aunque también dispara interferencia_ondas — usa el 'experimento' ya asignado
        // en el Inspector de esa escena, no recibe parámetro.
        integrador.DetonarSimulacionGrangier();
    }

    // Cablear en el Inspector: ControladorSimulacionVR -> OnProgresoRecibido
    public void ManejarProgreso(LineaProgreso p)
    {
        bool esGrangier = integrador.experimento == ControladorSimulacionVR.TipoExperimento.GrangierHwp;

        // simulator.py intercala líneas de modo 2 y modo 3 para el mismo ángulo;
        // esta escena solo debe reaccionar a las que le corresponden.
        if (esGrangier && p.modo_detectores != modoDetectorEsperado)
            return;

        if (!_recibioAlgunaLinea)
        {
            _recibioAlgunaLinea = true;
            MostrarSoloVista(vistaTerminal);
            AgregarLineaTerminal("> Simulación iniciada.");
            AgregarLineaTerminal("--------------------------------------------------");
        }

        _corridasVistas++;
        _ultimoProgreso = p;

        AgregarLineaTerminal(FormatearLinea(p));
    }

    // Cablear en el Inspector: ControladorSimulacionVR -> OnSimulacionCompletada
    public void ManejarFin()
    {
        // interferencia_ondas no emite progreso (es un cálculo instantáneo, sin corridas),
        // así que aquí nunca se pasó por vistaTerminal — se salta directo de Carga a Resumen,
        // lo cual es el comportamiento esperado para esa escena, no un bug.
        textoResumen.text = integrador.experimento == ControladorSimulacionVR.TipoExperimento.InterferenciaOndas
            ? ConstruirResumenOndas()
            : ConstruirResumenGrangier();

        MostrarSoloVista(vistaResumen);
    }

    // Cablear en el Inspector: ControladorSimulacionVR -> OnSimulacionError
    public void ManejarError(string mensaje)
    {
        if (_recibioAlgunaLinea)
            AgregarLineaTerminal($"[ERROR] {mensaje}");

        textoResumen.text = ConstruirEncabezadoError(mensaje);
        MostrarSoloVista(vistaResumen);
    }

    private string FormatearLinea(LineaProgreso p)
    {
        // Modo 2 no trae un NG (testigo) con significado real (Python manda null) —
        // se omite para no mostrar un engañoso "NG: 0".
        if (p.modo_detectores == 3)
            return $"[{_corridasVistas}] ángulo={p.angulo_grados:F1}° NG={p.NG} Nc={p.NGTR} g²={p.g2:F4}";

        return $"[{_corridasVistas}] ángulo={p.angulo_grados:F1}° Nc={p.NGTR} g²={p.g2:F4}";
    }

    private string ConstruirResumenGrangier()
    {
        if (_ultimoProgreso == null)
            return ConstruirEncabezadoError("No se recibió ninguna corrida durante la simulación.");

        string titulo = modoDetectorEsperado == 3 ? "3 DETECTORES (TESTIGO)" : "LUZ NATURAL (2 DETECTORES)";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<b>RESUMEN — {titulo}</b>");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine($"Corridas registradas    : {_corridasVistas}");
        sb.AppendLine($"Última corrida           : #{_ultimoProgreso.num_test}");
        sb.AppendLine($"Ángulo HWP                : {_ultimoProgreso.angulo_grados:F1}°");
        sb.AppendLine($"Coincidencias (Nc)        : {_ultimoProgreso.NGTR}");
        sb.AppendLine($"g² calculado              : {_ultimoProgreso.g2:F4}");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine("<i>Nota: son los valores de la última corrida recibida en vivo,");
        sb.AppendLine("no del archivo final (output.json no incluye este modo hoy).</i>");
        return sb.ToString();
    }

    private string ConstruirResumenOndas()
    {
        string ruta = Path.Combine(Application.dataPath, "../Backend/output.json");
        if (!File.Exists(ruta))
            return ConstruirEncabezadoError("No se encontró el archivo de resultados (output.json).");

        JsonSalidaOndas datos;
        try
        {
            datos = JsonUtility.FromJson<JsonSalidaOndas>(File.ReadAllText(ruta));
        }
        catch (System.Exception e)
        {
            return ConstruirEncabezadoError($"No se pudo leer el resultado: {e.Message}");
        }

        if (datos == null || datos.status != "ok" || datos.experimento != "interferencia_ondas")
        {
            string status = datos?.status ?? "desconocido";
            return ConstruirEncabezadoError($"Resultado inesperado (status='{status}').");
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>RESUMEN — INTERFERENCIA DE ONDAS</b>");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine($"Fase (ΔΦ)                 : {datos.resultados.fase_grados}°");
        sb.AppendLine($"Intensidad relativa       : {datos.resultados.intensidad_relativa:F3}");
        sb.AppendLine($"Visibilidad               : {datos.resultados.visibilidad:F3}");
        return sb.ToString();
    }

    private void MostrarSoloVista(GameObject vistaActiva)
    {
        vistaCarga.SetActive(vistaActiva == vistaCarga);
        vistaTerminal.SetActive(vistaActiva == vistaTerminal);
        vistaResumen.SetActive(vistaActiva == vistaResumen);
    }

    private void AgregarLineaTerminal(string linea)
    {
        textoTerminal.text += linea + "\n";
        DesplazarTerminalAlFinal();
    }

    private void DesplazarTerminalAlFinal()
    {
        if (scrollTerminal == null) return;
        Canvas.ForceUpdateCanvases();
        scrollTerminal.verticalNormalizedPosition = 0f;
    }

    private string ConstruirEncabezadoError(string mensaje)
    {
        return $"<color=#FF5555><b>ERROR</b></color>\n{mensaje}";
    }
}