// Copyright (C) 2015-2025 The Neo Project.
//
// CollectionExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Neo.Extensions
{
    /// <summary>
    /// Provides extension methods for collections.
    /// </summary>
    public static class CollectionExtensions
    {
        /// <summary>
        /// Adds a range of items to the collection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                collection.Add(item);
            }
        }

        /// <summary>
        /// Removes a range of items from the collection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveRange<T>(this ICollection<T> collection, IEnumerable<T> items)
        {
            foreach (var item in items.ToList())
            {
                collection.Remove(item);
            }
        }

        /// <summary>
        /// Tries to get a value from the dictionary, returns default if not found.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TValue? GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            return dictionary.TryGetValue(key, out var value) ? value : default;
        }

        /// <summary>
        /// Tries to get a value from the dictionary, returns the specified default if not found.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        {
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Gets or creates a value in the dictionary.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> valueFactory)
        {
            if (dictionary.TryGetValue(key, out var existing))
                return existing;

            var newValue = valueFactory();
            dictionary[key] = newValue;
            return newValue;
        }

        /// <summary>
        /// Safely gets the value at the specified index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? SafeGet<T>(this IList<T> list, int index)
        {
            return index >= 0 && index < list.Count ? list[index] : default;
        }

        /// <summary>
        /// Converts a collection to a hash set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T>? comparer = null)
        {
            return new HashSet<T>(source, comparer ?? EqualityComparer<T>.Default);
        }

        /// <summary>
        /// Checks if the collection is null or empty.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrEmpty<T>(this ICollection<T>? collection)
        {
            return collection == null || collection.Count == 0;
        }

        /// <summary>
        /// Performs an action on each element of the collection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
            {
                action(item);
            }
        }

        /// <summary>
        /// Performs an action on each element with index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ForEach<T>(this IEnumerable<T> source, Action<T, int> action)
        {
            var index = 0;
            foreach (var item in source)
            {
                action(item, index++);
            }
        }

        /// <summary>
        /// Samples random elements from the collection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> Sample<T>(this IEnumerable<T> source, int count)
        {
            var list = source.ToList();
            if (count >= list.Count) return list;

            var random = new Random();
            var result = new List<T>(count);
            var indices = new HashSet<int>();
            while (result.Count < count)
            {
                var index = random.Next(list.Count);
                if (indices.Add(index))
                {
                    result.Add(list[index]);
                }
            }
            return result;
        }

        /// <summary>
        /// Removes all elements that match the predicate.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveWhere<T>(this HashSet<T> set, Func<T, bool> predicate)
        {
            var itemsToRemove = new List<T>();
            foreach (var item in set)
            {
                if (predicate(item))
                {
                    itemsToRemove.Add(item);
                }
            }
            foreach (var item in itemsToRemove)
            {
                set.Remove(item);
            }
        }

        /// <summary>
        /// Removes all elements that match the predicate from the dictionary.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveWhere<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, Func<TKey, bool> predicate) where TKey : notnull
        {
            var keysToRemove = new List<TKey>();
            foreach (var key in dictionary.Keys)
            {
                if (predicate(key))
                {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in keysToRemove)
            {
                dictionary.Remove(key);
            }
        }
    }
}
