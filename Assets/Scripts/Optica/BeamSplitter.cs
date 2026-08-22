using UnityEngine;
using UnityEngine.Serialization;

public class BeamSplitter : MonoBehaviour
{
    [FormerlySerializedAs("rayoTrans")]
    public LineRenderer transmittedBeam;
    [FormerlySerializedAs("rayoRefle")]
    public LineRenderer reflectedBeam;

    [FormerlySerializedAs("detSeñal")]
    public GameObject signalDetector;
    [FormerlySerializedAs("detRefle")]
    public GameObject reflectedDetector;

    [FormerlySerializedAs("dLogic")]
    public DetectorLogic detectorLogic;

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

    public void DeactivateSplitter()
    {
        transmittedBeam.enabled = false;
        reflectedBeam.enabled = false;
    }
}
