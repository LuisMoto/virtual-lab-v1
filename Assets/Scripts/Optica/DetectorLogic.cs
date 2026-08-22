using UnityEngine;
using UnityEngine.Serialization;

public class DetectorLogic : MonoBehaviour
{
    [FormerlySerializedAs("detTest")]
    public GameObject witnessDetector;
    [FormerlySerializedAs("detSeñal")]
    public GameObject signalDetector;
    [FormerlySerializedAs("detRefle")]
    public GameObject reflectedDetector;

    public void ActivateDetectors()
    {
        Debug.Log("Witness detector active");
        Debug.Log("Signal detector active");
        Debug.Log("Reflected detector active");
    }
}
