using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Megumin;
using System.Net;

public class AutoGetIP : MonoBehaviour
{
    public TextMeshProUGUI WANIP;
    public TextMeshProUGUI GatewayIP;
    public TextMeshProUGUI LANIP;
    // Start is called before the first frame update
    void Start()
    {
        GetIP();
    }

    [Button]
    public async void GetIP()
    {
        var wan = await IPAddressExtension_A6F086FB3EE3403BB5033720C34DA414.GetWANIP();
        WANIP.text = wan?.ToString();

        var gateway = await IPAddressExtension_A6F086FB3EE3403BB5033720C34DA414.GetGateway(pingCheck: false);
        GatewayIP.text = gateway?.ToString();

        var lan = IPAddressExtension_A6F086FB3EE3403BB5033720C34DA414.GetLANIP();
        LANIP.text = lan?.ToString();
    }
}
