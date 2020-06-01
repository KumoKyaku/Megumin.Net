using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Megumin.Message;
using Net.Remote;
//using IRemoteDic = Megumin.IDictionary<int, System.Net.EndPoint, Net.Remote.IRemote>;

namespace Megumin.Remote
{
    ///// <summary>
    ///// 客户端不使用这个类，会发生Key冲突
    ///// </summary>
    //public static class RemotePool
    //{
    //    static RemotePool()
    //    {
    //        MainThreadScheduler.Add(StaticUpdate);
    //    }

    //    /// <summary>
    //    /// remote 在构造函数中自动添加到RemoteDic 中,需要手动移除 或者调用<see cref="IConnectable.Disconnect(bool)"/> + <see cref="MainThreadScheduler.Update(double)"/>移除。
    //    /// </summary>
    //    static IRemoteDic remoteDic = new K2Dictionary<int, EndPoint, IRemote>();
    //    /// <summary>
    //    /// 添加队列，防止多线程阻塞
    //    /// </summary>
    //    static readonly ConcurrentQueue<IRemote> tempAddQ = new ConcurrentQueue<IRemote>();

    //    public static readonly CoolDownTime UpdateDelta = new CoolDownTime();
    //    public static readonly CoolDownTime CheckRemoveUpdateDelta = new CoolDownTime() { MinDelta = TimeSpan.FromSeconds(2) };
    //    static void StaticUpdate(double delta)
    //    {
    //        if (UpdateDelta.CoolDown)
    //        {
    //            while (tempAddQ.Count > 0)
    //            {
    //                if (tempAddQ.TryDequeue(out var remote))
    //                {
    //                    if (remote != null && remote.IsVaild)
    //                    {
    //                        if (remoteDic.ContainsKey(remote.Guid) || remoteDic.ContainsKey(remote.RemappedEndPoint))
    //                        {
    //                            ///理论上不会冲突
    //                            Console.WriteLine($"remoteDic 键值冲突");
    //                        }
    //                        remoteDic[remote.Guid,remote.RemappedEndPoint] = remote;
    //                    }
    //                }
    //            }
    //        }

    //        if (CheckRemoveUpdateDelta.CoolDown)
    //        {
    //            ///移除释放的连接
    //            remoteDic.RemoveAll(r => !r.Value.IsVaild);
    //        }
    //    }

    //    /// <summary>
    //    /// 使用 <see cref="InterlockedID{IRemote}.NewID"/> 初始化你的<see cref="IRemote.Guid"/>，防止和框架底层ID冲突。
    //    /// </summary>
    //    /// <param name="remote"></param>
    //    public static void Add(IRemote remote)
    //    {
    //        tempAddQ.Enqueue(remote);
    //    }

    //    ///// <summary>
    //    ///// 使用 <see cref="InterlockedID{IRemote}.NewID"/> 初始化你的<see cref="IRemote.Guid"/>，防止和框架底层ID冲突。
    //    ///// </summary>
    //    ///// <param name="remote"></param>
    //    //public static void AddToPool(this IRemote remote)
    //    //{
    //    //    Add(remote);
    //    //}

    //    public static bool TryGet(int Guid, out IRemote remote)
    //    {
    //        return remoteDic.TryGetValue(Guid, out remote);
    //    }

    //    public static bool TryGet(EndPoint ep, out IRemote remote)
    //    {
    //        return remoteDic.TryGetValue(ep, out remote);
    //    }

    //    #region BroadCast

    //    /// <summary>
    //    /// 广播
    //    /// </summary>
    //    /// <param name="message"></param>
    //    /// <param name="remotes"></param>
    //    public static void BroadCastAsync<T>(T message, params IBroadCastSend[] remotes)
    //    {
    //        BroadCastAsync(message, remotes as IEnumerable<IBroadCastSend>);
    //    }

    //    public static void BroadCastAsync<T>(T message, IEnumerable<IBroadCastSend> remotes)
    //    {

    //        var msgBuffer = MessageLUT.Serialize(0, message);

    //        ///这里需要测试
    //        Task.Run(() =>
    //        {
    //            Parallel.ForEach(remotes,
    //            async (item) =>
    //            {
    //                //(Action<INetRemote>)
    //                await item?.BroadCastSendAsync(msgBuffer);
    //            });
    //        }).ContinueWith((t) =>
    //        {
    //            ///这里需要测试
    //            BufferPool.Push(msgBuffer.Array);
    //        });
    //    }


    //    #endregion


    //}
}
