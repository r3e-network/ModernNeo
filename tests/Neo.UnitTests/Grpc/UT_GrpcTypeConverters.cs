// Copyright (C) 2015-2025 The Neo Project.
//
// UT_GrpcTypeConverters.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Grpc.Converters;
using System;
using System.Linq;
using GrpcByteString = Google.Protobuf.ByteString;
using GrpcUInt256 = Neo.Grpc.V1.UInt256;
using GrpcUInt160 = Neo.Grpc.V1.UInt160;
using GrpcWitness = Neo.Grpc.V1.Witness;
using GrpcVMState = Neo.Grpc.V1.VMState;
using GrpcStackItemType = Neo.Grpc.V1.StackItemType;
using NeoWitness = Neo.Network.P2P.Payloads.Witness;

namespace Neo.UnitTests.Grpc;

/// <summary>
/// Unit tests for gRPC type converters.
/// </summary>
[TestClass]
public class UT_GrpcTypeConverters
{
    #region UInt256 Tests

    [TestMethod]
    public void UInt256_ToGrpc_ValidHash_ReturnsCorrectData()
    {
        // Arrange
        var bytes = new byte[32];
        for (int i = 0; i < 32; i++) bytes[i] = (byte)i;
        var hash = new UInt256(bytes);

        // Act
        var grpcHash = hash.ToGrpc();

        // Assert
        Assert.AreEqual(32, grpcHash.Data.Length);
        CollectionAssert.AreEqual(hash.GetSpan().ToArray(), grpcHash.Data.ToByteArray());
    }

    [TestMethod]
    public void UInt256_ToGrpc_NullHash_ReturnsEmptyData()
    {
        // Arrange
        UInt256? hash = null;

        // Act
        var grpcHash = hash.ToGrpc();

        // Assert
        Assert.IsTrue(grpcHash.Data.IsEmpty);
    }

    [TestMethod]
    public void UInt256_ToNeo_ValidData_ReturnsCorrectHash()
    {
        // Arrange
        var bytes = new byte[32];
        for (int i = 0; i < 32; i++) bytes[i] = (byte)(i * 2);
        var grpcHash = new GrpcUInt256 { Data = GrpcByteString.CopyFrom(bytes) };

        // Act
        var hash = grpcHash.ToNeo();

        // Assert
        Assert.IsNotNull(hash);
        CollectionAssert.AreEqual(bytes, hash!.GetSpan().ToArray());
    }

    [TestMethod]
    public void UInt256_ToNeo_EmptyData_ReturnsNull()
    {
        // Arrange
        var grpcHash = new GrpcUInt256 { Data = GrpcByteString.Empty };

        // Act
        var hash = grpcHash.ToNeo();

        // Assert
        Assert.IsNull(hash);
    }

    [TestMethod]
    public void UInt256_ToNeo_NullInput_ReturnsNull()
    {
        // Arrange
        GrpcUInt256? grpcHash = null;

        // Act
        var hash = grpcHash.ToNeo();

        // Assert
        Assert.IsNull(hash);
    }

    [TestMethod]
    public void UInt256_RoundTrip_PreservesData()
    {
        // Arrange
        var bytes = new byte[32];
        new Random(42).NextBytes(bytes);
        var original = new UInt256(bytes);

        // Act
        var grpc = original.ToGrpc();
        var roundTrip = grpc.ToNeo();

        // Assert
        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(original, roundTrip);
    }

    #endregion

    #region UInt160 Tests

    [TestMethod]
    public void UInt160_ToGrpc_ValidHash_ReturnsCorrectData()
    {
        // Arrange
        var bytes = new byte[20];
        for (int i = 0; i < 20; i++) bytes[i] = (byte)i;
        var hash = new UInt160(bytes);

        // Act
        var grpcHash = hash.ToGrpc();

        // Assert
        Assert.AreEqual(20, grpcHash.Data.Length);
        CollectionAssert.AreEqual(hash.GetSpan().ToArray(), grpcHash.Data.ToByteArray());
    }

