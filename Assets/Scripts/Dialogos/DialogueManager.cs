using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    public TMP_Text dialogueText;
    [TextArea(5, 10)] // Esto crea una caja de texto de mínimo 5 líneas y máximo 10
    public string[] dialogues;

    [Header("Configuración de Velocidad")]
    public float normalSpeed = 0.04f; // Tiempo entre cada letra
    public float fastSpeed = 0.01f;   // Tiempo al presionar 'S'

    private int currentDialogue = 0;
    private bool isTyping = false;
    private Coroutine typingCoroutine;

    void Start()
    {
        // Limpiamos el texto al inicio e iniciamos el primer diálogo
        dialogueText.text = "";
        if (dialogues.Length > 0)
        {
            StartDialogue();
        }
    }

    void Update()
    {
        // Detectar si presionamos "A"
        if (Input.GetKeyDown(KeyCode.A))
        {
            if (isTyping)
            {
                // UX Clásica: Si el jugador presiona A MIENTRAS se escribe, se autocompleta el texto de golpe
                CompleteTextInstantly();
            }
            else
            {
                // Si ya terminó de escribirse, pasamos al siguiente
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

        // Si ya no hay más diálogos, salimos
        if (currentDialogue >= dialogues.Length)
        {
            dialogueText.text = ""; // Aquí luego puedes apagar el Canvas si quieres
            return;
        }

        // Si hay más, escribimos el siguiente
        typingCoroutine = StartCoroutine(TypeLine(dialogues[currentDialogue]));
    }

    // Esta es la corrutina que hace el efecto máquina de escribir
    IEnumerator TypeLine(string line)
    {
        isTyping = true;
        dialogueText.text = ""; // Vaciamos la caja

        // Convertimos el string a un arreglo de letras y lo recorremos
        foreach (char letter in line.ToCharArray())
        {
            dialogueText.text += letter;

            // Evaluamos la velocidad en tiempo real por si el jugador mantiene 'S'
            float currentSpeed = Input.GetKey(KeyCode.S) ? fastSpeed : normalSpeed;

            yield return new WaitForSeconds(currentSpeed);
        }

        isTyping = false;
    }

    // Función para rellenar de golpe si el jugador se desespera
    void CompleteTextInstantly()
    {
        StopCoroutine(typingCoroutine); // Detenemos la máquina de escribir
        dialogueText.text = dialogues[currentDialogue]; // Ponemos el texto completo
        isTyping = false;
    }
}