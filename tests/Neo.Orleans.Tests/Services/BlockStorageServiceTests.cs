using Neo;
using Neo.Core.Interfaces;
using Neo.IO;
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
        var block = new TestBlockData(1, 1000);

        // Act
        var result = await _storage.StoreBlockAsync(block);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task StoreBlockAsync_DuplicateBlock_ReturnsFalse()
    {
        // Arrange
        var block = new TestBlockData(1, 1000);
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
        var block = new TestBlockData(1, 1000);
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
        var block = new TestBlockData(5, 5000);
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
        var block = new TestBlockData(1, 1000);
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
        await _storage.StoreBlockAsync(new TestBlockData(1, 1000));
        await _storage.StoreBlockAsync(new TestBlockData(5, 5000));
        await _storage.StoreBlockAsync(new TestBlockData(3, 3000));

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
            new TestBlockData(1, 1000),
            new TestBlockData(2, 2000),
            new TestBlockData(3, 3000)
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
}

/// <summary>
/// Test implementation of IBlockData.
/// </summary>
internal class TestBlockData : IBlockData
{
    private readonly byte[] _hashBytes;

    public TestBlockData(uint index, ulong timestamp)
    {
        Index = index;
        Timestamp = timestamp;
        _hashBytes = new byte[32];
        BitConverter.GetBytes(index).CopyTo(_hashBytes, 0);
        BitConverter.GetBytes(timestamp).CopyTo(_hashBytes, 4);
        Hash = new UInt256(_hashBytes);
        PrevHash = UInt256.Zero;
        MerkleRoot = UInt256.Zero;
        NextConsensus = UInt160.Zero;
    }

    public UInt256 Hash { get; }
    public uint Version => 0;
    public UInt256 PrevHash { get; }
    public UInt256 MerkleRoot { get; }
    public ulong Timestamp { get; }
    public ulong Nonce => 0;
    public uint Index { get; }
    public byte PrimaryIndex => 0;
    public UInt160 NextConsensus { get; }
    public int TransactionsCount => 0;
    public int Size => 0;

    public void Deserialize(ref MemoryReader reader) { }
    public void DeserializeUnsigned(ref MemoryReader reader) { }
    public void Serialize(System.IO.BinaryWriter writer) { }
    public void SerializeUnsigned(System.IO.BinaryWriter writer) { }
}
