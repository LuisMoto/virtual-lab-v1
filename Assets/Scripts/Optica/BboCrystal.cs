using UnityEngine;
using UnityEngine.Serialization;

public class BboCrystal : MonoBehaviour
{
    [FormerlySerializedAs("rayoPump")]
    public LineRenderer pumpBeam;
    [FormerlySerializedAs("rayoSignal")]
    public LineRenderer signalBeam;
    [FormerlySerializedAs("rayoIdler")]
    public LineRenderer idlerBeam;

    [FormerlySerializedAs("divisorHaz")]
    public BeamSplitter beamSplitter;

    [FormerlySerializedAs("pared")]
    public GameObject wall;

    [FormerlySerializedAs("detTest")]
    public GameObject witnessDetector;

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

    public void DeactivateCrystal()
    {
        pumpBeam.enabled = false;
        signalBeam.enabled = false;
        idlerBeam.enabled = false;

        beamSplitter.DeactivateSplitter();
    }
}
