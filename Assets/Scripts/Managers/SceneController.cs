using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fase 1.1 del Plan Maestro (Infraestructura de Red y Centralización).
///
/// Punto único de entrada para la navegación entre escenas y para el estado del
/// experimento seleccionado. Antes de este script, la navegación estaba dispersa
/// (p. ej. <see cref="DialogueManager"/>.SelectOption() llamaba a
/// <see cref="SceneManager"/>.LoadScene() directamente) y no había un lugar
/// central para guardar qué experimento eligió el usuario. SceneController
/// centraliza ambas cosas, desacoplando a la UI y al XR Interaction Toolkit de
/// los detalles de navegación y de la lógica de simulación.
///
/// Semana 1: solo cubre navegación + estado del experimento elegido. La conexión
/// con el backend por red (Fase 1.3/1.4) se agrega en las próximas semanas — el
/// lugar donde va a enganchar queda marcado con un TODO al final de este archivo.
/// </summary>
public class SceneController : MonoBehaviour
{
    public enum ExperimentScene
    {
        Intro,
        DosDetectores,
        TresDetectores
    }

    // Nombres reales de escena (Assets/Scenes/*.unity) — único lugar del proyecto
    // donde estos strings deberían aparecer hardcodeados.
    private const string SceneNameIntro = "Scene_1Intro";
    private const string SceneNameDosDet = "Scene_DosDet";
    private const string SceneNameTresDet = "Scene_TresDet";

    private static SceneController _instance;

    /// <summary>
    /// Acceso global al singleton. Si todavía no existe una instancia en la escena
    /// (por ejemplo, al arrancar desde Scene_1Intro sin haberlo colocado a mano),
    /// se crea automáticamente un GameObject persistente que la contiene, así que
    /// no es obligatorio arrastrarlo a ninguna escena para que funcione.
    /// </summary>
    public static SceneController Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("SceneController");
                _instance = go.AddComponent<SceneController>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    /// <summary>Experimento elegido por el usuario en el diálogo introductorio.</summary>
    public ExperimentScene CurrentExperiment { get; private set; } = ExperimentScene.Intro;

    /// <summary>
    /// Cantidad de detectores del experimento actualmente seleccionado (2 o 3).
    /// Hoy este número se sigue configurando a mano por escena en el Inspector de
    /// <see cref="GrangierDataReader"/> ("expectedMode"); este valor es la fuente
    /// de verdad centralizada que, más adelante, debería alimentar ese campo en
    /// vez de un número fijo hardcodeado por escena.
    /// </summary>
    public int CurrentDetectorMode =>
        CurrentExperiment == ExperimentScene.TresDetectores ? 3 : 2;

    void Awake()
    {
        // Si ya existe otra instancia (por ejemplo, porque el objeto se colocó a
        // mano en la escena Y además alguien más llamó a Instance antes de que
        // este Awake corriera), esta se destruye para preservar el patrón singleton.
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Carga la escena de introducción/diálogo.</summary>
    public void LoadIntro()
    {
        CurrentExperiment = ExperimentScene.Intro;
        SceneManager.LoadScene(SceneNameIntro);
    }

    /// <summary>Carga el experimento de 2 detectores.</summary>
    public void LoadDosDetectores()
    {
        CurrentExperiment = ExperimentScene.DosDetectores;
        SceneManager.LoadScene(SceneNameDosDet);
    }

    /// <summary>Carga el experimento de 3 detectores.</summary>
    public void LoadTresDetectores()
    {
        CurrentExperiment = ExperimentScene.TresDetectores;
        SceneManager.LoadScene(SceneNameTresDet);
    }

    // TODO (Fase 1.3/1.4 del Plan Maestro): cuando el backend se exponga como API
    // de red (Backend/server.py) y Unity deje de lanzar el subproceso local, este
    // es el lugar natural para exponer el cliente HTTP/SSE (UnityWebRequest) que
    // hoy vive disperso dentro de SimulationControllerVR.RunPythonProcess().
}
