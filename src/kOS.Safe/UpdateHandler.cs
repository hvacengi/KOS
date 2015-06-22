using System.Collections.Generic;
using System.Linq;
using System;
using kOS.Safe.Execution;
using kOS.Safe.Utilities;

namespace kOS.Safe
{
    public class UpdateHandler
    {
        // Using a Dictionary instead of List to prevent duplications.  If an object tries to
        // insert itself more than once into the observer list, it still only gets in the list
        // once and therefore only gets its Update() called once per update.
        // The value of the KeyValuePair, the int, is unused.
        private readonly HashSet<IUpdateObserver> observers = new HashSet<IUpdateObserver>();
        private readonly HashSet<IFixedUpdateObserver> fixedObservers = new HashSet<IFixedUpdateObserver>();
        private readonly HashSet<IFixedUpdateObserver> concurrentFixedObservers = new HashSet<IFixedUpdateObserver>();

        public ConcurrencyManager concurrencyManager;

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
                if (concurrencyManager == null)
                {
                    SafeHouse.Logger.LogError("concurencyManager is null, intatiating new (AddFixedObserver)");
                    concurrencyManager = new ConcurrencyManager(UpdateConcurrentFixedObservers);
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
                    concurrencyManager.Stop();
                    concurrencyManager = null;
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
            if (concurrencyManager != null)
            {
                if (concurrencyManager.IsRunning)
                {
                    concurrencyManager.AllowChild();
                    bool timedOut = !concurrencyManager.WaitForChild();
                    if (timedOut)
                    {
                        SafeHouse.Logger.LogError("Timeout waiting for concurencyManager child (UpdateFixedObservers)");
                        SafeHouse.Logger.LogError(string.Format("ChildThread.ThreadState = {0} (UpdateFixedObservers)", concurrencyManager.ChildThread.ThreadState));
                        if (++timeouts > 30)
                        {
                            concurrencyManager.Stop();
                        }
                    }
                }
                else if (concurrencyManager.IsErrored)
                {
                    SafeHouse.Logger.LogError("concurencyManager is errored (UpdateFixedObservers)");
                    SafeHouse.Logger.LogException(concurrencyManager.Exception);
                    concurrencyManager.Stop();
                    //concurrencyManager = null;
                }
                else
                {
                    SafeHouse.Logger.LogError("concurencyManager is not running, starting now (UpdateFixedObservers)");
                    concurrencyManager.Start();
                }
            }
            else
            {
                if (concurrentFixedObservers.Count > 0)
                {
                    SafeHouse.Logger.LogError("concurencyManager is null, intatiating new (UpdateFixedObservers)");
                    concurrencyManager = new ConcurrencyManager(UpdateConcurrentFixedObservers);
                }
            }
        }

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
