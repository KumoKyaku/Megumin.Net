using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Megumin;
using System.Net;
using System.Net.NetworkInformation;

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
    public void Test()
    {
        //Debug.Log(UnityEditorInternal.InternalEditorUtility.GetFullUnityVersion());
    }

    [Button]
    public async void GetIP()
    {
        var wan = await IPAddressExtension_A6F086FB3EE3403BB5033720C34DA414.GetWANIP();
        WANIP.text = wan?.ToString();


        var gateway = await IPAddressExtension_A6F086FB3EE3403BB5033720C34DA414.GetGateway(pingCheck: false);
        GatewayIP.text = gateway?.ToString();


        var laninfo = IPAddressExtension_A6F086FB3EE3403BB5033720C34DA414.GetLANInformation();
        if (laninfo != null)
        {
            LANIP.text = laninfo.Address.ToString();
        }
        else
        {
            var lan = IPAddressExtension_A6F086FB3EE3403BB5033720C34DA414.GetLANIP();
            LANIP.text = lan?.ToString();
        }
    }

    public void GetIPV6()
    {
        foreach (var item in NetworkInterface.GetAllNetworkInterfaces())
        {
            Debug.Log($"Íø¿¨ {item.Name}");
            NetworkInterfaceType type1 = NetworkInterfaceType.Wireless80211;
            NetworkInterfaceType type2 = NetworkInterfaceType.Ethernet;
            NetworkInterfaceType type3 = NetworkInterfaceType.Wman;
            NetworkInterfaceType type4 = NetworkInterfaceType.Wwanpp;
            NetworkInterfaceType type5 = NetworkInterfaceType.Wwanpp2;
            if (item.NetworkInterfaceType == type1 ||
                item.NetworkInterfaceType == type2 ||
                item.NetworkInterfaceType == type3 ||
                item.NetworkInterfaceType == type4 ||
                item.NetworkInterfaceType == type5)
            {
                if (item.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (var ip in item.GetIPProperties().UnicastAddresses)
                    {
                        Debug.Log(ip.Address.ToString());
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                        {
                            WANIP.text = ip.Address.ToString();
                            break;
                        }
                    }
                }
            }
        }
    }
}
