using System;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.CompilerServices;

namespace AsyncTest
{
    /// <summary>
    /// 用与测试异步方法和反编译IL对照，备忘
    /// </summary>
    class Program
    {
        public static TaskScheduler current { get; private set; }

        static void Main(string[] args)
        {
            NewMethod1();

            NewMethod2();

            TaskCompletionSource<int > source = new TaskCompletionSource<int>();
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            var state = source.Task.Status;
            Beginawait(source);
            var state2 = source.Task.Status;
            tokenSource.Cancel();
            Console.ReadLine();
        }

        private static async void Beginawait(TaskCompletionSource<int> source)
        {
            await source.Task;
            Console.WriteLine($"Task 触发取消");
        }

        private static void NewMethod2()
        {
            Test3 test3 = new Test3();
            test3.Test2();
            ThreadPool.QueueUserWorkItem(state =>
            {
                test3.source.Task.GetAwaiter().OnCompleted(() =>
                {
                    test3.ToString();
                });
                test3.source.SetResult(1);
            });
        }

        private static void NewMethod1()
        {
            Console.WriteLine("Hello World!");
            //NewMethod();
            Console.WriteLine($"5------当前线程{Thread.CurrentThread.ManagedThreadId}");
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            current = TaskScheduler.FromCurrentSynchronizationContext();
            Test2 test2 = new Test2();
            test2.TestAsync();

            Console.WriteLine($"6------当前线程{Thread.CurrentThread.ManagedThreadId}");
            Thread.Sleep(10000);
            Console.WriteLine($"7------当前线程{Thread.CurrentThread.ManagedThreadId}");
        }

        private static void NewMethod()
        {
            Test test = new Test();
            test.Test1Async();
            Task.Run(() =>
            {
                Console.WriteLine($"1------当前线程{Thread.CurrentThread.ManagedThreadId}");
                Thread.Sleep(1000);
                Console.WriteLine($"4------当前线程{Thread.CurrentThread.ManagedThreadId}");
                test.source.SetResult(0);
                Console.WriteLine($"5------当前线程{Thread.CurrentThread.ManagedThreadId}");
            });
            Thread.Sleep(10000);
            Console.WriteLine($"2------当前线程{Thread.CurrentThread.ManagedThreadId}");
        }
    }

    internal class Test2
    {
        internal async Task TestAsync()
        {
            await Task.Run(()=>
            {
                Console.WriteLine($"1------当前线程{Thread.CurrentThread.ManagedThreadId}");
                Thread.Sleep(1000);
                Console.WriteLine($"2------当前线程{Thread.CurrentThread.ManagedThreadId}");
            }).ContinueWith((task)=> 
            {
                Console.WriteLine($"3------当前线程{Thread.CurrentThread.ManagedThreadId}");
            }, Program.current);
            Console.WriteLine($"4------当前线程{Thread.CurrentThread.ManagedThreadId}");


            await Test11111();
        }

        private Task Test11111()
        {
            return null;
        }
    }

    public class Test
    {
        public TaskCompletionSource<int> source;

        public async Task Test1Async()
        {
            Console.WriteLine($"6------当前线程{Thread.CurrentThread.ManagedThreadId}");
            int a = await Test2();
            Console.WriteLine($"3------当前线程{Thread.CurrentThread.ManagedThreadId}");
        }

        private Task<int> Test2()
        {
            source = new TaskCompletionSource<int>();
            return source.Task;
        }
    }

    public class Test3
    {
        public TaskCompletionSource<int> source;
        public FastAwaiter<int> fastAwaiter = new FastAwaiter<int>();
        public Task<int> Test1Async()
        {
            source = new TaskCompletionSource<int>();
            source.ToString();
            return source.Task;
        }

        public async void Test2()
        {
            try
            {
                int v = await Test1Async();
                Console.WriteLine(v);
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                Console.WriteLine("finally");
            }
        }
    }

    public class Test4
    {
        public Task<int> Test1()
        {
            return Task.FromResult(1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<int> Test2()
        {
            int v = await Test1();
            return v;
        }

        public async void Test3()
        {
            int v = await Test2();
            Console.WriteLine(v);
        }
    }

    public class Test5
    {
        public async void Test()
        {
            await TestValueTask();
        }

        private ValueTask TestValueTask()
        {
            return default;
        }
    }

    public class Test6
    {
        public async void Test()
        {
            var a = await TestValueTask();
            Console.WriteLine(a);
        }

        private ValueTask<int> TestValueTask()
        {
            return default;
        }
    }

    public class Test7
    {
        public ValueTask<int> Test1()
        {
            return new ValueTask<int>(1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<int> Test2()
        {
            int v = await Test1();
            return v;
        }

        public async void Test3()
        {
            int v = await Test2();
            Console.WriteLine(v);
        }
    }


    public class Test8
    {
        public async void Send()
        {
            await new FastAwaitable();
            Console.WriteLine("");
        }
    }

    public class Test9
    {
        public MiniTask<int> Test()
        {
            return MiniTask<int>.Rent();
        }

        //public async IMiniAwaitable<int> Test2()
        //{
        //    await Task.Delay(1000);
        //    return 0;
        //}
    }

    public interface IGetAwaiter
    {
        IAwaiterResult GetAwaiter();
    }

    public interface IAwaiterResult : INotifyCompletion
    {
        bool IsCompleted { get; }
        void GetResult();
    }

    public interface IGetAwaiter<T>
    {
        IAwaiterResult<T> GetAwaiter();
    }

    public interface IAwaiterResult<T> : ICriticalNotifyCompletion
    {
        bool IsCompleted { get; }
        T GetResult();
    }

    public class FastAwaitable : IGetAwaiter
    {
        public IAwaiterResult GetAwaiter()
        {
            throw new NotImplementedException();
        }


    }

    public class FastAwaiter<T>
    {
        private Action UC;
        private Action C;

        public bool IsCompleted { get; internal set; }
        public T Result { get; internal set; }

        public void GetAwaiter()
        {
            //return default;
        }

        internal void SetUC(Action continuation)
        {
            this.UC = continuation;
        }

        internal void SetC(Action continuation)
        {
            this.C = continuation;
        }

        public void SetResult(T r)
        {
            this.Result = r;
            this.UC();
        }
    }

    public struct Result<T> : IAwaiterResult<T>
    {
        private FastAwaiter<T> fastAwaiter;

        public Result(FastAwaiter<T> fastAwaiter)
        {
            this.fastAwaiter = fastAwaiter;
        }

        public bool IsCompleted => fastAwaiter.IsCompleted;

        public T GetResult()
        {
            return fastAwaiter.Result;
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            fastAwaiter.SetUC(continuation);
        }

        public void OnCompleted(Action continuation)
        {
            fastAwaiter.SetC(continuation);
        }
    }

    
}