    [TestMethod]
    public void UInt160_ToGrpc_NullHash_ReturnsEmptyData()
    {
        // Arrange
        UInt160? hash = null;

        // Act
        var grpcHash = hash.ToGrpc();

        // Assert
        Assert.IsTrue(grpcHash.Data.IsEmpty);
    }

    [TestMethod]
    public void UInt160_ToNeo_ValidData_ReturnsCorrectHash()
    {
        // Arrange
        var bytes = new byte[20];
        for (int i = 0; i < 20; i++) bytes[i] = (byte)(i * 3);
        var grpcHash = new GrpcUInt160 { Data = GrpcByteString.CopyFrom(bytes) };

        // Act
        var hash = grpcHash.ToNeo();

        // Assert
        Assert.IsNotNull(hash);
        CollectionAssert.AreEqual(bytes, hash!.GetSpan().ToArray());
    }

    [TestMethod]
    public void UInt160_RoundTrip_PreservesData()
    {
        // Arrange
        var bytes = new byte[20];
        new Random(42).NextBytes(bytes);
        var original = new UInt160(bytes);

        // Act
        var grpc = original.ToGrpc();
        var roundTrip = grpc.ToNeo();

        // Assert
        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(original, roundTrip);
    }

    #endregion

    #region Witness Tests

    [TestMethod]
    public void Witness_ToGrpc_ValidWitness_ReturnsCorrectData()
    {
        // Arrange
        var invocation = new byte[] { 0x40, 0x01, 0x02, 0x03 };
        var verification = new byte[] { 0x21, 0x04, 0x05, 0x06 };
        var witness = new NeoWitness
        {
            InvocationScript = invocation,
            VerificationScript = verification
        };

        // Act
        var grpcWitness = witness.ToGrpc();

        // Assert
        CollectionAssert.AreEqual(invocation, grpcWitness.InvocationScript.ToByteArray());
        CollectionAssert.AreEqual(verification, grpcWitness.VerificationScript.ToByteArray());
    }

    [TestMethod]
    public void Witness_ToNeo_ValidWitness_ReturnsCorrectData()
    {
        // Arrange
        var invocation = new byte[] { 0x40, 0x01, 0x02, 0x03 };
        var verification = new byte[] { 0x21, 0x04, 0x05, 0x06 };
        var grpcWitness = new GrpcWitness
        {
            InvocationScript = GrpcByteString.CopyFrom(invocation),
            VerificationScript = GrpcByteString.CopyFrom(verification)
        };

        // Act
        var witness = grpcWitness.ToNeo();

        // Assert
        CollectionAssert.AreEqual(invocation, witness.InvocationScript.ToArray());
        CollectionAssert.AreEqual(verification, witness.VerificationScript.ToArray());
    }

    [TestMethod]
    public void Witness_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new NeoWitness
        {
            InvocationScript = new byte[] { 0x40, 0x01, 0x02 },
            VerificationScript = new byte[] { 0x21, 0x03, 0x04 }
        };

        // Act
        var grpc = original.ToGrpc();
        var roundTrip = grpc.ToNeo();

