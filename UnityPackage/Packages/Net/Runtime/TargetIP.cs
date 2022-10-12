using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Megumin;
using System.Net;

public class TargetIP : MonoBehaviour
{
    public TMP_InputField Target;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    public TextMeshProUGUI WANIP;
    [Button]
    public void SetWan()
    {
        Target.text = WANIP.text;
    }

    public TextMeshProUGUI GatewayIP;
    [Button]
    public void SetGateway()
    {
        Target.text = GatewayIP.text;
    }

    public TextMeshProUGUI LANIP;
    [Button]
    public void SetLAN()
    {
        Target.text= LANIP.text;
    }

    [Button]
    public void Loopback()
    {
        Target.text = IPAddress.Loopback.ToString();
    }

    public void Any()
    {
        Target.text = IPAddress.Any.ToString();
    }

    public void IPV6Loopback()
    {
        Target.text = IPAddress.IPv6Loopback.ToString();
    }

    public void IPV6Any()
    {
        Target.text = IPAddress.IPv6Any.ToString();
    }
    public void IP_TestLAN()
    {
        Target.text = "192.168.1.150";
    }

    public void IP_TestWAN()
    {
        Target.text = "47.102.202.48";
    }

    public void Paste()
    {
        Target.text = GUIUtility.systemCopyBuffer;
    }
}
