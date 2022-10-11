using Megumin.Message;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Megumin.Remote
{
    public class UdpRemoteMessageDefine
    {
        public const byte UdpAuthRequest = 10;
        public const byte UdpAuthResponse = 20;
        /// <summary>
        /// 低级别消息，没有rpc等高级功能，不经过Kcp等附加协议，直接处理
        /// </summary>
        public const byte LLData = 30;
        public const byte UdpData = 40;
        public const byte KcpData = 50;
    }

    /// <summary>
    /// Udp认证请求
    /// </summary>
    public struct UdpAuthRequest
    {
        public const int Length = 21;
        public Guid Guid;
        public int Option;

        public void Serialize(Span<byte> span)
        {
            span[0] = UdpRemoteMessageDefine.UdpAuthRequest;
            Guid.Write(span.Slice(1));
            Option.Write(span.Slice(17));
        }

        public static UdpAuthRequest Deserialize(Span<byte> span)
        {
            if (span[0] != UdpRemoteMessageDefine.UdpAuthRequest)
            {
                throw new FormatException();
            }
            UdpAuthRequest req = new UdpAuthRequest();
            req.Guid = span.Slice(1).ReadGuid();
            req.Option = span.Slice(17).ReadInt();
            return req;
        }
    }

    public class UdpAuthHelper
    {
        public readonly Dictionary<IPEndPoint, TaskCompletionSource<UdpAuthResponse>> authing
            = new Dictionary<IPEndPoint, TaskCompletionSource<UdpAuthResponse>>();

        public readonly object authingLock = new object();
        public Task<UdpAuthResponse> Auth(IPEndPoint endPoint, UdpClient client)
        {
            lock (authingLock)
            {
                if (!authing.TryGetValue(endPoint, out var source))
                {
                    source = new TaskCompletionSource<UdpAuthResponse>();
                    authing.Add(endPoint, source);

                    UdpAuthRequest reqest = new UdpAuthRequest();
                    reqest.Guid = Guid.NewGuid();
                    //创建认证消息
                    byte[] buffer = new byte[UdpAuthRequest.Length];
                    reqest.Serialize(buffer);

                    Task.Run(async () =>
                    {
                        //120秒后超时，防止内存泄露
                        await Task.Delay(1000 * 120);
                        authing.Remove(endPoint);
                    });

                    try
                    {
                        client.Send(buffer, buffer.Length, endPoint);
                    }
                    catch (Exception e)
                    {
                        //忽略所有异常
                        Console.WriteLine(e);
                    }
                }

                return source.Task;
            }
        }

        public Task<UdpAuthResponse> Auth(IPEndPoint endPoint, Socket client)
        {
            lock (authingLock)
            {
                if (!authing.TryGetValue(endPoint, out var source))
                {
                    source = new TaskCompletionSource<UdpAuthResponse>();
                    authing.Add(endPoint, source);

                    UdpAuthRequest reqest = new UdpAuthRequest();
                    reqest.Guid = Guid.NewGuid();
                    //创建认证消息
                    byte[] buffer = new byte[UdpAuthRequest.Length];
                    reqest.Serialize(buffer);

                    Task.Run(async () =>
                    {
                        //120秒后超时，防止内存泄露
                        await Task.Delay(1000 * 120);
                        authing.Remove(endPoint);
                    });

                    try
                    {
                        client.SendTo(buffer, 0, buffer.Length, SocketFlags.None, endPoint);
                    }
                    catch (Exception e)
                    {
                        //忽略所有异常
                        Console.WriteLine(e);
                    }
                }

                return source.Task;
            }
        }

        public void DealAnswerBuffer(IPEndPoint endPoint, Span<byte> buffer)
        {
            lock (authingLock)
            {
                if (authing.TryGetValue(endPoint, out var source))
                {
                    authing.Remove(endPoint);
                    var answer = UdpAuthResponse.Deserialize(buffer);
                    source.TrySetResult(answer);
                }
            }
        }
    }

    /// <summary>
    /// Udp认证应答
    /// </summary>
    public struct UdpAuthResponse
    {
        public const int Length = 26;
        [Obsolete("废弃", true)]
        public bool IsNew;
        public Guid Guid;
        public int Password;
        public int KcpChannel;

        public void Serialize(Span<byte> span)
        {
            span[0] = UdpRemoteMessageDefine.UdpAuthResponse;
            //span[1] = (byte)(IsNew ? 1 : 0);
            Guid.Write(span.Slice(2));
            Password.Write(span.Slice(18));
            KcpChannel.Write(span.Slice(22));
        }

        public static UdpAuthResponse Deserialize(Span<byte> span)
        {
            if (span[0] != UdpRemoteMessageDefine.UdpAuthResponse)
            {
                throw new FormatException();
            }
            UdpAuthResponse resp = new UdpAuthResponse();
            //resp.IsNew = span[1] != 0;
            resp.Guid = span.Slice(2).ReadGuid();
            resp.Password = span.Slice(18).ReadInt();
            resp.KcpChannel = span.Slice(22).ReadInt();
            return resp;
        }
    }

}
