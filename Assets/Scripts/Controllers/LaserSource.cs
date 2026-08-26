using UnityEngine;
using UnityEngine.Serialization;

public class LaserSource : MonoBehaviour
{
    [FormerlySerializedAs("laserUV")]
    [SerializeField] private LineRenderer uvLaser;

    [FormerlySerializedAs("cristal")]
    [SerializeField] private BboCrystal crystal;

    private bool _isActive = false;

    void Start()
    {
        uvLaser.enabled = false;

        uvLaser.startWidth = 0.03f;
        uvLaser.endWidth = 0.03f;

        uvLaser.material = new Material(Shader.Find("Sprites/Default"));

        Color uv = new Color(0.6f, 0f, 1f);

        uvLaser.startColor = uv;
        uvLaser.endColor = uv;
    }

    // Fase 3.3 del Plan Maestro (interacción física diegética): estos métodos públicos
    // reemplazan el toggle por teclado (antes: Input.GetKeyDown(KeyCode.Space) en Update()).
    // Quedan listos para conectarse desde el Inspector a un componente de colisión física
    // sobre el láser (p. ej. un collider/XR interactable que se agregará más adelante).

    /// <summary>Alterna el estado del láser: lo apaga si está prendido, lo prende si está apagado.</summary>
    public void ToggleLaser()
    {
        if (_isActive) TurnOff();
        else TurnOn();
    }

    /// <summary>Prende el láser y activa el cristal BBO al que apunta.</summary>
    public void TurnOn()
    {
        _isActive = true;
        uvLaser.enabled = true;

        uvLaser.SetPosition(0, transform.position);
        uvLaser.SetPosition(1, crystal.transform.position);

        crystal.ActivateCrystal();
    }

    /// <summary>Apaga el láser y desactiva el cristal BBO.</summary>
    public void TurnOff()
    {
        _isActive = false;
        uvLaser.enabled = false;

        crystal.DeactivateCrystal();
    }
}
