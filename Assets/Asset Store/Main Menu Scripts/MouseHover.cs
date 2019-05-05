using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MouseHover : MonoBehaviour 
{
    Color colorStart;
    Color colorEnd;

    void Start()
    {
        colorStart = Color.green;
        colorEnd = Color.red;
    }

    void OnMouseEnter()
    {
        GetComponent<TextMesh>().color = colorEnd;
    }

    void OnMouseExit()
    {
        GetComponent<TextMesh>().color = colorStart;
    }

}
