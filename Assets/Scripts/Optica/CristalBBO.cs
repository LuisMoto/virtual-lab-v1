using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CristalBBO : MonoBehaviour
{
    public LineRenderer rayoPump;
    public LineRenderer rayoSignal;
    public LineRenderer rayoIdler;

    public DivisorHaz divisorHaz;

    public GameObject pared;

    public GameObject detTest;

    public void ActivarCristal()
    {
        rayoPump.enabled = true;
        rayoSignal.enabled = true;
        rayoIdler.enabled = true;

        Material mat = new Material(Shader.Find("Sprites/Default"));

        rayoPump.material = mat;
        rayoSignal.material = mat;
        rayoIdler.material = mat;

        Color uv = new Color(0.6f, 0f, 1f);

        rayoPump.startColor = uv;
        rayoPump.endColor = uv;

        rayoSignal.startColor = Color.red;
        rayoSignal.endColor = Color.red;

        rayoIdler.startColor = Color.red;
        rayoIdler.endColor = Color.red;

        rayoPump.startWidth = 0.025f;
        rayoPump.endWidth = 0.025f;

        rayoSignal.startWidth = 0.025f;
        rayoSignal.endWidth = 0.025f;

        rayoIdler.startWidth = 0.025f;
        rayoIdler.endWidth = 0.025f;

        // UV hacia pared
        rayoPump.SetPosition(0, transform.position);

        Vector3 paredDir = pared.transform.position;

        rayoPump.SetPosition(1, paredDir);

        // Signal hacia divisor
        rayoSignal.SetPosition(0, transform.position);

        rayoSignal.SetPosition(1, divisorHaz.transform.position);

        // Idler diagonal
        rayoIdler.SetPosition(0, transform.position);

        rayoIdler.SetPosition(1, detTest.transform.position);

        divisorHaz.ActivarDivisor();
    }

    public void ApagarCristal()
    {
        rayoPump.enabled = false;
        rayoSignal.enabled = false;
        rayoIdler.enabled = false;

        divisorHaz.ApagarDivisor();
    }
}