using UnityEngine;
using UnityEngine.Serialization;

public class BboCrystal : MonoBehaviour
{
    [FormerlySerializedAs("rayoPump")]
    [SerializeField] private LineRenderer pumpBeam;
    [FormerlySerializedAs("rayoSignal")]
    [SerializeField] private LineRenderer signalBeam;
    [FormerlySerializedAs("rayoIdler")]
    [SerializeField] private LineRenderer idlerBeam;

    [FormerlySerializedAs("divisorHaz")]
    [SerializeField] private BeamSplitter beamSplitter;

    [FormerlySerializedAs("pared")]
    [SerializeField] private GameObject wall;

    [FormerlySerializedAs("detTest")]
    [SerializeField] private GameObject witnessDetector;

    /// <summary>
    /// Enables the pump/signal/idler beams and cascades activation into the beam splitter.
    /// </summary>
    public void ActivateCrystal()
    {
        pumpBeam.enabled = true;
        signalBeam.enabled = true;
        idlerBeam.enabled = true;

        Material mat = new Material(Shader.Find("Sprites/Default"));

        pumpBeam.material = mat;
        signalBeam.material = mat;
        idlerBeam.material = mat;

        Color uv = new Color(0.6f, 0f, 1f);

        pumpBeam.startColor = uv;
        pumpBeam.endColor = uv;

        signalBeam.startColor = Color.red;
        signalBeam.endColor = Color.red;

        idlerBeam.startColor = Color.red;
        idlerBeam.endColor = Color.red;

        pumpBeam.startWidth = 0.025f;
        pumpBeam.endWidth = 0.025f;

        signalBeam.startWidth = 0.025f;
        signalBeam.endWidth = 0.025f;

        idlerBeam.startWidth = 0.025f;
        idlerBeam.endWidth = 0.025f;

        // Pump beam toward the wall
        pumpBeam.SetPosition(0, transform.position);

        Vector3 wallPosition = wall.transform.position;

        pumpBeam.SetPosition(1, wallPosition);

        // Signal beam toward the splitter
        signalBeam.SetPosition(0, transform.position);

        signalBeam.SetPosition(1, beamSplitter.transform.position);

        // Idler beam (diagonal)
        idlerBeam.SetPosition(0, transform.position);

        idlerBeam.SetPosition(1, witnessDetector.transform.position);

        beamSplitter.ActivateSplitter();
    }

    /// <summary>
    /// Disables the pump/signal/idler beams and cascades deactivation into the beam splitter.
    /// </summary>
    public void DeactivateCrystal()
    {
        pumpBeam.enabled = false;
        signalBeam.enabled = false;
        idlerBeam.enabled = false;

        beamSplitter.DeactivateSplitter();
    }
}
