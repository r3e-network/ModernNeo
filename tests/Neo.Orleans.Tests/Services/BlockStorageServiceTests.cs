using Neo;
using Neo.Cryptography;
using Neo.Network.P2P.Payloads;
using Neo.Orleans.Services;

namespace Neo.Orleans.Tests.Services;

/// <summary>
/// Unit tests for InMemoryBlockStorageService.
/// </summary>
[TestClass]
public class BlockStorageServiceTests
{
    private InMemoryBlockStorageService _storage = null!;

    [TestInitialize]
    public void Setup()
    {
        _storage = new InMemoryBlockStorageService();
    }

    [TestMethod]
    public async Task StoreBlockAsync_NewBlock_ReturnsTrue()
    {
        // Arrange
        var block = BlockStorageTestData.CreateBlock(1, 1000);

        // Act
        var result = await _storage.StoreBlockAsync(block);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task StoreBlockAsync_DuplicateBlock_ReturnsFalse()
    {
        // Arrange
        var block = BlockStorageTestData.CreateBlock(1, 1000);
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
        var block = BlockStorageTestData.CreateBlock(1, 1000);
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
        var block = BlockStorageTestData.CreateBlock(5, 5000);
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
        var block = BlockStorageTestData.CreateBlock(1, 1000);
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
        await _storage.StoreBlockAsync(BlockStorageTestData.CreateBlock(1, 1000));
        await _storage.StoreBlockAsync(BlockStorageTestData.CreateBlock(5, 5000));
        await _storage.StoreBlockAsync(BlockStorageTestData.CreateBlock(3, 3000));

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
            BlockStorageTestData.CreateBlock(1, 1000),
            BlockStorageTestData.CreateBlock(2, 2000),
            BlockStorageTestData.CreateBlock(3, 3000)
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
        var block = BlockStorageTestData.CreateBlockWithTransaction(1);

        await _storage.StoreBlockAsync(block);

        var txHash = block.Transactions[0].Hash.GetSpan().ToArray();
        var contains = await _storage.ContainsTransactionAsync(txHash);
        var retrieved = await _storage.GetTransactionAsync(txHash);

        Assert.IsTrue(contains);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(block.Transactions[0].Hash, retrieved.Hash);
    }
}

internal static class BlockStorageTestData
{
    internal static Block CreateBlock(uint index, ulong timestamp)
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

    internal static Block CreateBlockWithTransaction(uint index)
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
}
