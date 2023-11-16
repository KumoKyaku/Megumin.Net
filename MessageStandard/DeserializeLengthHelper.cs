using System.Threading;

namespace Megumin.Remote
{
    /// <summary>
    /// 用于反序列化时获取长度
    /// </summary>
    public interface IDeserializeLengthWriter
    {
        int Length { set; }
    }

    /// <summary>
    /// 用于反序列化时获取长度
    /// </summary>
    public class DeserializeLengthHelper : IDeserializeLengthWriter
    {
        //https://learn.microsoft.com/en-us/dotnet/api/system.threadstaticattribute?redirectedfrom=MSDN&view=net-6.0
        //[ThreadStatic]
        //static DeserializeLengthHelper defaulthelper;
        //public static DeserializeLengthHelper Default
        //{
        //    get
        //    {
        //        if (defaulthelper == null)
        //        {
        //            defaulthelper = new DeserializeLengthHelper();
        //        }
        //        return defaulthelper;
        //    }
        //}

        //https://stackoverflow.com/questions/18333885/threadstatic-v-s-threadlocalt-is-generic-better-than-attribute
        static readonly ThreadLocal<DeserializeLengthHelper> defaulthelper
            = new ThreadLocal<DeserializeLengthHelper>(static () => new DeserializeLengthHelper());

        public static DeserializeLengthHelper Default => defaulthelper.Value;

        //仅将Length标记为ThreadStatic是不够的，可能在一个线程写，在另一个线程读，造成错误。
        public int Length { get; set; }
    }
}


