using UnityEngine;
using UnityEngine.Serialization;

public class LaserSource : MonoBehaviour
{
    [FormerlySerializedAs("laserUV")]
    public LineRenderer uvLaser;

    [FormerlySerializedAs("cristal")]
    public BboCrystal crystal;

    private bool isActive = false;

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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isActive = !isActive;

            uvLaser.enabled = isActive;

            if (isActive)
            {
                uvLaser.SetPosition(0, transform.position);
                uvLaser.SetPosition(1, crystal.transform.position);

                crystal.ActivateCrystal();
            }
            else
            {
                crystal.DeactivateCrystal();
            }
        }
    }
}
