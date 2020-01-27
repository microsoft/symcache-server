// © Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SymCacheHttpServer
{
    sealed class BackgroundTranscodeService : IHostedService, IBackgroundTranscodeQueue, IDisposable
    {
        readonly SymCacheRepository repository;
        readonly SymCacheTranscoder transcoder;

        readonly ConcurrentQueue<SymCacheKey> queue = new ConcurrentQueue<SymCacheKey>();
        readonly AutoResetEvent itemAvailable = new AutoResetEvent(false);
        readonly ConcurrentDictionary<SymCacheKey, SymCacheKey> pendingItems =
            new ConcurrentDictionary<SymCacheKey, SymCacheKey>();
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        readonly Thread[] threads;

        bool started;
        bool disposed;

        public BackgroundTranscodeService(SymCacheRepository repository, SymCacheTranscoder transcoder)
        {
            Debug.Assert(repository != null);
            Debug.Assert(transcoder != null);

            this.repository = repository;
            this.transcoder = transcoder;

            // Use one background transcode thread for each processor.
            threads = new Thread[Environment.ProcessorCount];
        }

        public void Enqueue(SymCacheKey key)
        {
            queue.Enqueue(key);
            itemAvailable.Set();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (started)
            {
                throw new InvalidOperationException();
            }

            for (int index = 0; index < threads.Length; ++index)
            {
                Thread thread = new Thread(RunThread);
                thread.IsBackground = true;
                thread.Start();
                threads[index] = thread;
            }

            started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (!started)
            {
                throw new InvalidOperationException();
            }

            cancellationTokenSource.Cancel();

            for (int index = 0; index < threads.Length; ++index)
            {
                while (threads[index] != null)
                {
                    Thread thread = threads[index];

                    bool threadExited;

                    if (cancellationToken.IsCancellationRequested)
                    {
                        //thread.Abort(); // thread.Abort is not supported on .NET Core.
                        //threadExited = true;
                        continue;
                    }
                    else if (thread.Join(500))
                    {
                        threadExited = true;
                    }
                    else
                    {
                        threadExited = false;
                    }

                    if (threadExited)
                    {
                        threads[index] = null;
                    }
                }
            }

            started = false;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                //foreach (Thread thread in threads)
                //{
                //    thread?.Abort(); // thread.Abort() is not supported on .NET Core.
                //}

                cancellationTokenSource.Dispose();
                disposed = true;
            }
        }

        void RunThread()
        {
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            // Keep the thread running until the service receives a request to stop.
            while (!cancellationToken.IsCancellationRequested)
            {
                // Wait for either the service to receive a request to stop, or for a least one item to be available to
                // process.
                WaitHandle.WaitAny(new WaitHandle[] { cancellationToken.WaitHandle, itemAvailable });

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                SymCacheKey key;

                while (queue.TryDequeue(out key))
                {
                    if (queue.Count > 0)
                    {
                        // There is at least one item this thread is not processing. Make sure other threads know at
                        // least one more thread can run. (The AutoResetEvent is set any time an item is enqueued, but
                        // multiple items may be enqueued before a single thread wakes up and resets the event.)
                        itemAvailable.Set();
                    }

                    if (!pendingItems.TryAdd(key, key))
                    {
                        // This item is already being processed. Don't start another instance of processing it.
                        continue;
                    }

                    try
                    {
                        ProcessAsync(key, cancellationToken).GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException)
                    {
                        // Do not cause the thread to exit abnormally when the cancellationToken is honored.
                    }

                    pendingItems.TryRemove(key, out _);
                }
            }
        }

        Task ProcessAsync(SymCacheKey key, CancellationToken cancellationToken)
        {
            Debug.Assert(key != null);
            return transcoder.TryTranscodeAsync(key, cancellationToken);
        }
    }
}
