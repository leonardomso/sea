using System;
using System.Buffers;

namespace SpacetimeDB
{
    internal sealed class PooledReceiveBuffer : IDisposable
    {
        internal const int InitialCapacity = 4 * 1024;
        internal const int RetainedCapacity = 64 * 1024;
        internal const int MaximumCapacity = 64 * 1024 * 1024;

        private byte[] bytes;
        private int count;
        private bool disposed;

        public PooledReceiveBuffer()
        {
            bytes = ArrayPool<byte>.Shared.Rent(InitialCapacity);
        }

        internal int Capacity => bytes.Length;

        internal ArraySegment<byte> WritableSegment
        {
            get
            {
                EnsureNotDisposed();
                return new ArraySegment<byte>(bytes, count, bytes.Length - count);
            }
        }

        internal bool EnsureWritableCapacity()
        {
            EnsureNotDisposed();
            if (count < bytes.Length)
            {
                return true;
            }

            if (bytes.Length >= MaximumCapacity)
            {
                return false;
            }

            var requestedCapacity = Math.Min(
                MaximumCapacity,
                checked(bytes.Length * 2));
            var expanded = ArrayPool<byte>.Shared.Rent(requestedCapacity);
            Buffer.BlockCopy(bytes, 0, expanded, 0, count);
            ArrayPool<byte>.Shared.Return(bytes);
            bytes = expanded;
            return true;
        }

        internal void Advance(int bytesWritten)
        {
            EnsureNotDisposed();
            if (bytesWritten < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bytesWritten));
            }
            if (bytesWritten > bytes.Length - count)
            {
                throw new ArgumentOutOfRangeException(nameof(bytesWritten));
            }

            count += bytesWritten;
        }

        internal byte[] CompleteMessage()
        {
            EnsureNotDisposed();
            var message = new byte[count];
            Buffer.BlockCopy(bytes, 0, message, 0, count);
            count = 0;
            TrimRetainedCapacity();
            return message;
        }

        internal void Reset()
        {
            EnsureNotDisposed();
            count = 0;
            TrimRetainedCapacity();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            ArrayPool<byte>.Shared.Return(bytes);
            bytes = Array.Empty<byte>();
            count = 0;
            disposed = true;
        }

        private void TrimRetainedCapacity()
        {
            if (bytes.Length <= RetainedCapacity)
            {
                return;
            }

            ArrayPool<byte>.Shared.Return(bytes);
            bytes = ArrayPool<byte>.Shared.Rent(InitialCapacity);
        }

        private void EnsureNotDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(PooledReceiveBuffer));
            }
        }
    }
}
