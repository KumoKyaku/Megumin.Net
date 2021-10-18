//using Net.Remote;
//using System;
//using System.Buffers;
//using System.Collections.Generic;
//using System.Runtime.CompilerServices;
//using System.Text;

//namespace Megumin.Remote
//{
//    /// <summary>
//    /// 不懂别动，这里有坑
//    /// <para></para>
//    /// 如果没有修改，可以不用释放，因为没有创建防御性副本。如果修改值，会创建防御性副本，内存来自内存池，需要手动释放。
//    /// </summary>
//    public struct RoutingInformationModifier
//    {
//        public static readonly RoutingInformationModifier Empty = new RoutingInformationModifier(new byte[1] { 1 });

//        IMemoryOwner<byte> deepCopy;
//        readonly ReadOnlyMemory<byte> source;

//        ReadOnlySpan<byte> ActiveSpan {
//            [MethodImpl(MethodImplOptions.AggressiveInlining)]
//            get
//            {
//                if (deepCopy == null)
//                {
//                    return source.Span;
//                }
//                return deepCopy.Memory.Span;
//            }
//        }

//        public RoutingInformationModifier(in ReadOnlyMemory<byte> buffer)
//        {
//            source = buffer;
//            Length = buffer.Length;
//            deepCopy = null;
//            DeepCopy();
//        }

//        void DeepCopy()
//        {
//            deepCopy = BufferPool.Rent(source.Length + 14);
//            source.CopyTo(deepCopy.Memory);
//            if (Length < 2)
//            {
//                Mode = RouteMode.Null;
//                Cursor = -1;
//            }
//        }

//        public RoutingInformationModifier(int identifier) :
//            this(null)
//        {
//            Identifier = identifier;
//        }

//        ///一个byte分成2部分使用， 枚举最多支持8个选项
//        public RouteMode Mode
//        {
//            get
//            {
//                if (Length < 2)
//                {
//                    return RouteMode.Null;
//                }
//                return (RouteMode)(ActiveSpan[1] & 0b0000_0111);
//            }
//            set
//            {
//                if (deepCopy == null)
//                {
//                    DeepCopy();
//                }

//                deepCopy.Memory.Span[1] = (byte)((deepCopy.Memory.Span[1] & 0b1111_1000) | (int)value);
//            }
//        }

//        /// <summary>
//        /// 指针范围 0-31
//        /// </summary>
//        public int Cursor
//        {
//            get
//            {
//                if (Length < 2)
//                {
//                    return -1;
//                }
//                return ActiveSpan[1] >> 3;
//            }
//            set
//            {
//                if (deepCopy == null)
//                {
//                    DeepCopy();
//                }
//                deepCopy.Memory.Span[1] = (byte)((value << 3) | deepCopy.Memory.Span[1]);
//            }
//        }

//        public void Dispose()
//        {
//            deepCopy?.Dispose();
//        }

//        public void AddNode(IRemoteID receiver, IRemoteID sender)
//        {
//            if (Mode == RouteMode.Find)
//            {
//                if (deepCopy == null)
//                {
//                    DeepCopy();
//                }

//                int position = 6 + 8 * Cursor;
//                receiver.ID.WriteTo(deepCopy.Memory.Span.Slice(position));
//                sender.ID.WriteTo(deepCopy.Memory.Span.Slice(position + 4));
//                Cursor += 1;
//                Length += 8;
//            }
//        }

//        public int Length { get; private set; }

//        public int Identifier
//        {
//            get
//            {
//                return ActiveSpan.Slice(2).ReadInt();
//            }
//            set
//            {
//                if (deepCopy == null)
//                {
//                    DeepCopy();
//                }

//                value.WriteTo(deepCopy.Memory.Span.Slice(2));
//                if (Length < 6)
//                {
//                    Length = 6;
//                }
//            }
//        }

//        /// <summary>
//        /// 反转路由表流向
//        /// </summary>
//        public void ReverseDirection()
//        {
//            switch (Mode)
//            {
//                case RouteMode.Find:
//                    Mode = RouteMode.Backward;
//                    break;
//                case RouteMode.Backward:
//                    Mode = RouteMode.Forward;
//                    break;
//                case RouteMode.Forward:
//                    Mode = RouteMode.Backward;
//                    break;
//                case RouteMode.Null:
//                    break;
//                default:
//                    break;
//            }
//        }

//        public void MoveCursorNext()
//        {
//            if (Mode == RouteMode.Backward)
//            {
//                Cursor -= 1;
//            }

//            if (Mode == RouteMode.Forward)
//            {
//                Cursor += 1;
//            }
//        }

//        public int? Next => null;

//        public static implicit operator ReadOnlySpan<byte>(RoutingInformationModifier routeTable)
//        {
//            return routeTable.deepCopy.Memory.Span.Slice(0,routeTable.Length);
//        }

//        public static implicit operator RoutingInformationModifier(in ReadOnlyMemory<byte> buffer)
//        {
//            return new RoutingInformationModifier(buffer);
//        }
//    }

//    /// <summary>
//    /// 路由模式，最多支持8个选项，将来如果需要更多选项，要重写整个路由表格式
//    /// </summary>
//    public enum RouteMode
//    {
//        Null,
//        Find,
//        Backward,
//        Forward,
//    }
//}

//通过在包头添加路由表来实现消息转发,蠢到爆.
//每个节点或者分布式服务器只应该处理自己的事情,
//根据消息每个节点有自己查找下一个节点的方法,然后异步等待下一个节点返回消息结果,并返给上一个节点就行了.
//甚至无需检查消息类型.无需反序列化.


