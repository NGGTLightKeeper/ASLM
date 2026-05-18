// Copyright NGGT.LightKeeper. All Rights Reserved.

namespace ASLM.Services
{
    /// <summary>
    /// Limits concurrent module launch operations shared by the shell and the interop HTTP API.
    /// </summary>
    public sealed class ModuleStartThrottle
    {
        /// <summary>
        /// Default maximum concurrent module starts (matches historical shell startup throttling).
        /// </summary>
        public const int DefaultMaxConcurrentStarts = 2;

        private readonly SemaphoreSlim _semaphore;

        /// <summary>
        /// Creates the shared throttle.
        /// </summary>
        public ModuleStartThrottle()
        {
            _semaphore = new SemaphoreSlim(DefaultMaxConcurrentStarts, DefaultMaxConcurrentStarts);
        }

        /// <summary>
        /// Waits until a launch slot is available.
        /// </summary>
        public Task WaitAsync(CancellationToken cancellationToken = default) =>
            _semaphore.WaitAsync(cancellationToken);

        /// <summary>
        /// Releases one launch slot.
        /// </summary>
        public void Release() => _semaphore.Release();
    }
}
