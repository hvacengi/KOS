using System.Collections.Generic;
using System.Linq;
using System;
using kOS.Safe.Execution;
using kOS.Safe.Utilities;

namespace kOS.Safe
{
    public class UpdateHandler
    {
        // Using a HashSet instead of List to prevent duplications.  If an object tries to
        // insert itself more than once into the observer list, it still only gets in the list
        // once and therefore only gets its Update() called once per update.
        private readonly HashSet<IUpdateObserver> observers = new HashSet<IUpdateObserver>();
        private readonly HashSet<IFixedUpdateObserver> fixedObservers = new HashSet<IFixedUpdateObserver>();
        private readonly HashSet<IFixedUpdateObserver> concurrentFixedObservers = new HashSet<IFixedUpdateObserver>();

        public ConcurrencyManager ConcurrencyManager { get; private set; }

        public double CurrentFixedTime { get; private set; }
        public double LastDeltaFixedTime { get; private set; }
        public double CurrentTime { get; private set; }
        public double LastDeltaTime { get; private set; }
        
        public void AddObserver(IUpdateObserver observer)
        {
            observers.Add(observer);
        }

        public void AddFixedObserver(IFixedUpdateObserver observer)
        {
            if (observer.IsConcurrent)
            {
                if (ConcurrencyManager == null)
                {
                    SafeHouse.Logger.LogError("concurencyManager is null, intatiating new (AddFixedObserver)");
                    ConcurrencyManager = new ConcurrencyManager(UpdateConcurrentFixedObservers);
                }
                concurrentFixedObservers.Add(observer);
            }
            else
            {
                fixedObservers.Add(observer);
            }
        }

        public void RemoveObserver(IUpdateObserver observer)
        {
            observers.Remove(observer);
        }

        public void RemoveFixedObserver(IFixedUpdateObserver observer)
        {
            if (observer.IsConcurrent)
            {
                concurrentFixedObservers.Remove(observer);
                if (concurrentFixedObservers.Count == 0)
                {
                    SafeHouse.Logger.LogError("concurentFixedObservers is empty, stopping concurrencyManager (RemoveFixedObserver)");
                    ConcurrencyManager.Stop();
                    ConcurrencyManager = null;
                }
            }
            else
            {
                fixedObservers.Remove(observer);
            }
        }

        public void UpdateObservers(double deltaTime)
        {
            LastDeltaTime = deltaTime;
            CurrentTime += deltaTime;
            
            var snapshot = new HashSet<IUpdateObserver>(observers);
            foreach (var observer in snapshot)
            {
                observer.KOSUpdate(deltaTime);
            }
        }

        int timeouts = 0;
        public void UpdateFixedObservers(double deltaTime)
        {
            LastDeltaFixedTime = deltaTime;
            CurrentFixedTime += deltaTime;
            
            var snapshot = new HashSet<IFixedUpdateObserver>(fixedObservers);
            foreach (var observer in snapshot)
            {
                observer.KOSFixedUpdate(deltaTime);
            }
            AllowConcurrentThreadsToUpdate();
        }

        /// <summary>
        /// Signals the concurrencyManager to allow its child thread
        /// (if it was blocking until its parent (me) said it was okay to continue)
        /// to have an update happen (i.e. allow one pass through the
        /// ConcurrencyManager.ChildMethod()).
        /// Will block until the concurrencyManager says the child thread method
        /// has had one pass, or has told the parent (me) that it thinks it has entered
        /// a section where it's safe to continue on in parallel with me (Unity's main thread).
        /// </summary>
        private void AllowConcurrentThreadsToUpdate()
        {
            if (ConcurrencyManager != null)
            {
                if (ConcurrencyManager.IsRunning)
                {
                    // This causes the concurrencyManager to end up calling my
                    // UpdateConcurrentFixedObservers():
                    ConcurrencyManager.AllowChild();
                    
                    // This call will block until my UpdateConcurrentFixedObservers()
                    // says it finished one full pass, or one of the methods inside it
                    // chose to tell me it thinks it has entered a threadsafe section
                    // where I can go on while it keeps working (i.e. compiling a program).
                    bool timedOut = !ConcurrencyManager.WaitForChild();

                    if (timedOut)
                    {
                        SafeHouse.Logger.LogError("Timeout waiting for concurencyManager child (UpdateFixedObservers)");
                        SafeHouse.Logger.LogError(string.Format("ChildThread.ThreadState = {0} (UpdateFixedObservers)", ConcurrencyManager.ChildThread.ThreadState));
                        if (++timeouts > 30)
                        {
                            ConcurrencyManager.Stop();
                        }
                    }
                }
                else if (ConcurrencyManager.IsErrored)
                {
                    SafeHouse.Logger.LogError("concurencyManager is errored (UpdateFixedObservers)");
                    SafeHouse.Logger.LogException(ConcurrencyManager.Exception);
                    ConcurrencyManager.Stop();
                    //concurrencyManager = null;
                }
                else
                {
                    SafeHouse.Logger.LogError("concurencyManager is not running, starting now (UpdateFixedObservers)");
                    ConcurrencyManager.Start();
                }
            }
            else
            {
                if (concurrentFixedObservers.Count > 0)
                {
                    SafeHouse.Logger.LogError("concurencyManager is null, intatiating new (UpdateFixedObservers)");
                    ConcurrencyManager = new ConcurrencyManager(UpdateConcurrentFixedObservers);
                }
            }
        }

        /// <summary>
        /// This is called BY the ConcurrencyManager, and is what the child thread is
        /// doing when me, the parent thread, tells it it's allowed to have a timeslice pass.
        /// </summary>
        public void UpdateConcurrentFixedObservers()
        {
            var snapshot = new HashSet<IFixedUpdateObserver>(concurrentFixedObservers);
            foreach (var observer in snapshot)
            {
                observer.KOSFixedUpdate(LastDeltaFixedTime);
            }
        }
        
        /// <summary>
        /// Return all the registered fixed update handlers of a particular type
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public IEnumerable<IFixedUpdateObserver> GetAllFixedUpdatersOfType(Type t)
        {
            return fixedObservers.Where(item => t.IsAssignableFrom(item.GetType()));
        }
        
        /// <summary>
        /// Return all the registered update handlers of a particular type
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public IEnumerable<IUpdateObserver> GetAllUpdatersOfType(Type t)
        {
            return observers.Where(item => t.IsAssignableFrom(item.GetType()));
        }

    }
}
