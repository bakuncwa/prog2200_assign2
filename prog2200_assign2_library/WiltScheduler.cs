using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Makes lilies wilt one at a time. Ready lilies are queued with <see cref="Request"/>; a background worker
    /// thread releases them one by one, a set gap apart, and the timer collects each with <see cref="TryTakeNext"/>.
    /// The worker never touches a flower: only the user-interface thread changes flowers.
    /// </summary>
    public sealed class WiltScheduler : IDisposable
    {
        private const int StopTimeoutMs = 1000;

        private readonly BlockingCollection<Flower> _waiting = new BlockingCollection<Flower>();
        private readonly ConcurrentQueue<Flower> _released = new ConcurrentQueue<Flower>();

        // Only used on the user-interface thread.
        private readonly HashSet<Flower> _queued = new HashSet<Flower>();

        private readonly AutoResetEvent _taken = new AutoResetEvent(false);
        private readonly ManualResetEvent _stopping = new ManualResetEvent(false);

        private readonly int _gapMs;
        private readonly Thread _worker;

        private bool _isDisposed;

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

        /// <summary>Queues a flower to wilt, unless it is already queued.</summary>
        public void Request(Flower flower)
        {
            if (_queued.Add(flower))
            {
                _waiting.Add(flower);
            }
        }

        /// <summary>Gets the flower whose turn it is to wilt, if any.</summary>
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

        /// <summary>Worker thread: releases one flower, waits until it is taken, then waits the gap.</summary>
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
                // Disposed while waiting; nothing left to do.
            }
        }
    }
}
