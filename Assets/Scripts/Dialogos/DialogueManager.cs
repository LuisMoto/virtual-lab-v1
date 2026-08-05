using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement; // Librería necesaria para cambiar de escenas

public class DialogueManager : MonoBehaviour
{
    [Header("Textos")]
    public TMP_Text dialogueText;
    [TextArea(3, 10)]
    public string[] dialogues;

    [Header("Configuración de Velocidad")]
    public float normalSpeed = 0.04f;

    [Header("Interacción VR (apuntar + gatillo)")]
    [Tooltip("Botón invisible que cubre el DialogueBox completo. Apuntar + gatillo avanza el diálogo.")]
    public Button botonAvanzar;

    [Header("Sistema de Decisiones")]
    public GameObject panelDecisiones; // El contenedor de las opciones
    [Tooltip("Botón de la opción '2 Detectores' (ya tiene el componente Button)")]
    public Button botonOpcion1;
    [Tooltip("Botón de la opción '3 Detectores' (ya tiene el componente Button)")]
    public Button botonOpcion2;

    private int currentDialogue = 0;
    private bool isTyping = false;
    private bool isChoosing = false;
    private Coroutine typingCoroutine;

    void Start()
    {
        // Aseguramos que el panel empiece apagado
        if (panelDecisiones != null) panelDecisiones.SetActive(false);

        // Todo el flujo se resuelve con el Ray Interactor de los controles:
        // apuntar + gatillo dispara el onClick del botón señalado.
        // No hay dependencia de teclado en ningún punto del script.
        botonAvanzar.onClick.AddListener(OnAvanzar);
        botonOpcion1.onClick.AddListener(() => SeleccionarOpcion(0));
        botonOpcion2.onClick.AddListener(() => SeleccionarOpcion(1));

        dialogueText.text = "";
        if (dialogues.Length > 0)
        {
            StartDialogue();
        }
    }

    void StartDialogue()
    {
        typingCoroutine = StartCoroutine(TypeLine(dialogues[currentDialogue]));
    }

    // Llamado por botonAvanzar.onClick (equivalente al KeyCode.A anterior)
    void OnAvanzar()
    {
        if (isChoosing) return; // mientras se elige, este botón queda tapado por el panel de todas formas

        if (isTyping)
        {
            CompleteTextInstantly();
        }
        else
        {
            NextDialogue();
        }
    }

    void NextDialogue()
    {
        currentDialogue++;

        // Si ya no hay más diálogos, activamos la pantalla de decisión
        if (currentDialogue >= dialogues.Length)
        {
            ShowChoices();
            return;
        }

        typingCoroutine = StartCoroutine(TypeLine(dialogues[currentDialogue]));
    }

    IEnumerator TypeLine(string line)
    {
        isTyping = true;
        dialogueText.text = "";

        foreach (char letter in line.ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(normalSpeed);
        }

        isTyping = false;
    }

    void CompleteTextInstantly()
    {
        StopCoroutine(typingCoroutine);
        dialogueText.text = dialogues[currentDialogue];
        isTyping = false;
    }

    // ---------------------------------------------------------
    // SISTEMA DE DECISIONES
    // ---------------------------------------------------------

    void ShowChoices()
    {
        isChoosing = true;
        panelDecisiones.SetActive(true); // Aparece el menú

        // El resaltado de la opción señalada (antes colorSeleccionado/colorNormal
        // + navegación con flechas) ahora lo maneja el propio componente Button:
        // configura su "Highlighted Color" en amarillo desde el Inspector.
        // Al apuntar con el control, Unity lo tiñe solo — sin código extra.
    }

    void SeleccionarOpcion(int opcion)
    {
        if (opcion == 0)
        {
            SceneManager.LoadScene("DosDet");
        }
        else
        {
            SceneManager.LoadScene("TresDet");
        }
    }
}