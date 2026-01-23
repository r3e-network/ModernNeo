// Copyright (C) 2015-2025 The Neo Project.
//
// NeoSystem.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.IO.Caching;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.P2P.Abstractions;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo
{
    /// <summary>
    /// Represents a message target that can receive messages.
    /// This is a transport-agnostic abstraction for message targets.
    /// </summary>
    public interface ISystemMessageTarget : IMessageTarget
    {
        /// <summary>
        /// Sends a message and waits for a response.
        /// </summary>
        Task<TResponse> Ask<TResponse>(object message, TimeSpan? timeout = null);
    }

    /// <summary>
    /// Simple in-process message target implementation.
    /// </summary>
    internal class InProcessMessageTarget : ISystemMessageTarget
    {
        private readonly Action<object> _handler;
        private readonly Func<object, Task<object>>? _askHandler;

        public InProcessMessageTarget(Action<object> handler, Func<object, Task<object>>? askHandler = null)
        {
            _handler = handler;
            _askHandler = askHandler;
        }

        public void Tell(object message) => _handler(message);

        public async Task<TResponse> Ask<TResponse>(object message, TimeSpan? timeout = null)
        {
            if (_askHandler == null)
                throw new NotSupportedException("Ask pattern not supported for this target");
            var result = await _askHandler(message);
            return (TResponse)result;
        }
    }

    /// <summary>
    /// Represents the basic unit that contains all the components required for running of a NEO node.
    /// This is the Orleans-compatible version without legacy actor dependencies.
    /// </summary>
    public class NeoSystem : IDisposable, Ledger.IBlockchainOperations
    {
        /// <summary>
        /// Triggered when a service is added to the <see cref="NeoSystem"/>.
        /// </summary>
        public event EventHandler<object>? ServiceAdded;

        /// <summary>
        /// The protocol settings of the <see cref="NeoSystem"/>.
        /// </summary>
        public ProtocolSettings Settings { get; }

        /// <summary>
        /// The genesis block of the NEO blockchain.
        /// </summary>
        public Block GenesisBlock { get; }

        /// <summary>
        /// The blockchain message target of the <see cref="NeoSystem"/>.
        /// </summary>
        public ISystemMessageTarget Blockchain { get; private set; } = null!;

        /// <summary>
        /// The local node message target of the <see cref="NeoSystem"/>.
        /// </summary>
        public ISystemMessageTarget LocalNode { get; private set; } = null!;

        /// <summary>
        /// The task manager message target of the <see cref="NeoSystem"/>.
        /// </summary>
        public ISystemMessageTarget TaskManager { get; private set; } = null!;

        /// <summary>
        /// The transaction router message target of the <see cref="NeoSystem"/>.
        /// </summary>
        public ISystemMessageTarget TxRouter { get; private set; } = null!;

        /// <summary>
        /// A readonly view of the store.
        /// </summary>
        /// <remarks>
        /// It doesn't need to be disposed because the <see cref="IStoreSnapshot"/> inside it is null.
        /// </remarks>
        public StoreCache StoreView => new(_store);

        /// <summary>
        /// The memory pool of the <see cref="NeoSystem"/>.
        /// </summary>
        public MemoryPool MemPool { get; }

        /// <summary>
        /// The header cache of the <see cref="NeoSystem"/>.
        /// </summary>
        public HeaderCache HeaderCache { get; } = [];

        internal RelayCache RelayCache { get; } = new(100);
        protected IStoreProvider StorageProvider { get; }

        private ImmutableList<object> _services = ImmutableList<object>.Empty;
        private readonly IStore _store;
        private ChannelsConfig? _startMessage = null;
        private int _suspend = 0;
        private bool _disposed = false;

        static NeoSystem()
        {
            // Unify unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            Plugin.LoadPlugins();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NeoSystem"/> class.
        /// </summary>
        /// <param name="settings">The protocol settings of the <see cref="NeoSystem"/>.</param>
        /// <param name="storageProvider">
        /// The storage engine used to create the <see cref="IStoreProvider"/> objects.
        /// If this parameter is <see langword="null"/>, a default in-memory storage engine will be used.
        /// </param>
        /// <param name="storagePath">
        /// The path of the storage.
        /// If <paramref name="storageProvider"/> is the default in-memory storage engine, this parameter is ignored.
        /// </param>
        public NeoSystem(ProtocolSettings settings, string? storageProvider = null, string? storagePath = null) :
            this(settings, StoreFactory.GetStoreProvider(storageProvider ?? nameof(MemoryStore))
                ?? throw new ArgumentException($"Can't find the storage provider {storageProvider}", nameof(storageProvider)), storagePath)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NeoSystem"/> class.
        /// </summary>
        /// <param name="settings">The protocol settings of the <see cref="NeoSystem"/>.</param>
        /// <param name="storageProvider">The <see cref="IStoreProvider"/> to use.</param>
        /// <param name="storagePath">
        /// The path of the storage.
        /// If <paramref name="storageProvider"/> is the default in-memory storage engine, this parameter is ignored.
        /// </param>
        public NeoSystem(ProtocolSettings settings, IStoreProvider storageProvider, string? storagePath = null)
        {
            Settings = settings;
            GenesisBlock = CreateGenesisBlock(settings);
            StorageProvider = storageProvider;
            _store = storageProvider.GetStore(storagePath);
            MemPool = new MemoryPool(this);

            // Initialize message targets with placeholder handlers
            // In Orleans mode, these will be replaced with grain references
            InitializeMessageTargets();

            foreach (var plugin in Plugin.Plugins)
                plugin.OnSystemLoaded(this);
        }

        /// <summary>
        /// Initializes message targets with default in-process handlers.
        /// Override this in derived classes for Orleans integration.
        /// </summary>
        protected virtual void InitializeMessageTargets()
        {
            // Default no-op handlers - will be replaced by Orleans grains or other implementations
            Blockchain = new InProcessMessageTarget(
                msg => Utility.Log(nameof(Blockchain), LogLevel.Debug, $"Message: {msg?.GetType().Name}"),
                async msg =>
                {
                    if (msg is Ledger.Blockchain.Initialize)
                        return true;
                    return await Task.FromResult<object>(null!);
                });

            LocalNode = new InProcessMessageTarget(
                msg => Utility.Log(nameof(LocalNode), LogLevel.Debug, $"Message: {msg?.GetType().Name}"));

            TaskManager = new InProcessMessageTarget(
                msg => Utility.Log(nameof(TaskManager), LogLevel.Debug, $"Message: {msg?.GetType().Name}"));

            TxRouter = new InProcessMessageTarget(
                msg => Utility.Log(nameof(TxRouter), LogLevel.Debug, $"Message: {msg?.GetType().Name}"));
        }

        /// <summary>
        /// Sets the blockchain message target (for Orleans integration).
        /// </summary>
        public void SetBlockchain(ISystemMessageTarget target) => Blockchain = target;

        /// <summary>
        /// Sets the local node message target (for Orleans integration).
        /// </summary>
        public void SetLocalNode(ISystemMessageTarget target) => LocalNode = target;

        /// <summary>
        /// Sets the task manager message target (for Orleans integration).
        /// </summary>
        public void SetTaskManager(ISystemMessageTarget target) => TaskManager = target;

        /// <summary>
        /// Sets the transaction router message target (for Orleans integration).
        /// </summary>
        public void SetTxRouter(ISystemMessageTarget target) => TxRouter = target;

        /// <summary>
        /// Creates the genesis block for the NEO blockchain.
        /// </summary>
        /// <param name="settings">The <see cref="ProtocolSettings"/> of the NEO system.</param>
        /// <returns>The genesis block.</returns>
        public static Block CreateGenesisBlock(ProtocolSettings settings) => new()
        {
            Header = new Header
            {
                PrevHash = UInt256.Zero,
                MerkleRoot = UInt256.Zero,
                Timestamp = (new DateTime(2016, 7, 15, 15, 8, 21, DateTimeKind.Utc)).ToTimestampMS(),
                Nonce = 2083236893, // nonce from the Bitcoin genesis block.
                Index = 0,
                PrimaryIndex = 0,
                NextConsensus = Contract.GetBFTAddress(settings.StandbyValidators),
                Witness = new Witness
                {
                    InvocationScript = ReadOnlyMemory<byte>.Empty,
                    VerificationScript = new[] { (byte)OpCode.PUSH1 }
                },
            },
            Transactions = [],
        };

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Utility.Log("UnhandledException", LogLevel.Fatal, e.ExceptionObject);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var p in Plugin.Plugins)
                p.Dispose();

            HeaderCache.Dispose();
            _store.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Adds a service to the <see cref="NeoSystem"/>.
        /// </summary>
        /// <param name="service">The service object to be added.</param>
        public void AddService(object service)
        {
            ImmutableInterlocked.Update(ref _services, p => p.Add(service));
            ServiceAdded?.Invoke(this, service);
        }

        /// <summary>
        /// Gets a specified type of service object from the <see cref="NeoSystem"/>.
        /// </summary>
        /// <typeparam name="T">The type of the service object.</typeparam>
        /// <param name="filter">
        /// An action used to filter the service objects. This parameter can be <see langword="null"/>.
        /// </param>
        /// <returns>The service object found.</returns>
        public T? GetService<T>(Func<T, bool>? filter = null)
        {
            var result = _services.OfType<T>();
            if (filter is null)
                return result.FirstOrDefault();
            return result.FirstOrDefault(filter);
        }

        /// <summary>
        /// Loads an <see cref="IStore"/> at the specified path.
        /// </summary>
        /// <param name="path">The path of the storage.</param>
        /// <returns>The loaded <see cref="IStore"/>.</returns>
        public IStore LoadStore(string path)
        {
            return StorageProvider.GetStore(path);
        }

        /// <summary>
        /// Resumes the startup process of <see cref="LocalNode"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the startup process is resumed; otherwise, <see langword="false"/>. </returns>
        public bool ResumeNodeStartup()
        {
            if (Interlocked.Decrement(ref _suspend) != 0)
                return false;
            if (_startMessage != null)
            {
                LocalNode.Tell(_startMessage);
                _startMessage = null;
            }
            return true;
        }

        /// <summary>
        /// Starts the <see cref="LocalNode"/> with the specified configuration.
        /// </summary>
        /// <param name="config">The configuration used to start the <see cref="LocalNode"/>.</param>
        public void StartNode(ChannelsConfig config)
        {
            _startMessage = config;

            if (_suspend == 0)
            {
                LocalNode.Tell(_startMessage);
                _startMessage = null;
            }
        }

        /// <summary>
        /// Suspends the startup process of <see cref="LocalNode"/>.
        /// </summary>
        public void SuspendNodeStartup()
        {
            Interlocked.Increment(ref _suspend);
        }

        /// <summary>
        /// Gets a snapshot of the blockchain storage.
        /// </summary>
        /// <returns>An instance of <see cref="StoreCache"/></returns>
        [Obsolete("This method is obsolete, use GetSnapshotCache instead.")]
        public StoreCache GetSnapshot()
        {
            return new StoreCache(_store.GetSnapshot());
        }

        /// <summary>
        /// Gets a snapshot of the blockchain storage with an execution cache.
        /// With the snapshot, we have the latest state of the blockchain, with the cache,
        /// we can run transactions in a sandboxed environment.
        /// </summary>
        /// <returns>An instance of <see cref="StoreCache"/></returns>
        public StoreCache GetSnapshotCache()
        {
            return new StoreCache(_store.GetSnapshot());
        }

        /// <summary>
        /// Determines whether the specified transaction exists in the memory pool or storage.
        /// </summary>
        /// <param name="hash">The hash of the transaction</param>
        /// <returns><see langword="true"/> if the transaction exists; otherwise, <see langword="false"/>.</returns>
        public ContainsTransactionType ContainsTransaction(UInt256 hash)
        {
            if (MemPool.ContainsKey(hash)) return ContainsTransactionType.ExistsInPool;
            return NativeContract.Ledger.ContainsTransaction(StoreView, hash) ?
                ContainsTransactionType.ExistsInLedger : ContainsTransactionType.NotExist;
        }

        /// <summary>
        /// Determines whether the specified transaction conflicts with some on-chain transaction.
        /// </summary>
        /// <param name="hash">The hash of the transaction</param>
        /// <param name="signers">The list of signer accounts of the transaction</param>
        /// <returns>
        /// <see langword="true"/> if the transaction conflicts with on-chain transaction; otherwise, <see langword="false"/>.
        /// </returns>
        public bool ContainsConflictHash(UInt256 hash, IEnumerable<UInt160> signers)
        {
            return NativeContract.Ledger.ContainsConflictHash(StoreView, hash, signers, this.GetMaxTraceableBlocks());
        }
    }
}
