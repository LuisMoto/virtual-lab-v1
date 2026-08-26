using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
// Scene navigation now goes through SceneController (Fase 1.1 del Plan Maestro)
// instead of calling UnityEngine.SceneManagement.SceneManager directly from here.

public class DialogueManager : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text dialogueText;
    [TextArea(3, 10)]
    [SerializeField] private string[] dialogues;

    [Header("Speed Settings")]
    [SerializeField] private float normalSpeed = 0.04f;

    [Header("VR Interaction (point + trigger)")]
    [Tooltip("Invisible button covering the whole DialogueBox. Point + trigger advances the dialogue.")]
    [FormerlySerializedAs("botonAvanzar")]
    [SerializeField] private Button advanceButton;

    [Header("Choice System")]
    [FormerlySerializedAs("panelDecisiones")]
    [SerializeField] private GameObject choicesPanel; // Container for the options
    [Tooltip("Button for the '2 Detectors' option (already has the Button component)")]
    [FormerlySerializedAs("botonOpcion1")]
    [SerializeField] private Button option1Button;
    [Tooltip("Button for the '3 Detectors' option (already has the Button component)")]
    [FormerlySerializedAs("botonOpcion2")]
    [SerializeField] private Button option2Button;

    private int _currentDialogue = 0;
    private bool _isTyping = false;
    private bool _isChoosing = false;
    private Coroutine _typingCoroutine;

    void Start()
    {
        // Make sure the panel starts hidden
        if (choicesPanel != null) choicesPanel.SetActive(false);

        // The whole flow is driven by the controllers' Ray Interactor:
        // point + trigger fires the onClick of the targeted button.
        // There is no keyboard dependency anywhere in the script.
        advanceButton.onClick.AddListener(OnAdvance);
        option1Button.onClick.AddListener(() => SelectOption(0));
        option2Button.onClick.AddListener(() => SelectOption(1));

        dialogueText.text = "";
        if (dialogues.Length > 0)
        {
            StartDialogue();
        }
    }

    void StartDialogue()
    {
        _typingCoroutine = StartCoroutine(TypeLine(dialogues[_currentDialogue]));
    }

    // Called by advanceButton.onClick (equivalent to the former KeyCode.A)
    void OnAdvance()
    {
        if (_isChoosing) return; // while choosing, this button is covered by the panel anyway

        if (_isTyping)
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
        _currentDialogue++;

        // If there are no more dialogues, show the choice screen
        if (_currentDialogue >= dialogues.Length)
        {
            ShowChoices();
            return;
        }

        _typingCoroutine = StartCoroutine(TypeLine(dialogues[_currentDialogue]));
    }

    IEnumerator TypeLine(string line)
    {
        _isTyping = true;
        dialogueText.text = "";

        foreach (char letter in line.ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(normalSpeed);
        }

        _isTyping = false;
    }

    void CompleteTextInstantly()
    {
        StopCoroutine(_typingCoroutine);
        dialogueText.text = dialogues[_currentDialogue];
        _isTyping = false;
    }

    // ---------------------------------------------------------
    // CHOICE SYSTEM
    // ---------------------------------------------------------

    void ShowChoices()
    {
        _isChoosing = true;
        choicesPanel.SetActive(true); // The menu appears

        // Highlighting the targeted option (formerly selectedColor/normalColor
        // + arrow-key navigation) is now handled by the Button component itself:
        // set its "Highlighted Color" to yellow from the Inspector.
        // When pointing with the controller, Unity tints it automatically, no extra code.
    }

    // Fase 1.1 del Plan Maestro: la navegación entre escenas ya no vive dispersa
    // aquí — se delega al singleton centralizado SceneController, que además
    // registra qué experimento eligió el usuario (SceneController.CurrentExperiment).
    void SelectOption(int option)
    {
        if (option == 0)
        {
            SceneController.Instance.LoadDosDetectores();
        }
        else
        {
            SceneController.Instance.LoadTresDetectores();
        }
    }
}
