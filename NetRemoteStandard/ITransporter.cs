using System;
using System.Collections.Generic;
using System.Text;

namespace Net.Remote
{

    public interface ITransporter
    {
        void Send(int rpcID, object message, object options = null);
    }
}



