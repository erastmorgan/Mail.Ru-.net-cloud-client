using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MailRuCloudApi
{
    /// <summary>
    /// A ring-buffer stream that you can read from and write to from
    /// different threads.
    /// </summary>
    public class RingBufferedStream : Stream
    {
        private readonly byte[] store;

        private readonly ManualResetEventAsync writeAvailable
            = new ManualResetEventAsync(false);

        private readonly ManualResetEventAsync readAvailable
            = new ManualResetEventAsync(false);

        private readonly CancellationTokenSource cancellationTokenSource
            = new CancellationTokenSource();

        private int readPos;

        private int readAvailableByteCount;

        private int writePos;

        private int writeAvailableByteCount;

        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="RingBufferedStream"/>
        /// class.
        /// </summary>
        /// <param name="bufferSize">
        /// The maximum number of bytes to buffer.
        /// </param>
        public RingBufferedStream(int bufferSize)
        {
            this.store = new byte[bufferSize];
            this.writeAvailableByteCount = bufferSize;
            this.readAvailableByteCount = 0;
        }

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override long Length
        {
            get
            {
                throw new NotSupportedException(
                    "Cannot get length on RingBufferedStream");
            }
        }

        /// <inheritdoc/>
        public override int ReadTimeout { get; set; } = Timeout.Infinite;

        /// <inheritdoc/>
        public override int WriteTimeout { get; set; } = Timeout.Infinite;

        /// <inheritdoc/>
        public override long Position
        {
            get
            {
                throw new NotSupportedException(
                    "Cannot set position on RingBufferedStream");
            }

            set
            {
                throw new NotSupportedException(
                    "Cannot set position on RingBufferedStream");
            }
        }

        /// <summary>
        /// Gets the number of bytes currently buffered.
        /// </summary>
        public int BufferedByteCount => this.readAvailableByteCount;

        /// <inheritdoc/>
        public override void Flush()
        {
            // nothing to do
        }

        /// <summary>
        /// Set the length of the current stream. Always throws <see
        /// cref="NotSupportedException"/>.
        /// </summary>
        /// <param name="value">
        /// The desired length of the current stream in bytes.
        /// </param>
        public override void SetLength(long value)
        {
            throw new NotSupportedException(
                "Cannot set length on RingBufferedStream");
        }

        /// <summary>
        /// Sets the position in the current stream. Always throws <see
        /// cref="NotSupportedException"/>.
        /// </summary>
        /// <param name="offset">
        /// The byte offset to the <paramref name="origin"/> parameter.
        /// </param>
        /// <param name="origin">
        /// A value of type <see cref="SeekOrigin"/> indicating the reference
        /// point used to obtain the new position.
        /// </param>
        /// <returns>
        /// The new position within the current stream.
        /// </returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("Cannot seek on RingBufferedStream");
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException("RingBufferedStream");
            }

            Monitor.Enter(this.store);
            bool haveLock = true;
            try
            {
                while (count > 0)
                {
                    if (this.writeAvailableByteCount == 0)
                    {
                        this.writeAvailable.Reset();
                        Monitor.Exit(this.store);
                        haveLock = false;
                        bool canceled;
                        if (!this.writeAvailable.Wait(
                            this.WriteTimeout,
                            this.cancellationTokenSource.Token,
                            out canceled) || canceled)
                        {
                            break;
                        }

                        Monitor.Enter(this.store);
                        haveLock = true;
                    }
                    else
                    {
                        var toWrite = this.store.Length - this.writePos;
                        if (toWrite > this.writeAvailableByteCount)
                        {
                            toWrite = this.writeAvailableByteCount;
                        }

                        if (toWrite > count)
                        {
                            toWrite = count;
                        }

                        Array.Copy(
                            buffer,
                            offset,
                            this.store,
                            this.writePos,
                            toWrite);
                        offset += toWrite;
                        count -= toWrite;
                        this.writeAvailableByteCount -= toWrite;
                        this.readAvailableByteCount += toWrite;
                        this.writePos += toWrite;
                        if (this.writePos == this.store.Length)
                        {
                            this.writePos = 0;
                        }

                        this.readAvailable.Set();
                    }
                }
            }
            finally
            {
                if (haveLock)
                {
                    Monitor.Exit(this.store);
                }
            }
        }

        /// <inheritdoc/>
        public override void WriteByte(byte value)
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException("RingBufferedStream");
            }

            Monitor.Enter(this.store);
            bool haveLock = true;
            try
            {
                while (true)
                {
                    if (this.writeAvailableByteCount == 0)
                    {
                        this.writeAvailable.Reset();
                        Monitor.Exit(this.store);
                        haveLock = false;
                        bool canceled;
                        if (!this.writeAvailable.Wait(
                            this.WriteTimeout,
                            this.cancellationTokenSource.Token,
                            out canceled) || canceled)
                        {
                            break;
                        }

                        Monitor.Enter(this.store);
                        haveLock = true;
                    }
                    else
                    {
                        this.store[this.writePos] = value;
                        --this.writeAvailableByteCount;
                        ++this.readAvailableByteCount;
                        ++this.writePos;
                        if (this.writePos == this.store.Length)
                        {
                            this.writePos = 0;
                        }

                        this.readAvailable.Set();
                        break;
                    }
                }
            }
            finally
            {
                if (haveLock)
                {
                    Monitor.Exit(this.store);
                }
            }
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException("RingBufferedStream");
            }

            Monitor.Enter(this.store);
            int ret = 0;
            bool haveLock = true;
            try
            {
                while (count > 0)
                {
                    if (this.readAvailableByteCount == 0)
                    {
                        this.readAvailable.Reset();
                        Monitor.Exit(this.store);
                        haveLock = false;
                        bool canceled;
                        if (!this.readAvailable.Wait(
                            this.ReadTimeout,
                            this.cancellationTokenSource.Token,
                            out canceled) || canceled)
                        {
                            break;
                        }

                        Monitor.Enter(this.store);
                        haveLock = true;
                    }
                    else
                    {
                        var toRead = this.store.Length - this.readPos;
                        if (toRead > this.readAvailableByteCount)
                        {
                            toRead = this.readAvailableByteCount;
                        }

                        if (toRead > count)
                        {
                            toRead = count;
                        }

                        Array.Copy(
                            this.store,
                            this.readPos,
                            buffer,
                            offset,
                            toRead);
                        offset += toRead;
                        count -= toRead;
                        this.readAvailableByteCount -= toRead;
                        this.writeAvailableByteCount += toRead;
                        ret += toRead;
                        this.readPos += toRead;
                        if (this.readPos == this.store.Length)
                        {
                            this.readPos = 0;
                        }

                        this.writeAvailable.Set();
                    }
                }
            }
            finally
            {
                if (haveLock)
                {
                    Monitor.Exit(this.store);
                }
            }

            return ret;
        }

        /// <inheritdoc/>
        public override int ReadByte()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException("RingBufferedStream");
            }

            Monitor.Enter(this.store);
            int ret = -1;
            bool haveLock = true;
            try
            {
                while (true)
                {
                    if (this.readAvailableByteCount == 0)
                    {
                        this.readAvailable.Reset();
                        Monitor.Exit(this.store);
                        haveLock = false;
                        bool canceled;
                        if (!this.readAvailable.Wait(
                            this.ReadTimeout,
                            this.cancellationTokenSource.Token,
                            out canceled) || canceled)
                        {
                            break;
                        }

                        Monitor.Enter(this.store);
                        haveLock = true;
                    }
                    else
                    {
                        ret = this.store[this.readPos];
                        ++this.writeAvailableByteCount;
                        --this.readAvailableByteCount;
                        ++this.readPos;
                        if (this.readPos == this.store.Length)
                        {
                            this.readPos = 0;
                        }

                        this.writeAvailable.Set();
                        break;
                    }
                }
            }
            finally
            {
                if (haveLock)
                {
                    Monitor.Exit(this.store);
                }
            }

            return ret;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.disposed = true;
                this.cancellationTokenSource.Cancel();
            }

            base.Dispose(disposing);
        }
    }

    public sealed class ManualResetEventAsync
    {
        /// <summary>
        /// The task completion source.
        /// </summary>
        private volatile TaskCompletionSource<bool> taskCompletionSource =
            new TaskCompletionSource<bool>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ManualResetEventAsync"/>
        /// class with a <see cref="bool"/> value indicating whether to set the
        /// initial state to signaled.
        /// </summary>
        /// <param name="initialState">
        /// True to set the initial state to signaled; false to set the initial
        /// state to non-signaled.
        /// </param>
        public ManualResetEventAsync(bool initialState)
        {
            if (initialState)
            {
                this.Set();
            }
        }

        /// <summary>
        /// Return a task that can be consumed by <see cref="Task.Wait()"/>
        /// </summary>
        /// <returns>
        /// The asynchronous waiter.
        /// </returns>
        public Task GetWaitTask()
        {
            return this.taskCompletionSource.Task;
        }

        /// <summary>
        /// Mark the event as signaled.
        /// </summary>
        public void Set()
        {
            var tcs = this.taskCompletionSource;
            Task.Factory.StartNew(
                s => ((TaskCompletionSource<bool>)s).TrySetResult(true),
                tcs,
                CancellationToken.None,
                TaskCreationOptions.PreferFairness,
                TaskScheduler.Default);
            tcs.Task.Wait();
        }

        /// <summary>
        /// Mark the event as not signaled.
        /// </summary>
        public void Reset()
        {
            while (true)
            {
                var tcs = this.taskCompletionSource;
                if (!tcs.Task.IsCompleted
#pragma warning disable 420
                || Interlocked.CompareExchange(
                        ref this.taskCompletionSource,
                        new TaskCompletionSource<bool>(),
                        tcs) == tcs)
#pragma warning restore 420
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Waits for the <see cref="ManualResetEventAsync"/> to be signaled.
        /// </summary>
        /// <exception cref="T:System.AggregateException">
        /// The <see cref="ManualResetEventAsync"/> waiting <see cref="Task"/>
        /// was canceled -or- an exception was thrown during the execution
        /// of the <see cref="ManualResetEventAsync"/> waiting <see cref="Task"/>.
        /// </exception>
        public void Wait()
        {
            this.GetWaitTask().Wait();
        }

        /// <summary>
        /// Waits for the <see cref="ManualResetEventAsync"/> to be signaled.
        /// </summary>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> to observe while waiting for
        /// the task to complete.
        /// </param>
        /// <exception cref="T:System.OperationCanceledException">
        /// The <paramref name="cancellationToken"/> was canceled.
        /// </exception>
        /// <exception cref="T:System.AggregateException">
        /// The <see cref="ManualResetEventAsync"/> waiting <see cref="Task"/> was
        /// canceled -or- an exception was thrown during the execution of the
        /// <see cref="ManualResetEventAsync"/> waiting <see cref="Task"/>.
        /// </exception>
        public void Wait(CancellationToken cancellationToken)
        {
            this.GetWaitTask().Wait(cancellationToken);
        }

        /// <summary>
        /// Waits for the <see cref="ManualResetEventAsync"/> to be signaled.
        /// </summary>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> to observe while waiting for
        /// the task to complete.
        /// </param>
        /// <param name="canceled">
        /// Set to true if the wait was canceled via the <paramref
        /// name="cancellationToken"/>.
        /// </param>
        public void Wait(CancellationToken cancellationToken, out bool canceled)
        {
            try
            {
                this.GetWaitTask().Wait(cancellationToken);
                canceled = false;
            }
            catch (Exception ex)
                when (ex is OperationCanceledException
                    || (ex is AggregateException
                        && ex.InnerOf<OperationCanceledException>() != null))
            {
                canceled = true;
            }
        }

        /// <summary>
        /// Waits for the <see cref="ManualResetEventAsync"/> to be signaled.
        /// </summary>
        /// <param name="timeout">
        /// A <see cref="System.TimeSpan"/> that represents the number of
        /// milliseconds to wait, or a <see cref="System.TimeSpan"/> that
        /// represents -1 milliseconds to wait indefinitely.
        /// </param>
        /// <returns>
        /// true if the <see cref="ManualResetEventAsync"/> was signaled within
        /// the allotted time; otherwise, false.
        /// </returns>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="timeout"/> is a negative number other than -1
        /// milliseconds, which represents an infinite time-out -or-
        /// timeout is greater than <see cref="int.MaxValue"/>.
        /// </exception>
        public bool Wait(TimeSpan timeout)
        {
            return this.GetWaitTask().Wait(timeout);
        }

        /// <summary>
        /// Waits for the <see cref="ManualResetEventAsync"/> to be signaled.
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// The number of milliseconds to wait, or
        /// <see cref="System.Threading.Timeout.Infinite"/> (-1) to wait
        /// indefinitely.
        /// </param>
        /// <returns>
        /// true if the <see cref="ManualResetEventAsync"/> was signaled within
        /// the allotted time; otherwise, false.
        /// </returns>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="millisecondsTimeout"/> is a negative number other
        /// than -1, which represents an infinite time-out.
        /// </exception>
        public bool Wait(int millisecondsTimeout)
        {
            return this.GetWaitTask().Wait(millisecondsTimeout);
        }

        /// <summary>
        /// Waits for the <see cref="ManualResetEventAsync"/> to be signaled.
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// The number of milliseconds to wait, or
        /// <see cref="System.Threading.Timeout.Infinite"/> (-1) to wait
        /// indefinitely.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> to observe while waiting for the
        /// <see cref="ManualResetEventAsync"/> to be signaled.
        /// </param>
        /// <returns>
        /// true if the <see cref="ManualResetEventAsync"/> was signaled within
        /// the allotted time; otherwise, false.
        /// </returns>
        /// <exception cref="T:System.AggregateException">
        /// The <see cref="ManualResetEventAsync"/> waiting <see cref="Task"/>
        /// was canceled -or- an exception was thrown during the execution of
        /// the <see cref="ManualResetEventAsync"/> waiting <see cref="Task"/>.
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="millisecondsTimeout"/> is a negative number other
        /// than -1, which represents an infinite time-out.
        /// </exception>
        /// <exception cref="T:System.OperationCanceledException">
        /// The <paramref name="cancellationToken"/> was canceled.
        /// </exception>
        public bool Wait(int millisecondsTimeout, CancellationToken cancellationToken)
        {
            return this.GetWaitTask().Wait(millisecondsTimeout, cancellationToken);
        }

        /// <summary>
        /// Waits for the <see cref="ManualResetEventAsync"/> to be signaled.
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// The number of milliseconds to wait, or
        /// <see cref="System.Threading.Timeout.Infinite"/> (-1) to wait
        /// indefinitely.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> to observe while waiting for the
        /// <see cref="ManualResetEventAsync"/> to be signaled.
        /// </param>
        /// <param name="canceled">
        /// Set to true if the wait was canceled via the <paramref
        /// name="cancellationToken"/>.
        /// </param>
        /// <returns>
        /// true if the <see cref="ManualResetEventAsync"/> was signaled within
        /// the allotted time; otherwise, false.
        /// </returns>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="millisecondsTimeout"/> is a negative number other
        /// than -1, which represents an infinite time-out.
        /// </exception>
        public bool Wait(
            int millisecondsTimeout,
            CancellationToken cancellationToken,
            out bool canceled)
        {
            bool ret = false;
            try
            {
                ret = this.GetWaitTask().Wait(millisecondsTimeout, cancellationToken);
                canceled = false;
            }
            catch (Exception ex)
                when (ex is OperationCanceledException
                    || (ex is AggregateException
                        && ex.InnerOf<OperationCanceledException>() != null))
            {
                canceled = true;
            }

            return ret;
        }
    }

    public static class Extensions
    {
        /// <summary>
        /// Finds the first exception of the requested type.
        /// </summary>
        /// <typeparam name="T">
        /// The type of exception to return
        /// </typeparam>
        /// <param name="ex">
        /// The exception to look in.
        /// </param>
        /// <returns>
        /// The exception or the first inner exception that matches the
        /// given type; null if not found.
        /// </returns>
        public static T InnerOf<T>(this Exception ex)
            where T : Exception
        {
            return (T)InnerOf(ex, typeof(T));
        }

        /// <summary>
        /// Finds the first exception of the requested type.
        /// </summary>
        /// <param name="ex">
        /// The exception to look in.
        /// </param>
        /// <param name="t">
        /// The type of exception to return
        /// </param>
        /// <returns>
        /// The exception or the first inner exception that matches the
        /// given type; null if not found.
        /// </returns>
        public static Exception InnerOf(this Exception ex, Type t)
        {
            if (ex == null || t.IsInstanceOfType(ex))
            {
                return ex;
            }

            var ae = ex as AggregateException;
            if (ae != null)
            {
                foreach (var e in ae.InnerExceptions)
                {
                    var ret = InnerOf(e, t);
                    if (ret != null)
                    {
                        return ret;
                    }
                }
            }

            return InnerOf(ex.InnerException, t);
        }
    }
}
