using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DivisorHaz : MonoBehaviour
{
    public LineRenderer rayoTrans;
    public LineRenderer rayoRefle;

    public GameObject detSeñal;
    public GameObject detRefle;

    public DLogic dLogic;

    public void ActivarDivisor()
    {
        rayoTrans.enabled = true;
        rayoRefle.enabled = true;

        Material mat = new Material(Shader.Find("Sprites/Default"));

        rayoTrans.material = mat;
        rayoRefle.material = mat;

        rayoTrans.startColor = Color.red;
        rayoTrans.endColor = Color.red;

        rayoRefle.startColor = Color.red;
        rayoRefle.endColor = Color.red;

        rayoTrans.startWidth = 0.02f;
        rayoTrans.endWidth = 0.02f;

        rayoRefle.startWidth = 0.02f;
        rayoRefle.endWidth = 0.02f;

        // TRANSMITIDO
        rayoTrans.SetPosition(0, transform.position);

        rayoTrans.SetPosition(1, detSeñal.transform.position);

        // REFLEJADO
        rayoRefle.SetPosition(0, transform.position);

        rayoRefle.SetPosition(1, detRefle.transform.position);

        dLogic.ActivarDetectores();
    }

    public void ApagarDivisor()
    {
        rayoTrans.enabled = false;
        rayoRefle.enabled = false;
    }
}