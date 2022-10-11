﻿using Megumin.DCS;
using Megumin.Remote.Test;
using Megumin.Remote;
using Net.Remote;
using System;
using System.Threading.Tasks;

namespace DemoServer
{
    internal class GateService : IService
    {
        public int GUID { get; set; }

        TcpRemoteListenerOld listener = new TcpRemoteListenerOld(Config.MainPort);

        public void Start()
        {
            StartListenAsync();
        }

        public async void StartListenAsync()
        {
            var remote = await listener.ListenAsync(Create);
            StartListenAsync();
            Console.WriteLine($"建立连接");
        }

        GateRemote Create()
        {
            return new GateRemote();
        }

        class GateRemote : TcpRemote
        {
            //protected async override ValueTask<object> OnReceive(object message)
            //{
            //    switch (message)
            //    {
            //        case string str:
            //            return $"{str} world";
            //        case Login2Gate login:

            //            Console.WriteLine($"客户端登陆请求：{login.Account}-----{login.Password}");

            //            Login2GateResult resp = new Login2GateResult();
            //            resp.IsSuccess = true;
            //            return resp;
            //        default:
            //            break;
            //    }
            //    return null;
            //}
        }

        public void Update(double deltaTime)
        {
            
        }
    }
}