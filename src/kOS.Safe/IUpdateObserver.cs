using System;

namespace kOS.Safe
{
    public interface IUpdateObserver : IDisposable
    {
        void KOSUpdate(double deltaTime);
    }

    public interface IFixedUpdateObserver : IDisposable
    {
        void KOSFixedUpdate(double deltaTime);
        
        /// <summary>
        /// If you make this return TRUE in your IFixedUpdateObserver, you will
        /// cause your KOSFixedUpdate to be under the control of kOS.Safe.ConcurrencyManager,
        /// and let you use all its features.  Otherwise your KOSFixedUpdate() will be
        /// called just like a normal Unity FixedUpdate() call would operate, and it will
        /// be under the same limitations as a normal Unity FixedUpdate() call.
        /// </summary>
        bool IsConcurrent { get; }
    }
}