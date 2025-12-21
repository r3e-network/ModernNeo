// Copyright (C) 2015-2025 The Neo Project.
//
// ObjectPool.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Neo.Core.Pooling;

/// <summary>
/// A thread-safe object pool implementation that follows the Microsoft.Extensions.ObjectPool pattern.
/// Reduces GC pressure by reusing objects instead of allocating new ones.
/// </summary>
/// <typeparam name="T">The type of objects to pool. Must have a parameterless constructor.</typeparam>
public sealed class ObjectPool<T> where T : class, new()
{
    private readonly ConcurrentBag<T> _items = new();
    private readonly Func<T> _factory;
    private readonly Action<T>? _resetAction;
    private readonly int _maxSize;
    private int _count;

    /// <summary>
    /// Gets the maximum number of objects that can be stored in the pool.
    /// </summary>
    public int MaxSize => _maxSize;

    /// <summary>
    /// Gets the approximate current number of objects in the pool.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectPool{T}"/> class with default settings.
    /// </summary>
    public ObjectPool() : this(Environment.ProcessorCount * 2)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectPool{T}"/> class with a specified maximum size.
    /// </summary>
    /// <param name="maxSize">The maximum number of objects to retain in the pool.</param>
    public ObjectPool(int maxSize) : this(maxSize, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectPool{T}"/> class with custom factory and reset logic.
    /// </summary>
    /// <param name="maxSize">The maximum number of objects to retain in the pool.</param>
    /// <param name="factory">Optional factory function to create new instances. If null, uses default constructor.</param>
    /// <param name="resetAction">Optional action to reset objects when returned to the pool.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when maxSize is less than 1.</exception>
    public ObjectPool(int maxSize, Func<T>? factory, Action<T>? resetAction)
    {
        if (maxSize < 1)
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Pool size must be at least 1");

        _maxSize = maxSize;
        _factory = factory ?? (() => new T());
        _resetAction = resetAction;
    }

    /// <summary>
    /// Gets an object from the pool or creates a new one if the pool is empty.
    /// </summary>
    /// <returns>An object instance from the pool or a newly created instance.</returns>
    public T Get()
    {
        if (_items.TryTake(out var item))
        {
            Interlocked.Decrement(ref _count);
            return item;
        }

        return _factory();
    }

    /// <summary>
    /// Returns an object to the pool for reuse.
    /// If the pool is at maximum capacity, the object is discarded.
    /// </summary>
    /// <param name="item">The object to return to the pool.</param>
    /// <exception cref="ArgumentNullException">Thrown when item is null.</exception>
    public void Return(T item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        // Reset the object if it implements IPooledObject
        if (item is IPooledObject pooledObject)
        {
            pooledObject.Reset();
        }

        // Apply custom reset action if provided
        _resetAction?.Invoke(item);

        // Only add back to pool if under max size
        if (Interlocked.Increment(ref _count) <= _maxSize)
        {
            _items.Add(item);
        }
        else
        {
            // Over capacity, decrement and discard
            Interlocked.Decrement(ref _count);
        }
    }

    /// <summary>
    /// Clears all objects from the pool.
    /// </summary>
    public void Clear()
    {
        while (_items.TryTake(out _))
        {
            Interlocked.Decrement(ref _count);
        }
    }
}
