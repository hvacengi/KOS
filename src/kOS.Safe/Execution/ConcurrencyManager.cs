using System;
using System.Threading;

namespace kOS.Safe.Execution
{
    public class ConcurrencyManager
    {
        public delegate void ChildThreadMethod();

        public ManualResetEvent ChildThreadReset { get; private set; }

        public ManualResetEvent ParentThreadReset { get; private set; }

        public ChildThreadMethod ChildMethod { get; private set; }

        public Thread ChildThread { get; private set; }

        public Exception Exception { get; private set; }

        public bool IsRunning { get; private set; }

        public bool IsParallel { get; private set; }

        public bool IsErrored { get; private set; }

        public int TimeOutCount { get; set; }

        public System.Diagnostics.Stopwatch ChildStopwatch { get; set; }

        public System.Diagnostics.Stopwatch ParallelStopwatch { get; set; }

        public System.Diagnostics.Stopwatch ParentStopwatch { get; set; }

        public ConcurrencyManager()
        {
            ChildThreadReset = new ManualResetEvent(false);
            ParentThreadReset = new ManualResetEvent(true);
            IsRunning = false;
            IsParallel = false;
            IsErrored = false;
            TimeOutCount = 0;
            ChildStopwatch = new System.Diagnostics.Stopwatch();
            ParallelStopwatch = new System.Diagnostics.Stopwatch();
            ParentStopwatch = new System.Diagnostics.Stopwatch();
        }

        public ConcurrencyManager(ChildThreadMethod childMethod)
        {
            ChildThreadReset = new ManualResetEvent(false);
            ParentThreadReset = new ManualResetEvent(true);
            ChildMethod = childMethod;
            IsRunning = false;
            IsParallel = false;
            IsErrored = false;
            TimeOutCount = 0;
            ChildStopwatch = new System.Diagnostics.Stopwatch();
            ParallelStopwatch = new System.Diagnostics.Stopwatch();
            ParentStopwatch = new System.Diagnostics.Stopwatch();
        }

        public void Start()
        {
            if (ChildThread != null)
            {
                throw new kOS.Safe.Exceptions.KOSException("Cannot start concurency manager, thread already exists.");
            }
            IsRunning = true;
            ChildThread = new Thread(() =>
            {
                while (IsRunning)
                {
                    try
                    {
                        while (IsRunning)
                        {
                            ChildMethod();
                            AllowParent();
                            WaitForParent();
                        }
                    }
                    catch (Exception ex)
                    {
                        IsErrored = true;
                        this.Exception = ex;
                        this.IsRunning = false;
                    }
                }
            });
            ChildThread.Start();
            Thread.Sleep(0);
            if (!ChildThread.IsAlive)
            {
                IsRunning = false;
                ParentThreadReset.Set();
                throw new kOS.Safe.Exceptions.KOSException("Child thread is not alive.");
            }
        }

        public void Stop()
        {
            if (IsRunning)
            {
                IsRunning = false;
                AllowChild();
                if (ChildThread.IsAlive)
                {
                    ChildThread.Join();
                }
                ChildThread = null;
            }
        }

        public void BlockParent()
        {
            ParentThreadReset.Reset();
        }

        public void AllowParent()
        {
            ChildThreadReset.Reset();
            ParentThreadReset.Set();
            ChildStopwatch.Stop();
            Thread.Sleep(0);
        }

        public void AllowParentParallel()
        {
            IsParallel = true;
            ChildThreadReset.Reset();
            ChildStopwatch.Stop();
            //ParallelStopwatch.Reset();
            ParallelStopwatch.Start();
            ParentThreadReset.Set();
        }

        public bool WaitForChild()
        {
            if (IsRunning)
            {
                bool ret = true;
                if (!IsParallel)
                {
                    ret = ParentThreadReset.WaitOne(1000);
                }
                ChildThreadReset.Reset();
                ParentStopwatch.Reset();
                ParentStopwatch.Start();
                return ret;
            }
            else return true;
        }

        public void BlockChild()
        {
            ChildThreadReset.Reset();
        }

        public void AllowChild()
        {
            ParentThreadReset.Reset();
            ChildThreadReset.Set();
            ParentStopwatch.Stop();
            Thread.Sleep(0);
        }

        public void WaitForParent()
        {
            bool resetChild = true;
            if (IsParallel)
            {
                IsParallel = false;
                resetChild = false;
                ChildThreadReset.Reset();
                ParallelStopwatch.Stop();
                Thread.Sleep(0);
            }
            ChildThreadReset.WaitOne();
            ParentThreadReset.Reset();
            if (resetChild) { ChildStopwatch.Reset(); }
            ChildStopwatch.Start();
        }
    }
}