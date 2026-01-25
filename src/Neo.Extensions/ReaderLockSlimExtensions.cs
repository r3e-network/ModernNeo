// Copyright (C) 2015-2025 The Neo Project.
//
// ReaderLockSlimExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Neo.Extensions
{
    /// <summary>
    /// Provides extension methods for <see cref="ReaderWriterLockSlim"/> to simplify lock usage.
    /// </summary>
    public static class ReaderLockSlimExtensions
    {
        /// <summary>
        /// Executes the specified function under a read lock.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="lock">The lock to use.</param>
        /// <param name="func">The function to execute.</param>
        /// <returns>The result of the function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(this ReaderWriterLockSlim @lock, Func<T> func)
        {
            @lock.EnterReadLock();
            try
            {
                return func();
            }
            finally
            {
                @lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Executes the specified action under a read lock.
        /// </summary>
        /// <param name="lock">The lock to use.</param>
        /// <param name="action">The action to execute.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Read(this ReaderWriterLockSlim @lock, Action action)
        {
            @lock.EnterReadLock();
            try
            {
                action();
            }
            finally
            {
                @lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Executes the specified function under an upgradeable read lock.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="lock">The lock to use.</param>
        /// <param name="func">The function to execute.</param>
        /// <returns>The result of the function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T UpgradeableRead<T>(this ReaderWriterLockSlim @lock, Func<T> func)
        {
            @lock.EnterUpgradeableReadLock();
            try
            {
                return func();
            }
            finally
            {
                @lock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Executes the specified action under an upgradeable read lock.
        /// </summary>
        /// <param name="lock">The lock to use.</param>
        /// <param name="action">The action to execute.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpgradeableRead(this ReaderWriterLockSlim @lock, Action action)
        {
            @lock.EnterUpgradeableReadLock();
            try
            {
                action();
            }
            finally
            {
                @lock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Executes the specified function under a write lock.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="lock">The lock to use.</param>
        /// <param name="func">The function to execute.</param>
        /// <returns>The result of the function.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Write<T>(this ReaderWriterLockSlim @lock, Func<T> func)
        {
            @lock.EnterWriteLock();
            try
            {
                return func();
            }
            finally
            {
                @lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Executes the specified action under a write lock.
        /// </summary>
        /// <param name="lock">The lock to use.</param>
        /// <param name="action">The action to execute.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(this ReaderWriterLockSlim @lock, Action action)
        {
            @lock.EnterWriteLock();
            try
            {
                action();
            }
            finally
            {
                @lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Executes the specified function under an upgradeable read lock that may be upgraded to write lock.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="lock">The lock to use.</param>
        /// <param name="func">The function to execute.</param>
        /// <returns>The result of the function.</returns>
        /// <remarks>
        /// This method allows upgrading from read to write lock within the same scope.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReadWrite<T>(this ReaderWriterLockSlim @lock, Func<T> func)
        {
            @lock.EnterUpgradeableReadLock();
            try
            {
                @lock.EnterWriteLock();
                try
                {
                    return func();
                }
                finally
                {
                    @lock.ExitWriteLock();
                }
            }
            finally
            {
                @lock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Executes the specified action under an upgradeable read lock that may be upgraded to write lock.
        /// </summary>
        /// <param name="lock">The lock to use.</param>
        /// <param name="action">The action to execute.</param>
        /// <remarks>
        /// This method allows upgrading from read to write lock within the same scope.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadWrite(this ReaderWriterLockSlim @lock, Action action)
        {
            @lock.EnterUpgradeableReadLock();
            try
            {
                @lock.EnterWriteLock();
                try
                {
                    action();
                }
                finally
                {
                    @lock.ExitWriteLock();
                }
            }
            finally
            {
                @lock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Executes the specified action under a read lock, with support for upgradable to write lock.
        /// </summary>
        /// <param name="lock">The lock to use.</param>
        /// <param name="action">The action to execute.</param>
        /// <remarks>
        /// This method allows upgrading from read to write lock within the same scope.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadWithUpgrade(this ReaderWriterLockSlim @lock, Action<Action> action)
        {
            @lock.EnterUpgradeableReadLock();
            try
            {
                action(() =>
                {
                    @lock.EnterWriteLock();
                    try
                    {
                    }
                    finally
                    {
                        @lock.ExitWriteLock();
                    }
                });
            }
            finally
            {
                @lock.ExitUpgradeableReadLock();
            }
        }
    }
}
