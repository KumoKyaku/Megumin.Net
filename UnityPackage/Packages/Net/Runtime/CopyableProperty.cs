using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using System.Threading.Tasks;

namespace Megumin
{
    public class CopyableProperty : MonoBehaviour
    {
        public TextMeshProUGUI ButtonName;
        public TextMeshProUGUI PropertyValue;
        public TextMeshProUGUI Note;
        public void Copy()
        {
            if (!PropertyValue)
            {
                return;
            }
            ShowCopyed();
            GUIUtility.systemCopyBuffer = PropertyValue.text;
        }

        private async void ShowCopyed()
        {
            const string COPYED = "Copyed !!!";

            if (ButtonName.text == COPYED)
            {
                return;
            }

            var old = ButtonName.text;
            ButtonName.text = COPYED;

            await Task.Delay(1000);
            ButtonName.text = old;
        }
    }
}



