using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    public float fastSpeed = 0.01f;

    [Header("Sistema de Decisiones")]
    public GameObject panelDecisiones; // El contenedor de las opciones
    public TMP_Text textoOpcion1;      // "2 Detectores"
    public TMP_Text textoOpcion2;      // "3 Detectores"
    public Color colorSeleccionado = Color.yellow; // Color cuando está seleccionada
    public Color colorNormal = Color.white;        // Color cuando no está seleccionada

    private int currentDialogue = 0;
    private bool isTyping = false;
    private Coroutine typingCoroutine;

    // Variables de control para las decisiones
    private bool isChoosing = false;
    private int currentChoice = 0; // 0 = Opción 1, 1 = Opción 2

    void Start()
    {
        // Asegurarnos de que el panel empiece apagado
        if (panelDecisiones != null) panelDecisiones.SetActive(false);

        dialogueText.text = "";
        if (dialogues.Length > 0)
        {
            StartDialogue();
        }
    }

    void Update()
    {
        // Si estamos en la fase de elegir, bloqueamos la tecla 'A' y activamos flechas
        if (isChoosing)
        {
            HandleChoiceInput();
            return;
        }

        // Comportamiento normal del diálogo
        if (Input.GetKeyDown(KeyCode.A))
        {
            if (isTyping)
            {
                CompleteTextInstantly();
            }
            else
            {
                NextDialogue();
            }
        }
    }

    void StartDialogue()
    {
        typingCoroutine = StartCoroutine(TypeLine(dialogues[currentDialogue]));
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
            float currentSpeed = Input.GetKey(KeyCode.S) ? fastSpeed : normalSpeed;
            yield return new WaitForSeconds(currentSpeed);
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
    // NUEVAS FUNCIONES PARA LAS DECISIONES
    // ---------------------------------------------------------

    void ShowChoices()
    {
        isChoosing = true;
        panelDecisiones.SetActive(true); // Aparece el menú
        UpdateChoiceUI(); // Pintamos de color la opción seleccionada
    }

    void HandleChoiceInput()
    {
        // Si presionas flecha arriba o abajo, cambiamos entre la opción 0 y la 1
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            currentChoice = (currentChoice == 0) ? 1 : 0;
            UpdateChoiceUI();
        }

        // Si presionas Enter (normal o el del teclado numérico) confirmas
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ConfirmChoice();
        }
    }

    void UpdateChoiceUI()
    {
        // Pintamos el texto de amarillo si está seleccionado, o blanco si no lo está
        textoOpcion1.color = (currentChoice == 0) ? colorSeleccionado : colorNormal;
        textoOpcion2.color = (currentChoice == 1) ? colorSeleccionado : colorNormal;
    }

    void ConfirmChoice()
    {
        // Según lo que hayamos elegido, cargamos la escena correspondiente
        if (currentChoice == 0)
        {
            SceneManager.LoadScene("DosDet");
        }
        else if (currentChoice == 1)
        {
            SceneManager.LoadScene("TresDet");
        }
    }
}
