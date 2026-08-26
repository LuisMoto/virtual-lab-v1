using UnityEngine;
using UnityEngine.Serialization;

public class DetectorLogic : MonoBehaviour
{
    [FormerlySerializedAs("detTest")]
    [SerializeField] private GameObject witnessDetector;
    [FormerlySerializedAs("detSeñal")]
    [SerializeField] private GameObject signalDetector;
    [FormerlySerializedAs("detRefle")]
    [SerializeField] private GameObject reflectedDetector;

    /// <summary>
    /// Activates the visual state of the detectors for this optical path.
    /// </summary>
    public void ActivateDetectors()
    {
        Debug.Log("Witness detector active");
        Debug.Log("Signal detector active");
        Debug.Log("Reflected detector active");
    }
}