        // Assert
        CollectionAssert.AreEqual(original.InvocationScript.ToArray(), roundTrip.InvocationScript.ToArray());
        CollectionAssert.AreEqual(original.VerificationScript.ToArray(), roundTrip.VerificationScript.ToArray());
    }

    #endregion

    #region VMState Tests

    [TestMethod]
    public void VMState_ToGrpc_Halt_ReturnsHalt()
    {
        // Arrange
        var state = Neo.VM.VMState.HALT;

        // Act
        var grpcState = state.ToGrpc();

        // Assert
        Assert.AreEqual(GrpcVMState.Halt, grpcState);
    }

    [TestMethod]
    public void VMState_ToGrpc_Fault_ReturnsFault()
    {
        // Arrange
        var state = Neo.VM.VMState.FAULT;

        // Act
        var grpcState = state.ToGrpc();

        // Assert
        Assert.AreEqual(GrpcVMState.Fault, grpcState);
    }

    [TestMethod]
    public void VMState_ToGrpc_None_ReturnsNone()
    {
        // Arrange
        var state = Neo.VM.VMState.NONE;

        // Act
        var grpcState = state.ToGrpc();

        // Assert
        Assert.AreEqual(GrpcVMState.None, grpcState);
    }

    [TestMethod]
    public void VMState_ToGrpc_Break_ReturnsBreak()
    {
        // Arrange
        var state = Neo.VM.VMState.BREAK;

        // Act
        var grpcState = state.ToGrpc();

        // Assert
        Assert.AreEqual(GrpcVMState.Break, grpcState);
    }

    #endregion

    #region StackItem Tests

    [TestMethod]
    public void StackItem_ToGrpc_Boolean_True()
    {
        // Arrange
        var item = Neo.VM.Types.StackItem.True;

        // Act
        var grpcItem = item.ToGrpc();

        // Assert
        Assert.AreEqual(GrpcStackItemType.Boolean, grpcItem.Type);
        Assert.IsTrue(grpcItem.BooleanValue);
    }

    [TestMethod]
    public void StackItem_ToGrpc_Boolean_False()
    {
        // Arrange
        var item = Neo.VM.Types.StackItem.False;

        // Act
        var grpcItem = item.ToGrpc();

        // Assert
        Assert.AreEqual(GrpcStackItemType.Boolean, grpcItem.Type);
        Assert.IsFalse(grpcItem.BooleanValue);
    }

    [TestMethod]
    public void StackItem_ToGrpc_Integer()
    {
        // Arrange
        var item = new Neo.VM.Types.Integer(12345);

        // Act
        var grpcItem = item.ToGrpc();

        // Assert
        Assert.AreEqual(GrpcStackItemType.Integer, grpcItem.Type);
        var value = new System.Numerics.BigInteger(grpcItem.IntegerValue.ToByteArray());
        Assert.AreEqual(12345, (int)value);
    }

    [TestMethod]
    public void StackItem_ToGrpc_ByteString()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var item = new Neo.VM.Types.ByteString(data);

        // Act
        var grpcItem = item.ToGrpc();

        // Assert
        Assert.AreEqual(GrpcStackItemType.ByteString, grpcItem.Type);
        CollectionAssert.AreEqual(data, grpcItem.ByteStringValue.ToByteArray());
    }

    [TestMethod]
    public void StackItem_ToGrpc_Array()
    {
        // Arrange
        var array = new Neo.VM.Types.Array();
        array.Add(Neo.VM.Types.StackItem.True);
        array.Add(new Neo.VM.Types.Integer(42));

        // Act
        var grpcItem = array.ToGrpc();

        // Assert
        Assert.AreEqual(GrpcStackItemType.Array, grpcItem.Type);
        Assert.AreEqual(2, grpcItem.ArrayValue.Items.Count);
        Assert.AreEqual(GrpcStackItemType.Boolean, grpcItem.ArrayValue.Items[0].Type);
        Assert.AreEqual(GrpcStackItemType.Integer, grpcItem.ArrayValue.Items[1].Type);
    }

    [TestMethod]
    public void StackItem_ToGrpc_Map()
    {
        // Arrange
        var map = new Neo.VM.Types.Map();
        map[new Neo.VM.Types.ByteString(new byte[] { 0x01 })] = new Neo.VM.Types.Integer(100);

        // Act
        var grpcItem = map.ToGrpc();

        // Assert
        Assert.AreEqual(GrpcStackItemType.Map, grpcItem.Type);
        Assert.AreEqual(1, grpcItem.MapValue.Entries.Count);
    }

    #endregion
}
