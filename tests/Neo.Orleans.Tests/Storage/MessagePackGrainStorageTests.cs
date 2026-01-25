// Copyright (C) 2015-2025 The Neo Project.
//
// MessagePackGrainStorageTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Logging;
using Moq;
using Neo.Orleans.Storage;
using Orleans.Runtime;
using Orleans.Storage;

namespace Neo.Orleans.Tests.Storage
{
    /// <summary>
    /// Unit tests for MessagePackGrainStorage.
    /// Tests serialization/deserialization of grain state using MessagePack.
    /// </summary>
    [TestClass]
    public sealed class MessagePackGrainStorageTests
    {
        private MessagePackGrainStorage _storage = null!;
        private Mock<ILogger<MessagePackGrainStorage>> _loggerMock = null!;

        [TestInitialize]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<MessagePackGrainStorage>>();
            var options = new MessagePackGrainStorageOptions { UseCompression = false };
            _storage = new MessagePackGrainStorage("test", options, _loggerMock.Object);
        }

        #region Basic State Operations

        [TestMethod]
        public async Task ReadStateAsync_NonExistent_ReturnsDefaultState()
        {
            // Arrange
            var grainId = GrainId.Create("test", "grain1");
            var grainState = new TestGrainState<SimpleState>();

            // Act
            await _storage.ReadStateAsync("state", grainId, grainState);

            // Assert
            Assert.IsFalse(grainState.RecordExists);
            Assert.IsNull(grainState.ETag);
        }

        [TestMethod]
        public async Task WriteStateAsync_NewState_Succeeds()
        {
            // Arrange
            var grainId = GrainId.Create("test", "grain1");
            var grainState = new TestGrainState<SimpleState>
            {
                State = new SimpleState { Value = 42, Name = "Test" }
            };

            // Act
            await _storage.WriteStateAsync("state", grainId, grainState);

            // Assert
            Assert.IsTrue(grainState.RecordExists);
            Assert.IsNotNull(grainState.ETag);
        }

        [TestMethod]
        public async Task WriteAndReadStateAsync_RoundTrip_Succeeds()
        {
            // Arrange
            var grainId = GrainId.Create("test", "grain1");
            var originalState = new SimpleState { Value = 123, Name = "RoundTrip" };
            var writeGrainState = new TestGrainState<SimpleState> { State = originalState };

            // Act - Write
            await _storage.WriteStateAsync("state", grainId, writeGrainState);

            // Act - Read
            var readGrainState = new TestGrainState<SimpleState>();
            await _storage.ReadStateAsync("state", grainId, readGrainState);

            // Assert
            Assert.IsTrue(readGrainState.RecordExists);
            Assert.AreEqual(originalState.Value, readGrainState.State.Value);
            Assert.AreEqual(originalState.Name, readGrainState.State.Name);
        }

        [TestMethod]
        public async Task ClearStateAsync_ExistingState_RemovesState()
        {
            // Arrange
            var grainId = GrainId.Create("test", "grain1");
            var grainState = new TestGrainState<SimpleState>
            {
                State = new SimpleState { Value = 42 }
            };
            await _storage.WriteStateAsync("state", grainId, grainState);

            // Act
            await _storage.ClearStateAsync("state", grainId, grainState);

            // Assert
            Assert.IsFalse(grainState.RecordExists);
            Assert.IsNull(grainState.ETag);

            // Verify state is actually cleared
            var readGrainState = new TestGrainState<SimpleState>();
            await _storage.ReadStateAsync("state", grainId, readGrainState);
            Assert.IsFalse(readGrainState.RecordExists);
        }

        #endregion

        #region UInt256 State Tests

        [TestMethod]
        public async Task WriteAndReadStateAsync_UInt256State_RoundTrip()
        {
            // Arrange
            var grainId = GrainId.Create("test", "grain1");
            var originalState = new UInt256State
            {
                Hash = UInt256.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef"),
                Index = 100
            };
            var writeGrainState = new TestGrainState<UInt256State> { State = originalState };

            // Act - Write
            await _storage.WriteStateAsync("state", grainId, writeGrainState);

            // Act - Read
            var readGrainState = new TestGrainState<UInt256State>();
            await _storage.ReadStateAsync("state", grainId, readGrainState);

            // Assert
            Assert.IsTrue(readGrainState.RecordExists);
            Assert.AreEqual(originalState.Hash, readGrainState.State.Hash);
            Assert.AreEqual(originalState.Index, readGrainState.State.Index);
        }

        [TestMethod]
        public async Task WriteAndReadStateAsync_UInt160State_RoundTrip()
        {
            // Arrange
            var grainId = GrainId.Create("test", "grain1");
            var originalState = new UInt160State
            {
                Address = UInt160.Parse("0xabcdef0123456789abcdef0123456789abcdef01"),
                Balance = 1000000
            };
            var writeGrainState = new TestGrainState<UInt160State> { State = originalState };

            // Act - Write
            await _storage.WriteStateAsync("state", grainId, writeGrainState);

            // Act - Read
            var readGrainState = new TestGrainState<UInt160State>();
            await _storage.ReadStateAsync("state", grainId, readGrainState);

            // Assert
            Assert.IsTrue(readGrainState.RecordExists);
            Assert.AreEqual(originalState.Address, readGrainState.State.Address);
            Assert.AreEqual(originalState.Balance, readGrainState.State.Balance);
        }

        #endregion

        #region Compression Tests

