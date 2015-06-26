using System;
using System.Threading;

namespace kOS.Safe.Execution
{
    /// <summary>
    /// ConcurrencyManager class for managing synchronization between a parent thread and a child thread.  The child 
    /// method will be executed any time that the parent calls the AllowChild() method.  This child method may also 
    /// defer execution to the parent thread, either in parallel or in series.  See the AllowParent, AllowParentParallel 
    /// and AllowChild methods.  At a minimum, the parent thread needs to call AllowChild() followed by WaitForChild()
    /// at any point where the child is allowed to execute (i.e. every pass through a loop).  If the child is operating
    /// in a parallel safe mode, the wait will return imediately.
    /// </summary>
    public class ConcurrencyManager
    {
        public delegate void ChildThreadMethod();

        /// <summary>
        /// The reset event for the child thread.
        /// </summary>
        public ManualResetEvent ChildThreadReset { get; private set; }

        /// <summary>
        /// The reset event for the parent thread.
        /// </summary>
        public ManualResetEvent ParentThreadReset { get; private set; }

        /// <summary>
        /// The delegate method to be iterativly executed.  It will be called after the parent calls AllowChild()
        /// unless the child is already running in parallel.  Parent/Child allows/waits may also be managed from
        /// within ChildMethod if it has access to the ConcurrencyManager object running it.
        /// </summary>
        public ChildThreadMethod ChildMethod { get; private set; }

        public Thread ChildThread { get; private set; }

        /// <summary>
        /// Returns the last exception caught from ChildMethod.  It may be null.
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        /// Returns true if the child thread is currently running.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Returns true if the execution of ChildMethod has signaled the parent may continue in parallel.  It is
        /// automatically cleared the next time the child calls WaitForParent().
        /// </summary>
        public bool IsParallel { get; private set; }

        /// <summary>
        /// Returns true if executing ChildMethod caught an exception.  The exception is stored in the Exception member.
        /// </summary>
        public bool IsErrored { get; private set; }

        /// <summary>
        /// A count of the number of times that WaitForChild() encountered a timeout waiting for the reset event.  May
        /// be used to check the health of the child thread.
        /// </summary>
        public int TimeOutCount { get; set; }

        /// <summary>
        /// Stopwatch to monitor the amount of time spent executing in the child thread while in sequence with the parent.
        /// It is automatically reset when started in the WaitForParent() method, after the wait returns.
        /// </summary>
        public System.Diagnostics.Stopwatch ChildStopwatch { get; set; }

        /// <summary>
        /// Stopwatch to monitor the amount of time spent executing in the parallel thread.  It must be reset externally.
        /// </summary>
        public System.Diagnostics.Stopwatch ParallelStopwatch { get; set; }

        /// <summary>
        /// Stopwatch to monitor the amount of time spent executing in the parent thread.  It is automatically reset when
        /// started in the WaitForChild() method, after the wait returns.
        /// </summary>
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

        /// <summary>
        /// Constructor for a new ConcurrencyManager object with the given child method.  The child method will be executed
        /// any time that the parent calls the AllowChild() method.  This child method may also defer execution to the
        /// parent thread, either in parallel or in series.  See the AllowParent, AllowParentParallel and AllowChild methods.
        /// </summary>
        /// <param name="childMethod">A method to be invoked on every iteration of the loop</param>
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

        /// <summary>
        /// Starts the child thread and checks to see if it starts.  Will throw an exception if called when the thread 
        /// is already running, or if the child thread is not alive after starting execution.
        /// </summary>
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

        /// <summary>
        /// Stops the child thread, allowing it to continue and joining the thread to ensure execution is complete.
        /// </summary>
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

        /// <summary>
        /// Called by the child, allows the parent to proceed in execution.
        /// </summary>
        public void AllowParent()
        {
            ChildThreadReset.Reset();
            ParentThreadReset.Set();
            ChildStopwatch.Stop();
            Thread.Sleep(0);
        }

        /// <summary>
        /// Called by the child, allows the parent to proceed in execution while setting the IsParallel
        /// flag to true.  Use this method to allow the child to execute in parallel to the parent.
        /// You must call WaitForParent from the child after the parallel-safe execution is complete.
        /// </summary>
        public void AllowParentParallel()
        {
            IsParallel = true;
            ChildThreadReset.Reset();
            ChildStopwatch.Stop();
            //ParallelStopwatch.Reset();
            ParallelStopwatch.Start();
            ParentThreadReset.Set();
        }

        /// <summary>
        /// Pause the current thread and wait for the child to signal that it is safe to proceed.
        /// </summary>
        /// <returns>False if waiting for the child thread times out, and true otherwise</returns>
        public bool WaitForChild()
        {
            if (IsRunning)
            {
                bool ret = true;
                if (!IsParallel)
                {
                    ret = ParentThreadReset.WaitOne(1000);
                }
                if (!ret)
                {
                    TimeOutCount++;
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

        /// <summary>
        /// Called by the parent, allows the child to proceed in execution.
        /// </summary>
        public void AllowChild()
        {
            ParentThreadReset.Reset();
            ChildThreadReset.Set();
            ParentStopwatch.Stop();
            Thread.Sleep(0);
        }

        /// <summary>
        /// Called by the child, pauses the current thread and waits for the parent to signal
        /// that it is safe to proceed.
        /// </summary>
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