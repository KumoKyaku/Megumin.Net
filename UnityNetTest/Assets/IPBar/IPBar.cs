using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class IPBar : MonoBehaviour
{
    public TextMeshProUGUI Label;
    public TextMeshProUGUI IP;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void Copy()
    {
        GUIUtility.systemCopyBuffer = IP.text;
    }
}