        [TestMethod]
        public async Task WriteAndReadStateAsync_WithCompression_RoundTrip()
        {
            // Arrange - Create storage with compression enabled
            var options = new MessagePackGrainStorageOptions { UseCompression = true };
            var compressedStorage = new MessagePackGrainStorage("compressed", options, _loggerMock.Object);

            var grainId = GrainId.Create("test", "grain1");
            var originalState = new SimpleState { Value = 42, Name = "Compressed" };
            var writeGrainState = new TestGrainState<SimpleState> { State = originalState };

            // Act - Write
            await compressedStorage.WriteStateAsync("state", grainId, writeGrainState);

            // Act - Read
            var readGrainState = new TestGrainState<SimpleState>();
            await compressedStorage.ReadStateAsync("state", grainId, readGrainState);

            // Assert
            Assert.IsTrue(readGrainState.RecordExists);
            Assert.AreEqual(originalState.Value, readGrainState.State.Value);
            Assert.AreEqual(originalState.Name, readGrainState.State.Name);
        }

        #endregion

        #region ETag Concurrency Tests

        [TestMethod]
        public async Task WriteStateAsync_ETagMismatch_ThrowsInconsistentStateException()
        {
            // Arrange
            var grainId = GrainId.Create("test", "grain1");
            var grainState1 = new TestGrainState<SimpleState>
            {
                State = new SimpleState { Value = 1 }
            };
            var grainState2 = new TestGrainState<SimpleState>
            {
                State = new SimpleState { Value = 2 }
            };

            // Write initial state
            await _storage.WriteStateAsync("state", grainId, grainState1);

            // Simulate concurrent modification - grainState2 has wrong ETag
            grainState2.ETag = "wrong-etag";

            // Act & Assert
            await Assert.ThrowsExactlyAsync<InconsistentStateException>(async () =>
                await _storage.WriteStateAsync("state", grainId, grainState2));
        }

        [TestMethod]
        public async Task WriteStateAsync_CorrectETag_Succeeds()
        {
            // Arrange
            var grainId = GrainId.Create("test", "grain1");
            var grainState = new TestGrainState<SimpleState>
            {
                State = new SimpleState { Value = 1 }
            };

            // Write initial state
            await _storage.WriteStateAsync("state", grainId, grainState);
            var originalETag = grainState.ETag;

            // Update with correct ETag
            grainState.State = new SimpleState { Value = 2 };

            // Act
            await _storage.WriteStateAsync("state", grainId, grainState);

            // Assert
            Assert.IsNotNull(grainState.ETag);
            Assert.AreNotEqual(originalETag, grainState.ETag);
        }

        #endregion

        #region Multiple Grains Tests

        [TestMethod]
        public async Task WriteAndReadStateAsync_MultipleGrains_IsolatedState()
        {
            // Arrange
            var grainId1 = GrainId.Create("test", "grain1");
            var grainId2 = GrainId.Create("test", "grain2");

            var state1 = new TestGrainState<SimpleState>
            {
                State = new SimpleState { Value = 100, Name = "Grain1" }
            };
            var state2 = new TestGrainState<SimpleState>
            {
                State = new SimpleState { Value = 200, Name = "Grain2" }
            };

            // Act - Write both
            await _storage.WriteStateAsync("state", grainId1, state1);
            await _storage.WriteStateAsync("state", grainId2, state2);

            // Act - Read both
            var readState1 = new TestGrainState<SimpleState>();
            var readState2 = new TestGrainState<SimpleState>();
            await _storage.ReadStateAsync("state", grainId1, readState1);
            await _storage.ReadStateAsync("state", grainId2, readState2);

            // Assert - States are isolated
            Assert.AreEqual(100, readState1.State.Value);
            Assert.AreEqual("Grain1", readState1.State.Name);
            Assert.AreEqual(200, readState2.State.Value);
            Assert.AreEqual("Grain2", readState2.State.Name);
        }

        #endregion

        #region Helper Classes

        private class TestGrainState<T> : IGrainState<T>
        {
            public T State { get; set; } = default!;
            public string? ETag { get; set; }
            public bool RecordExists { get; set; }
        }

        [MessagePack.MessagePackObject]
        public class SimpleState
        {
            [MessagePack.Key(0)]
            public int Value { get; set; }

            [MessagePack.Key(1)]
            public string? Name { get; set; }
        }

        [MessagePack.MessagePackObject]
        public class UInt256State
        {
            [MessagePack.Key(0)]
            public UInt256 Hash { get; set; } = UInt256.Zero;

            [MessagePack.Key(1)]
            public uint Index { get; set; }
        }

        [MessagePack.MessagePackObject]
        public class UInt160State
        {
            [MessagePack.Key(0)]
            public UInt160 Address { get; set; } = UInt160.Zero;

            [MessagePack.Key(1)]
            public long Balance { get; set; }
        }

        #endregion
    }

    /// <summary>
    /// Tests for MessagePackGrainStorageOptions.
    /// </summary>
    [TestClass]
    public sealed class MessagePackGrainStorageOptionsTests
    {
        [TestMethod]
        public void DefaultOptions_CompressionEnabled()
        {
            // Arrange & Act
            var options = new MessagePackGrainStorageOptions();

            // Assert
            Assert.IsTrue(options.UseCompression);
        }

        [TestMethod]
        public void CustomOptions_CompressionDisabled()
        {
            // Arrange & Act
            var options = new MessagePackGrainStorageOptions { UseCompression = false };

            // Assert
            Assert.IsFalse(options.UseCompression);
        }
    }
}
