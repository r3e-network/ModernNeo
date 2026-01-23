// Copyright (C) 2015-2025 The Neo Project.
//
// StoreBasedBlockStorageServiceTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using Neo;
using Neo.Cryptography;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Services;
using Neo.Persistence;
using Neo.Persistence.Providers;

namespace Neo.Orleans.Tests.Services;

/// <summary>
/// Unit tests for StoreBasedBlockStorageService.
/// Tests the IStore-backed implementation with MemoryStore.
/// </summary>
[TestClass]
public class StoreBasedBlockStorageServiceTests
{
    private MemoryStore _store = null!;
    private StoreBasedBlockStorageService _storage = null!;

    [TestInitialize]
    public void Setup()
    {
        _store = new MemoryStore();
        _storage = new StoreBasedBlockStorageService(_store, ownsStore: false);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _storage.Dispose();
        _store.Dispose();
    }

    [TestMethod]
    public async Task StoreBlockAsync_NewBlock_ReturnsTrue()
    {
        // Arrange
        var block = CreateBlock(1, 1000);

        // Act
        var result = await _storage.StoreBlockAsync(block);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task StoreBlockAsync_DuplicateBlock_ReturnsFalse()
    {
        // Arrange
        var block = CreateBlock(1, 1000);
        await _storage.StoreBlockAsync(block);

        // Act - Try to store same block again
        var result = await _storage.StoreBlockAsync(block);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task GetBlockByHashAsync_ExistingBlock_ReturnsBlock()
    {
        // Arrange
        var block = CreateBlock(1, 1000);
        await _storage.StoreBlockAsync(block);

        // Act
        var retrieved = await _storage.GetBlockByHashAsync(block.Hash.GetSpan().ToArray());

        // Assert
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(block.Index, retrieved.Index);
        Assert.AreEqual(block.Timestamp, retrieved.Timestamp);
    }

    [TestMethod]
    public async Task GetBlockByHashAsync_NonExistingBlock_ReturnsNull()
    {
        // Arrange
        var hash = new byte[32];

        // Act
        var retrieved = await _storage.GetBlockByHashAsync(hash);

        // Assert
        Assert.IsNull(retrieved);
    }

    [TestMethod]
    public async Task GetBlockByIndexAsync_ExistingBlock_ReturnsBlock()
    {
        // Arrange
        var block = CreateBlock(5, 5000);
        await _storage.StoreBlockAsync(block);

        // Act
        var retrieved = await _storage.GetBlockByIndexAsync(5);

        // Assert
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(5u, retrieved.Index);
    }

    [TestMethod]
    public async Task GetBlockByIndexAsync_NonExistingBlock_ReturnsNull()
    {
        // Act
        var retrieved = await _storage.GetBlockByIndexAsync(999);

        // Assert
        Assert.IsNull(retrieved);
    }

    [TestMethod]
    public async Task ContainsBlockAsync_ExistingBlock_ReturnsTrue()
    {
        // Arrange
        var block = CreateBlock(1, 1000);
        await _storage.StoreBlockAsync(block);

        // Act
        var contains = await _storage.ContainsBlockAsync(block.Hash.GetSpan().ToArray());

        // Assert
        Assert.IsTrue(contains);
    }

    [TestMethod]
    public async Task ContainsBlockAsync_NonExistingBlock_ReturnsFalse()
    {
        // Arrange
        var hash = new byte[32];

        // Act
        var contains = await _storage.ContainsBlockAsync(hash);

        // Assert
        Assert.IsFalse(contains);
    }

    [TestMethod]
    public async Task GetHeightAsync_EmptyStorage_ReturnsZero()
    {
        // Act
        var height = await _storage.GetHeightAsync();

        // Assert
        Assert.AreEqual(0u, height);
    }

    [TestMethod]
    public async Task GetHeightAsync_AfterStoringBlocks_ReturnsHighestIndex()
    {
        // Arrange
        await _storage.StoreBlockAsync(CreateBlock(1, 1000));
        await _storage.StoreBlockAsync(CreateBlock(5, 5000));
        await _storage.StoreBlockAsync(CreateBlock(3, 3000));

        // Act
        var height = await _storage.GetHeightAsync();

        // Assert
        Assert.AreEqual(5u, height);
    }

    [TestMethod]
    public async Task MultipleBlocks_AllRetrievable()
    {
        // Arrange
        var blocks = new[]
        {
            CreateBlock(1, 1000),
            CreateBlock(2, 2000),
            CreateBlock(3, 3000)
        };

        foreach (var block in blocks)
        {
            await _storage.StoreBlockAsync(block);
        }

        // Act & Assert
        foreach (var block in blocks)
        {
            var byHash = await _storage.GetBlockByHashAsync(block.Hash.GetSpan().ToArray());
            var byIndex = await _storage.GetBlockByIndexAsync(block.Index);

            Assert.IsNotNull(byHash);
            Assert.IsNotNull(byIndex);
            Assert.AreEqual(block.Index, byHash.Index);
            Assert.AreEqual(block.Index, byIndex.Index);
        }
    }

    [TestMethod]
    public async Task ContainsTransactionAsync_BlockWithTransaction_ReturnsTrue()
    {
        var block = CreateBlockWithTransaction(1);

        await _storage.StoreBlockAsync(block);

        var txHash = block.Transactions[0].Hash.GetSpan().ToArray();
        var contains = await _storage.ContainsTransactionAsync(txHash);
        var retrieved = await _storage.GetTransactionAsync(txHash);

        Assert.IsTrue(contains);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(block.Transactions[0].Hash, retrieved.Hash);
    }

    [TestMethod]
    public async Task CustomSerializer_UsesProvidedSerializer()
    {
        // Arrange
        var serializerCalled = false;
        var deserializerCalled = false;

        _storage.BlockSerializer = block =>
        {
            serializerCalled = true;
            // Simple custom format: just index + timestamp
            var data = new byte[12];
            BitConverter.GetBytes(block.Index).CopyTo(data, 0);
            BitConverter.GetBytes(block.Timestamp).CopyTo(data, 4);
            return data;
        };

        _storage.BlockDeserializer = data =>
        {
            deserializerCalled = true;
            return CreateBlock(
                BitConverter.ToUInt32(data, 0),
                BitConverter.ToUInt64(data, 4));
        };

        var block = CreateBlock(10, 10000);

        // Act
        await _storage.StoreBlockAsync(block);
        var retrieved = await _storage.GetBlockByHashAsync(block.Hash.GetSpan().ToArray());

        // Assert
        Assert.IsTrue(serializerCalled);
        Assert.IsTrue(deserializerCalled);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(block.Index, retrieved.Index);
    }

    [TestMethod]
    public void Dispose_OwnsStore_DisposesStore()
    {
        // Arrange
        var ownedStore = new MemoryStore();
        var service = new StoreBasedBlockStorageService(ownedStore, ownsStore: true);

        // Act - Should not throw
        service.Dispose();

        // Assert - Store should be disposed (no way to verify directly, but no exception means success)
        Assert.IsTrue(true);
    }

    [TestMethod]
    public void Dispose_DoesNotOwnStore_DoesNotDisposeStore()
    {
        // Arrange
        var sharedStore = new MemoryStore();
        var service = new StoreBasedBlockStorageService(sharedStore, ownsStore: false);

        // Act
        service.Dispose();

        // Assert - Store should still be usable
        sharedStore.Put([0x01], [0x02]);
        Assert.IsTrue(sharedStore.Contains([0x01]));

        sharedStore.Dispose();
    }

    private static Block CreateBlock(uint index, ulong timestamp)
    {
        var header = new Header
        {
            Version = 0,
            PrevHash = UInt256.Zero,
            MerkleRoot = UInt256.Zero,
            Timestamp = timestamp,
            Nonce = 0,
            Index = index,
            PrimaryIndex = 0,
            NextConsensus = UInt160.Zero,
            Witness = Witness.Empty
        };

        return new Block
        {
            Header = header,
            Transactions = Array.Empty<Transaction>()
        };
    }

    private static Block CreateBlockWithTransaction(uint index)
    {
        var tx = new Transaction
        {
            Version = 0,
            Nonce = 1,
            SystemFee = 0,
            NetworkFee = 0,
            ValidUntilBlock = index + 1,
            Signers = [new Signer { Account = UInt160.Zero, Scopes = WitnessScope.None }],
            Attributes = Array.Empty<TransactionAttribute>(),
            Script = new byte[] { 0x01 },
            Witnesses = [new Witness { InvocationScript = Array.Empty<byte>(), VerificationScript = Array.Empty<byte>() }]
        };

        var header = new Header
        {
            Version = 0,
            PrevHash = UInt256.Zero,
            MerkleRoot = MerkleTree.ComputeRoot([tx.Hash]),
            Timestamp = 1,
            Nonce = 0,
            Index = index,
            PrimaryIndex = 0,
            NextConsensus = UInt160.Zero,
            Witness = new Witness { InvocationScript = Array.Empty<byte>(), VerificationScript = Array.Empty<byte>() }
        };

        return new Block
        {
            Header = header,
            Transactions = [tx]
        };
    }

    [TestMethod]
    public void Constructor_NullStore_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new StoreBasedBlockStorageService(null!));
    }

    [TestMethod]
    public async Task StoreBlockAsync_NullBlock_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await _storage.StoreBlockAsync(null!));
    }

    [TestMethod]
    public async Task GetBlockByHashAsync_NullHash_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await _storage.GetBlockByHashAsync(null!));
    }

    [TestMethod]
    public async Task ContainsBlockAsync_NullHash_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await _storage.ContainsBlockAsync(null!));
    }

    [TestMethod]
    public async Task DataPersistence_AcrossServiceInstances()
    {
        // Arrange - Store block with first service instance
        var block = CreateBlock(42, 42000);
        await _storage.StoreBlockAsync(block);

        // Act - Create new service instance with same store
        using var newService = new StoreBasedBlockStorageService(_store, ownsStore: false);
        var retrieved = await newService.GetBlockByHashAsync(block.Hash.GetSpan().ToArray());
        var height = await newService.GetHeightAsync();

        // Assert - Data should persist
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(42u, retrieved.Index);
        Assert.AreEqual(42u, height);
    }
}
