using UnityEngine;
using UnityEngine.Serialization;

public class BeamSplitter : MonoBehaviour
{
    [FormerlySerializedAs("rayoTrans")]
    [SerializeField] private LineRenderer transmittedBeam;
    [FormerlySerializedAs("rayoRefle")]
    [SerializeField] private LineRenderer reflectedBeam;

    [FormerlySerializedAs("detSeñal")]
    [SerializeField] private GameObject signalDetector;
    [FormerlySerializedAs("detRefle")]
    [SerializeField] private GameObject reflectedDetector;

    [FormerlySerializedAs("dLogic")]
    [SerializeField] private DetectorLogic detectorLogic;

    /// <summary>
    /// Enables the transmitted/reflected beams toward their detectors and activates them.
    /// </summary>
    public void ActivateSplitter()
    {
        transmittedBeam.enabled = true;
        reflectedBeam.enabled = true;

        Material mat = new Material(Shader.Find("Sprites/Default"));

        transmittedBeam.material = mat;
        reflectedBeam.material = mat;

        transmittedBeam.startColor = Color.red;
        transmittedBeam.endColor = Color.red;

        reflectedBeam.startColor = Color.red;
        reflectedBeam.endColor = Color.red;

        transmittedBeam.startWidth = 0.02f;
        transmittedBeam.endWidth = 0.02f;

        reflectedBeam.startWidth = 0.02f;
        reflectedBeam.endWidth = 0.02f;

        // Transmitted beam
        transmittedBeam.SetPosition(0, transform.position);
        transmittedBeam.SetPosition(1, signalDetector.transform.position);

        // Reflected beam
        reflectedBeam.SetPosition(0, transform.position);
        reflectedBeam.SetPosition(1, reflectedDetector.transform.position);

        detectorLogic.ActivateDetectors();
    }

    /// <summary>
    /// Disables the transmitted/reflected beams.
    /// </summary>
    public void DeactivateSplitter()
    {
        transmittedBeam.enabled = false;
        reflectedBeam.enabled = false;
    }
}
