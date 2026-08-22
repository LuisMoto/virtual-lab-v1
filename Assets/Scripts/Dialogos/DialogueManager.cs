using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using UnityEngine.SceneManagement; // Required to switch between scenes

public class DialogueManager : MonoBehaviour
{
    [Header("Text")]
    public TMP_Text dialogueText;
    [TextArea(3, 10)]
    public string[] dialogues;

    [Header("Speed Settings")]
    public float normalSpeed = 0.04f;

    [Header("VR Interaction (point + trigger)")]
    [Tooltip("Invisible button covering the whole DialogueBox. Point + trigger advances the dialogue.")]
    [FormerlySerializedAs("botonAvanzar")]
    public Button advanceButton;

    [Header("Choice System")]
    [FormerlySerializedAs("panelDecisiones")]
    public GameObject choicesPanel; // Container for the options
    [Tooltip("Button for the '2 Detectors' option (already has the Button component)")]
    [FormerlySerializedAs("botonOpcion1")]
    public Button option1Button;
    [Tooltip("Button for the '3 Detectors' option (already has the Button component)")]
    [FormerlySerializedAs("botonOpcion2")]
    public Button option2Button;

    private int currentDialogue = 0;
    private bool isTyping = false;
    private bool isChoosing = false;
    private Coroutine typingCoroutine;

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
        typingCoroutine = StartCoroutine(TypeLine(dialogues[currentDialogue]));
    }

    // Called by advanceButton.onClick (equivalent to the former KeyCode.A)
    void OnAdvance()
    {
        if (isChoosing) return; // while choosing, this button is covered by the panel anyway

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

        // If there are no more dialogues, show the choice screen
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
    // CHOICE SYSTEM
    // ---------------------------------------------------------

    void ShowChoices()
    {
        isChoosing = true;
        choicesPanel.SetActive(true); // The menu appears

        // Highlighting the targeted option (formerly selectedColor/normalColor
        // + arrow-key navigation) is now handled by the Button component itself:
        // set its "Highlighted Color" to yellow from the Inspector.
        // When pointing with the controller, Unity tints it automatically, no extra code.
    }

    void SelectOption(int option)
    {
        if (option == 0)
        {
            SceneManager.LoadScene("DosDet");
        }
        else
        {
            SceneManager.LoadScene("TresDet");
        }
    }
}
