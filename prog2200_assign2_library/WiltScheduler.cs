using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Makes flowers wilt one at a time instead of all at once. Flowers that are ready to wilt are handed to
    /// <see cref="Request"/>; a background worker thread lets them go one by one, waiting a while after each one
    /// actually starts wilting before letting the next go. The garden's timer collects each flower whose turn has come
    /// with <see cref="TryTakeNext"/> and tells it to wilt.
    /// </summary>
    /// <remarks>
    /// The worker thread only passes flowers along; it never touches them. Request and TryTakeNext are called on the
    /// user-interface thread, which is the only thread that changes a flower.
    /// </remarks>
    public sealed class WiltScheduler : IDisposable
    {
        // How long the worker waits for the garden to stop before giving up when the scheduler is disposed.
        private const int StopTimeoutMs = 1000;

        // Flowers waiting for their turn, oldest first, and the one whose turn has come.
        private readonly BlockingCollection<Flower> _waiting = new BlockingCollection<Flower>();
        private readonly ConcurrentQueue<Flower> _released = new ConcurrentQueue<Flower>();

        // Flowers that have been requested and not yet taken, so each one is only queued once.
        // Only used on the user-interface thread.
        private readonly HashSet<Flower> _queued = new HashSet<Flower>();

        // Set by the garden when it takes the released flower; set when the scheduler is disposed.
        private readonly AutoResetEvent _taken = new AutoResetEvent(false);
        private readonly ManualResetEvent _stopping = new ManualResetEvent(false);

        private readonly int _gapMs;
        private readonly Thread _worker;

        private bool _isDisposed;

        /// <param name="gapMs">Milliseconds between one flower starting to wilt and the next being let go.</param>
        public WiltScheduler(int gapMs)
        {
            _gapMs = gapMs;
            _worker = new Thread(ReleaseOneAtATime)
            {
                IsBackground = true,
                Name = "Wilt scheduler"
            };
            _worker.Start();
        }

        /// <summary>Puts <paramref name="flower"/> in the queue to wilt, unless it is already waiting.</summary>
        public void Request(Flower flower)
        {
            if (_queued.Add(flower))
            {
                _waiting.Add(flower);
            }
        }

        /// <summary>Gets the flower whose turn it is to wilt, if there is one right now.</summary>
        public bool TryTakeNext(out Flower flower)
        {
            if (!_released.TryDequeue(out flower))
            {
                return false;
            }

            _queued.Remove(flower);
            _taken.Set();
            return true;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _waiting.CompleteAdding();
            _stopping.Set();
            _worker.Join(StopTimeoutMs);

            _waiting.Dispose();
            _taken.Dispose();
            _stopping.Dispose();
        }

        /// <summary>
        /// The worker thread: lets the waiting flowers go one at a time. After each one it waits until the garden has
        /// taken it (so a paused garden does not build up a crowd that would all wilt together), then waits the gap.
        /// </summary>
        private void ReleaseOneAtATime()
        {
            WaitHandle[] takenOrStopping = { _taken, _stopping };

            try
            {
                foreach (Flower flower in _waiting.GetConsumingEnumerable())
                {
                    _released.Enqueue(flower);

                    if (WaitHandle.WaitAny(takenOrStopping) == 1 || _stopping.WaitOne(_gapMs))
                    {
                        return;
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // The scheduler was disposed while the worker was waiting; there is nothing left to do.
            }
        }
    }
}
