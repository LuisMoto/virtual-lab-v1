using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DLogic : MonoBehaviour
{
    public GameObject detTest;
    public GameObject detSeñal;
    public GameObject detRefle;

    public void ActivarDetectores()
    {
        Debug.Log("Detector Testigo activo");
        Debug.Log("Detector Señal activo");
        Debug.Log("Detector Reflejado activo");
    }
}