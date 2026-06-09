using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserSource : MonoBehaviour
{
    public LineRenderer laserUV;

    public CristalBBO cristal;

    private bool activo = false;

    void Start()
    {
        laserUV.enabled = false;

        laserUV.startWidth = 0.03f;
        laserUV.endWidth = 0.03f;

        laserUV.material = new Material(Shader.Find("Sprites/Default"));

        Color uv = new Color(0.6f, 0f, 1f);

        laserUV.startColor = uv;
        laserUV.endColor = uv;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            activo = !activo;

            laserUV.enabled = activo;

            if (activo)
            {
                laserUV.SetPosition(0, transform.position);
                laserUV.SetPosition(1, cristal.transform.position);

                cristal.ActivarCristal();
            }
            else
            {
                cristal.ApagarCristal();
            }
        }
    }
}