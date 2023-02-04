using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using TMPro;
using UnityEngine;

namespace Megumin
{
    public class NetworkInterfacInfo : MonoBehaviour
    {
        public TMP_Dropdown NetworkInterfac_Dropdown;
        public Transform PropertyParent;
        public CopyableProperty Template;

        private void Awake()
        {
            NetworkInterfac_Dropdown.onValueChanged.AddListener(OnValueChanged);
        }

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            NetworkInterfac_Dropdown.ClearOptions();
            List<string> strings = new List<string>();
            foreach (var item in NetworkInterface.GetAllNetworkInterfaces())
            {
                strings.Add(item.Name);
            }
            NetworkInterfac_Dropdown.AddOptions(strings);
            OnValueChanged(0);
        }

        public void OnValueChanged(int index)
        {
            DestroyAllProp();
            var nets = NetworkInterface.GetAllNetworkInterfaces();
            if (index >= 0 && index < nets.Length)
            {
                var net = nets[index];
                OnSelectNetworkInterface(net);
            }
        }

        private void OnSelectNetworkInterface(NetworkInterface net)
        {
            DestroyAllProp();
            Log(net);

            InitProperty("Name", net.Name);
            InitProperty("Description", net.Description);
            InitProperty("NetworkInterfaceType", net.NetworkInterfaceType);
            InitProperty("OperationalStatus", net.OperationalStatus);

            var ipProperties = net.GetIPProperties();
            if (ipProperties != null)
            {
                foreach (var ip in ipProperties.UnicastAddresses)
                {
                    string note = GetNetworkAreaType(ip.Address);
                    InitProperty("UnicastAddresses", ip.Address, note);
                }

                foreach (var gateway in ipProperties.GatewayAddresses)
                {
                    InitProperty("GatewayAddresses", gateway.Address);
                }
            }

            InitProperty("Id", net.Id);
            InitProperty("IsReceiveOnly", net.IsReceiveOnly);
            InitProperty("SupportsMulticast", net.SupportsMulticast);
            //InitProperty("Speed", net.Speed);
        }

        static string GetNetworkAreaType(IPAddress address)
        {
            string note = "Unknown";

#if MEGUMIN_EXPLOSION4UNITY
            if (address.IsLAN())
            {
                note = "Local Area Network";
            }
            else
            {
                note = "Wide Area Network";
            }
#endif

            return note;
        }

        private void DestroyAllProp()
        {
            for (int i = 0; i < PropertyParent.childCount; i++)
            {
                var child = PropertyParent.GetChild(i);
                Destroy(child.gameObject);
            }
        }

        private void InitProperty(string name, object value, string note = null)
        {
            var prop = Instantiate(Template, PropertyParent);
            prop.ButtonName.text = name;
            prop.PropertyValue.text = value.ToString();
            prop.Note.text = note;
        }

        public void DebugLogNetworkInterfaceInfo()
        {
            foreach (var item in NetworkInterface.GetAllNetworkInterfaces())
            {
                Log(item);
            }
        }

        public static void Log(NetworkInterface item)
        {
            string info = "";
            info += $"Name : {item.Name}\n";
            info += $"Description : {item.Description}\n";
            info += $"Id : {item.Id}\n";
            info += $"Speed : {item.Speed}\n";
            info += $"IsReceiveOnly : {item.IsReceiveOnly}\n";
            info += $"NetworkInterfaceType : {item.NetworkInterfaceType}\n";
            info += $"OperationalStatus : {item.OperationalStatus}\n";
            info += $"SupportsMulticast : {item.SupportsMulticast}\n";

            var ipProperties = item.GetIPProperties();
            if (ipProperties != null)
            {
                info += $"ipProperties: \n";
                info += $"   UnicastAddresses: \n";
                foreach (var ip in ipProperties.UnicastAddresses)
                {
                    info += $"       Address: {ip.Address}    {GetNetworkAreaType(ip.Address)}\n";
                    info += $"       AddressFamily: {ip.Address.AddressFamily}\n";
                    info += $"       \n";
                }

                info += $"   GatewayAddresses: \n";
                foreach (var gateway in ipProperties.GatewayAddresses)
                {
                    info += $"       Address: {gateway.Address}\n";
                    info += $"       AddressFamily: {gateway.Address.AddressFamily}\n";
                    info += $"       \n";
                }
            }

            Debug.Log($"Íø¿¨ {info}");
        }
    }
}




