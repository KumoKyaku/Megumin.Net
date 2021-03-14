using System;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Remote
{

    public class UdpRemoteMessageDefine
    {
        public const byte UdpAuthRequest = 10;
        public const byte UdpAuthResponse = 20;
        /// <summary>
        /// 低级别消息，没有rpc等高级功能，不经过Kcp等附加协议，直接处理
        /// </summary>
        public const byte LLMsg = 30;
        public const byte Common = 40;
    }

    /// <summary>
    /// Udp认证请求
    /// </summary>
    public struct UdpAuthRequest
    {
        public const int Length = 21;
        public Guid Guid;
        public int Password;

        public void Serialize(Span<byte> span)
        {
            span[0] = UdpRemoteMessageDefine.UdpAuthRequest;
            Guid.WriteTo(span.Slice(1));
            Password.WriteTo(span.Slice(17));
        }

        public static UdpAuthRequest Deserialize(Span<byte> span)
        {
            if (span[0] != UdpRemoteMessageDefine.UdpAuthRequest)
            {
                throw new FormatException();
            }
            UdpAuthRequest req = new UdpAuthRequest();
            req.Guid = span.Slice(1).ReadGuid();
            req.Password = span.Slice(17).ReadInt();
            return req;
        }
    }

    /// <summary>
    /// Udp认证应答
    /// </summary>
    public struct UdpAuthResponse
    {
        public const int Length = 26;
        public bool IsNew;
        public Guid Guid;
        public int Password;
        public int KcpChannel;

        public void Serialize(Span<byte> span)
        {
            span[0] = UdpRemoteMessageDefine.UdpAuthResponse;
            span[1] = (byte)(IsNew ? 1 : 0);
            Guid.WriteTo(span.Slice(2));
            Password.WriteTo(span.Slice(18));
            KcpChannel.WriteTo(span.Slice(22));
        }

        public static UdpAuthResponse Deserialize(Span<byte> span)
        {
            if (span[0] != UdpRemoteMessageDefine.UdpAuthResponse)
            {
                throw new FormatException();
            }
            UdpAuthResponse resp = new UdpAuthResponse();
            resp.IsNew = span[1] != 0;
            resp.Guid = span.Slice(2).ReadGuid();
            resp.Password = span.Slice(18).ReadInt();
            resp.KcpChannel = span.Slice(22).ReadInt();
            return resp;
        }
    }

}
