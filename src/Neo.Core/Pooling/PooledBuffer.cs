// Copyright (C) 2015-2025 The Neo Project.
//
// PooledBuffer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Buffers;

namespace Neo.Core.Pooling;

/// <summary>
/// Provides a pooled buffer for temporary byte array allocations.
/// Uses ArrayPool internally to reduce GC pressure from frequent byte array allocations.
/// </summary>
public sealed class PooledBuffer : IPooledObject, IDisposable
{
    private byte[]? _buffer;
    private int _length;
    private bool _disposed;

    /// <summary>
    /// Gets the underlying byte array. May be larger than the requested size.
    /// </summary>
    public byte[] Buffer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _buffer ?? throw new InvalidOperationException("Buffer not initialized. Call Rent first.");
        }
    }

    /// <summary>
    /// Gets the actual length of data in the buffer.
    /// </summary>
    public int Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _length;
        }
    }

    /// <summary>
    /// Gets a span view of the buffer with the actual data length.
    /// </summary>
    public Span<byte> Span
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new Span<byte>(_buffer, 0, _length);
        }
    }

    /// <summary>
    /// Gets a memory view of the buffer with the actual data length.
    /// </summary>
    public Memory<byte> Memory
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new Memory<byte>(_buffer, 0, _length);
        }
    }

    /// <summary>
    /// Rents a buffer from the shared array pool with at least the specified minimum length.
    /// </summary>
    /// <param name="minimumLength">The minimum length of the buffer needed.</param>
    /// <returns>This instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when minimumLength is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a buffer is already rented.</exception>
    public PooledBuffer Rent(int minimumLength)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (minimumLength < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumLength), "Length must be non-negative");

        if (_buffer != null)
            throw new InvalidOperationException("Buffer already rented. Call Return first.");

        _buffer = ArrayPool<byte>.Shared.Rent(minimumLength);
        _length = minimumLength;

        return this;
    }

    /// <summary>
    /// Returns the rented buffer to the shared array pool.
    /// </summary>
    /// <param name="clearArray">If true, clears the array before returning to the pool.</param>
    public void Return(bool clearArray = false)
    {
        if (_buffer != null)
        {
            ArrayPool<byte>.Shared.Return(_buffer, clearArray);
            _buffer = null;
            _length = 0;
        }
    }

    /// <summary>
    /// Resets the pooled buffer to its initial state.
    /// </summary>
    public void Reset()
    {
        Return(clearArray: false);
        _disposed = false;
    }

    /// <summary>
    /// Disposes the pooled buffer, returning it to the array pool.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Return(clearArray: false);
            _disposed = true;
        }
    }

    /// <summary>
    /// Creates a new PooledBuffer and rents a buffer of the specified size.
    /// </summary>
    /// <param name="minimumLength">The minimum length of the buffer needed.</param>
    /// <returns>A new PooledBuffer instance with a rented buffer.</returns>
    public static PooledBuffer Create(int minimumLength)
    {
        var buffer = new PooledBuffer();
        buffer.Rent(minimumLength);
        return buffer;
    }

    /// <summary>
    /// Copies data from a source span into a new pooled buffer.
    /// </summary>
    /// <param name="source">The source data to copy.</param>
    /// <returns>A new PooledBuffer containing a copy of the source data.</returns>
    public static PooledBuffer CopyFrom(ReadOnlySpan<byte> source)
    {
        var buffer = Create(source.Length);
        source.CopyTo(buffer.Span);
        return buffer;
    }
}
