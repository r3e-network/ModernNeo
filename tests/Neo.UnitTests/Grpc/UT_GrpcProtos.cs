// Copyright (C) 2015-2025 The Neo Project.
//
// UT_GrpcProtos.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

#nullable enable

using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

// Use aliases to avoid ambiguity with Neo types
using GrpcUInt256 = Neo.Grpc.V1.UInt256;
using GrpcUInt160 = Neo.Grpc.V1.UInt160;
using GrpcHeader = Neo.Grpc.V1.Header;
using GrpcBlock = Neo.Grpc.V1.Block;
using GrpcTransaction = Neo.Grpc.V1.Transaction;
using GrpcSigner = Neo.Grpc.V1.Signer;
using GrpcWitness = Neo.Grpc.V1.Witness;
using GrpcWitnessScope = Neo.Grpc.V1.WitnessScope;
using GrpcWitnessRule = Neo.Grpc.V1.WitnessRule;
using GrpcWitnessRuleAction = Neo.Grpc.V1.WitnessRuleAction;
using GrpcWitnessCondition = Neo.Grpc.V1.WitnessCondition;
using GrpcWitnessConditionType = Neo.Grpc.V1.WitnessConditionType;
using GrpcTransactionAttribute = Neo.Grpc.V1.TransactionAttribute;
using GrpcTransactionAttributeType = Neo.Grpc.V1.TransactionAttributeType;
using GrpcHighPriorityAttribute = Neo.Grpc.V1.HighPriorityAttribute;
using GrpcConflictsAttribute = Neo.Grpc.V1.ConflictsAttribute;
using GrpcContractState = Neo.Grpc.V1.ContractState;
using GrpcContractManifest = Neo.Grpc.V1.ContractManifest;
using GrpcContractAbi = Neo.Grpc.V1.ContractAbi;
using GrpcContractMethod = Neo.Grpc.V1.ContractMethod;
using GrpcContractParameterType = Neo.Grpc.V1.ContractParameterType;
using GrpcInvokeResult = Neo.Grpc.V1.InvokeResult;
using GrpcVMState = Neo.Grpc.V1.VMState;
using GrpcStackItem = Neo.Grpc.V1.StackItem;
using GrpcStackItemType = Neo.Grpc.V1.StackItemType;
using GrpcStackItemArray = Neo.Grpc.V1.StackItemArray;
using GrpcNotification = Neo.Grpc.V1.Notification;
using GrpcNep17Balance = Neo.Grpc.V1.Nep17Balance;
using GrpcNodeVersion = Neo.Grpc.V1.NodeVersion;
using GrpcProtocolSettings = Neo.Grpc.V1.ProtocolSettings;
using GrpcPeersInfo = Neo.Grpc.V1.PeersInfo;
using GrpcPeer = Neo.Grpc.V1.Peer;
using GrpcValidator = Neo.Grpc.V1.Validator;
using GrpcMemoryPoolInfo = Neo.Grpc.V1.MemoryPoolInfo;
using GrpcPaginationRequest = Neo.Grpc.V1.PaginationRequest;
using GrpcGetBlockRequest = Neo.Grpc.V1.GetBlockRequest;
using GrpcGetBlockCountRequest = Neo.Grpc.V1.GetBlockCountRequest;
using GrpcSendRawTransactionRequest = Neo.Grpc.V1.SendRawTransactionRequest;
using GrpcInvokeFunctionRequest = Neo.Grpc.V1.InvokeFunctionRequest;
using GrpcInvokeParameter = Neo.Grpc.V1.InvokeParameter;

namespace Neo.UnitTests.Grpc;

/// <summary>
/// Unit tests for gRPC Proto message serialization.
/// </summary>
[TestClass]
public class UT_GrpcProtos
{
    #region Common Types Tests

    [TestMethod]
    public void UInt256_Serialization_RoundTrip()
    {
        // Arrange
        var original = new GrpcUInt256
        {
            Data = ByteString.CopyFrom(new byte[32].Select((_, i) => (byte)i).ToArray())
        };

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcUInt256.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(32, deserialized.Data.Length);
        CollectionAssert.AreEqual(original.Data.ToByteArray(), deserialized.Data.ToByteArray());
    }

    [TestMethod]
    public void UInt160_Serialization_RoundTrip()
    {
        // Arrange
        var original = new GrpcUInt160
        {
            Data = ByteString.CopyFrom(new byte[20].Select((_, i) => (byte)(i * 2)).ToArray())
        };

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcUInt160.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(20, deserialized.Data.Length);
        CollectionAssert.AreEqual(original.Data.ToByteArray(), deserialized.Data.ToByteArray());
    }

    [TestMethod]
    public void PaginationRequest_Serialization()
    {
        // Arrange
        var original = new GrpcPaginationRequest
        {
            Page = 5,
            PageSize = 100
        };

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcPaginationRequest.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(5, deserialized.Page);
        Assert.AreEqual(100, deserialized.PageSize);
    }

    #endregion

    #region Blockchain Types Tests

    [TestMethod]
    public void Header_Serialization_RoundTrip()
    {
        // Arrange
        var original = new GrpcHeader
        {
            Version = 0,
            PrevHash = new GrpcUInt256 { Data = ByteString.CopyFrom(new byte[32]) },
            MerkleRoot = new GrpcUInt256 { Data = ByteString.CopyFrom(new byte[32]) },
            Timestamp = 1703001600000,
            Nonce = 12345678,
            Index = 1000,
            PrimaryIndex = 0,
            NextConsensus = new GrpcUInt160 { Data = ByteString.CopyFrom(new byte[20]) },
            Hash = new GrpcUInt256 { Data = ByteString.CopyFrom(new byte[32]) },
            Size = 112
        };

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcHeader.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(0u, deserialized.Version);
        Assert.AreEqual(1703001600000ul, deserialized.Timestamp);
        Assert.AreEqual(12345678ul, deserialized.Nonce);
        Assert.AreEqual(1000u, deserialized.Index);
        Assert.AreEqual(112, deserialized.Size);
    }

    [TestMethod]
    public void Transaction_Serialization_RoundTrip()
    {
        // Arrange
        var original = new GrpcTransaction
        {
            Version = 0,
            Nonce = 123456,
            SystemFee = 1000000,
            NetworkFee = 500000,
            ValidUntilBlock = 2000,
            Script = ByteString.CopyFrom(new byte[] { 0x01, 0x02, 0x03 }),
            Hash = new GrpcUInt256 { Data = ByteString.CopyFrom(new byte[32]) },
            Size = 250,
            Sender = new GrpcUInt160 { Data = ByteString.CopyFrom(new byte[20]) },
            FeePerByte = 2000
        };

        original.Signers.Add(new GrpcSigner
        {
            Account = new GrpcUInt160 { Data = ByteString.CopyFrom(new byte[20]) },
            Scopes = GrpcWitnessScope.CalledByEntry
        });

        original.Witnesses.Add(new GrpcWitness
        {
            InvocationScript = ByteString.CopyFrom(new byte[] { 0x40 }),
            VerificationScript = ByteString.CopyFrom(new byte[] { 0x21 })
        });

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcTransaction.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(0u, deserialized.Version);
        Assert.AreEqual(123456u, deserialized.Nonce);
        Assert.AreEqual(1000000L, deserialized.SystemFee);
        Assert.AreEqual(500000L, deserialized.NetworkFee);
        Assert.AreEqual(2000u, deserialized.ValidUntilBlock);
        Assert.AreEqual(1, deserialized.Signers.Count);
        Assert.AreEqual(1, deserialized.Witnesses.Count);
        Assert.AreEqual(GrpcWitnessScope.CalledByEntry, deserialized.Signers[0].Scopes);
    }

    [TestMethod]
    public void Block_Serialization_RoundTrip()
    {
        // Arrange
        var header = new GrpcHeader
        {
            Version = 0,
            Index = 100,
            Timestamp = 1703001600000,
            PrevHash = new GrpcUInt256 { Data = ByteString.CopyFrom(new byte[32]) },
            MerkleRoot = new GrpcUInt256 { Data = ByteString.CopyFrom(new byte[32]) },
            NextConsensus = new GrpcUInt160 { Data = ByteString.CopyFrom(new byte[20]) }
        };

        var tx = new GrpcTransaction
        {
            Version = 0,
            Nonce = 1,
            SystemFee = 100000,
            NetworkFee = 50000,
            ValidUntilBlock = 200,
            Script = ByteString.CopyFrom(new byte[] { 0x01 }),
            Hash = new GrpcUInt256 { Data = ByteString.CopyFrom(new byte[32]) }
        };

        var original = new GrpcBlock
        {
            Header = header,
            Hash = new GrpcUInt256 { Data = ByteString.CopyFrom(new byte[32]) },
            Size = 500,
            Confirmations = 10
        };
        original.Transactions.Add(tx);

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcBlock.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(100u, deserialized.Header.Index);
        Assert.AreEqual(1, deserialized.Transactions.Count);
        Assert.AreEqual(10u, deserialized.Confirmations);
    }

    [TestMethod]
    public void Signer_WithWitnessRules_Serialization()
    {
        // Arrange
        var original = new GrpcSigner
        {
            Account = new GrpcUInt160 { Data = ByteString.CopyFrom(new byte[20]) },
            Scopes = GrpcWitnessScope.WitnessRules
        };

        original.Rules.Add(new GrpcWitnessRule
        {
            Action = GrpcWitnessRuleAction.Allow,
            Condition = new GrpcWitnessCondition
            {
                Type = GrpcWitnessConditionType.Boolean,
                BooleanValue = true
            }
        });

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcSigner.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(GrpcWitnessScope.WitnessRules, deserialized.Scopes);
        Assert.AreEqual(1, deserialized.Rules.Count);
        Assert.AreEqual(GrpcWitnessRuleAction.Allow, deserialized.Rules[0].Action);
        Assert.IsTrue(deserialized.Rules[0].Condition.BooleanValue);
    }

    [TestMethod]
    public void TransactionAttribute_HighPriority_Serialization()
    {
        // Arrange
        var original = new GrpcTransactionAttribute
        {
            Type = GrpcTransactionAttributeType.HighPriority,
            HighPriority = new GrpcHighPriorityAttribute()
        };

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcTransactionAttribute.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(GrpcTransactionAttributeType.HighPriority, deserialized.Type);
        Assert.IsNotNull(deserialized.HighPriority);
    }

    [TestMethod]
    public void TransactionAttribute_Conflicts_Serialization()
    {
        // Arrange
        var conflictHash = new GrpcUInt256 { Data = ByteString.CopyFrom(new byte[32].Select((_, i) => (byte)i).ToArray()) };
        var original = new GrpcTransactionAttribute
        {
            Type = GrpcTransactionAttributeType.Conflicts,
            Conflicts = new GrpcConflictsAttribute { Hash = conflictHash }
        };

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcTransactionAttribute.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(GrpcTransactionAttributeType.Conflicts, deserialized.Type);
        CollectionAssert.AreEqual(conflictHash.Data.ToByteArray(), deserialized.Conflicts.Hash.Data.ToByteArray());
    }

    #endregion

    #region Node Types Tests

    [TestMethod]
    public void NodeVersion_Serialization_RoundTrip()
    {
        // Arrange
        var original = new GrpcNodeVersion
        {
            TcpPort = 10333,
            WsPort = 10334,
            Nonce = 123456789,
            UserAgent = "/Neo:3.7.0/",
            Protocol = new GrpcProtocolSettings
            {
                Network = 860833102,
                ValidatorsCount = 7,
                MsPerBlock = 15000,
                MaxTraceableBlocks = 2102400,
                MaxTransactionsPerBlock = 512,
                MemoryPoolMaxTransactions = 50000
            }
        };

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcNodeVersion.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(10333, deserialized.TcpPort);
        Assert.AreEqual(10334, deserialized.WsPort);
        Assert.AreEqual("/Neo:3.7.0/", deserialized.UserAgent);
        Assert.AreEqual(860833102u, deserialized.Protocol.Network);
        Assert.AreEqual(7, deserialized.Protocol.ValidatorsCount);
    }

    [TestMethod]
    public void PeersInfo_Serialization_RoundTrip()
    {
        // Arrange
        var original = new GrpcPeersInfo();
        original.Connected.Add(new GrpcPeer { Address = "192.168.1.1", Port = 10333 });
        original.Connected.Add(new GrpcPeer { Address = "192.168.1.2", Port = 10333 });
        original.Unconnected.Add(new GrpcPeer { Address = "192.168.1.3", Port = 10333 });

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcPeersInfo.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(2, deserialized.Connected.Count);
        Assert.AreEqual(1, deserialized.Unconnected.Count);
        Assert.AreEqual("192.168.1.1", deserialized.Connected[0].Address);
    }

    [TestMethod]
    public void Validator_Serialization_RoundTrip()
    {
        // Arrange
        var original = new GrpcValidator
        {
            PublicKey = ByteString.CopyFrom(new byte[33]),
            Votes = 1000000,
            Active = true
        };

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcValidator.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(33, deserialized.PublicKey.Length);
        Assert.AreEqual(1000000L, deserialized.Votes);
        Assert.IsTrue(deserialized.Active);
    }

    [TestMethod]
    public void MemoryPoolInfo_Serialization()
    {
        // Arrange
        var original = new GrpcMemoryPoolInfo
        {
            VerifiedCount = 100,
            UnverifiedCount = 50
        };

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcMemoryPoolInfo.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(100, deserialized.VerifiedCount);
        Assert.AreEqual(50, deserialized.UnverifiedCount);
    }

    #endregion

    #region Contract Types Tests

    [TestMethod]
    public void ContractState_Serialization_RoundTrip()
    {
        // Arrange
        var original = new GrpcContractState
        {
            Id = 1,
            UpdateCounter = 0,
            Hash = new GrpcUInt160 { Data = ByteString.CopyFrom(new byte[20]) },
            Nef = ByteString.CopyFrom(new byte[] { 0x4E, 0x45, 0x46 }),
            Manifest = new GrpcContractManifest
            {
                Name = "TestContract"
            }
        };

        original.Manifest.SupportedStandards.Add("NEP-17");
        original.Manifest.Abi = new GrpcContractAbi();
        original.Manifest.Abi.Methods.Add(new GrpcContractMethod
        {
            Name = "transfer",
            ReturnType = GrpcContractParameterType.Boolean,
            Safe = false
        });

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcContractState.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(1, deserialized.Id);
        Assert.AreEqual("TestContract", deserialized.Manifest.Name);
        Assert.AreEqual(1, deserialized.Manifest.SupportedStandards.Count);
        Assert.AreEqual("NEP-17", deserialized.Manifest.SupportedStandards[0]);
        Assert.AreEqual(1, deserialized.Manifest.Abi.Methods.Count);
        Assert.AreEqual("transfer", deserialized.Manifest.Abi.Methods[0].Name);
    }

    [TestMethod]
    public void InvokeResult_Serialization_RoundTrip()
    {
        // Arrange
        var original = new GrpcInvokeResult
        {
            Script = ByteString.CopyFrom(new byte[] { 0x01, 0x02 }),
            State = GrpcVMState.Halt,
            GasConsumed = 1000000
        };

        original.Stack.Add(new GrpcStackItem
        {
            Type = GrpcStackItemType.Integer,
            IntegerValue = ByteString.CopyFrom(new byte[] { 0x64 }) // 100
        });

        original.Notifications.Add(new GrpcNotification
        {
            Contract = new GrpcUInt160 { Data = ByteString.CopyFrom(new byte[20]) },
            EventName = "Transfer"
        });

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcInvokeResult.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(GrpcVMState.Halt, deserialized.State);
        Assert.AreEqual(1000000L, deserialized.GasConsumed);
        Assert.AreEqual(1, deserialized.Stack.Count);
        Assert.AreEqual(GrpcStackItemType.Integer, deserialized.Stack[0].Type);
        Assert.AreEqual(1, deserialized.Notifications.Count);
        Assert.AreEqual("Transfer", deserialized.Notifications[0].EventName);
    }

    [TestMethod]
    public void Nep17Balance_Serialization()
    {
        // Arrange
        var original = new GrpcNep17Balance
        {
            AssetHash = new GrpcUInt160 { Data = ByteString.CopyFrom(new byte[20]) },
            Amount = "1000000000",
            LastUpdatedBlock = 12345
        };

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcNep17Balance.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual("1000000000", deserialized.Amount);
        Assert.AreEqual(12345u, deserialized.LastUpdatedBlock);
    }

    #endregion

    #region Service Request/Response Tests

    [TestMethod]
    public void GetBlockRequest_ByHash_Serialization()
    {
        // Arrange
        var hash = new GrpcUInt256 { Data = ByteString.CopyFrom(new byte[32].Select((_, i) => (byte)i).ToArray()) };
        var original = new GrpcGetBlockRequest
        {
            Hash = hash,
            Verbose = true
        };

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcGetBlockRequest.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(GrpcGetBlockRequest.IdentifierOneofCase.Hash, deserialized.IdentifierCase);
        Assert.IsTrue(deserialized.Verbose);
        CollectionAssert.AreEqual(hash.Data.ToByteArray(), deserialized.Hash.Data.ToByteArray());
    }

    [TestMethod]
    public void GetBlockRequest_ByIndex_Serialization()
    {
        // Arrange
        var original = new GrpcGetBlockRequest
        {
            Index = 12345,
            Verbose = false
        };

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcGetBlockRequest.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(GrpcGetBlockRequest.IdentifierOneofCase.Index, deserialized.IdentifierCase);
        Assert.AreEqual(12345u, deserialized.Index);
        Assert.IsFalse(deserialized.Verbose);
    }

    [TestMethod]
    public void SendRawTransactionRequest_Serialization()
    {
        // Arrange
        var txBytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
        var original = new GrpcSendRawTransactionRequest
        {
            Transaction = ByteString.CopyFrom(txBytes)
        };

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcSendRawTransactionRequest.Parser.ParseFrom(bytes);

        // Assert
        CollectionAssert.AreEqual(txBytes, deserialized.Transaction.ToByteArray());
    }

    [TestMethod]
    public void InvokeFunctionRequest_Serialization()
    {
        // Arrange
        var original = new GrpcInvokeFunctionRequest
        {
            ScriptHash = new GrpcUInt160 { Data = ByteString.CopyFrom(new byte[20]) },
            Operation = "transfer",
            UseDiagnostic = true
        };

        original.Params.Add(new GrpcInvokeParameter
        {
            Type = GrpcContractParameterType.Hash160,
            Hash160Value = new GrpcUInt160 { Data = ByteString.CopyFrom(new byte[20]) }
        });

        original.Params.Add(new GrpcInvokeParameter
        {
            Type = GrpcContractParameterType.Integer,
            IntegerValue = ByteString.CopyFrom(new byte[] { 0x64 })
        });

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcInvokeFunctionRequest.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual("transfer", deserialized.Operation);
        Assert.IsTrue(deserialized.UseDiagnostic);
        Assert.AreEqual(2, deserialized.Params.Count);
        Assert.AreEqual(GrpcContractParameterType.Hash160, deserialized.Params[0].Type);
        Assert.AreEqual(GrpcContractParameterType.Integer, deserialized.Params[1].Type);
    }

    #endregion

    #region Edge Cases Tests

    [TestMethod]
    public void EmptyMessage_Serialization()
    {
        // Arrange
        var original = new GrpcGetBlockCountRequest();

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcGetBlockCountRequest.Parser.ParseFrom(bytes);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(0, bytes.Length);
    }

    [TestMethod]
    public void LargeTransaction_Serialization()
    {
        // Arrange - Create a transaction with many signers and attributes
        var original = new GrpcTransaction
        {
            Version = 0,
            Nonce = 999999,
            SystemFee = 10000000,
            NetworkFee = 5000000,
            ValidUntilBlock = 100000,
            Script = ByteString.CopyFrom(new byte[1000]),
            Hash = new GrpcUInt256 { Data = ByteString.CopyFrom(new byte[32]) }
        };

        for (int i = 0; i < 16; i++)
        {
            original.Signers.Add(new GrpcSigner
            {
                Account = new GrpcUInt160 { Data = ByteString.CopyFrom(new byte[20].Select((_, j) => (byte)(i + j)).ToArray()) },
                Scopes = GrpcWitnessScope.CalledByEntry
            });
        }

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcTransaction.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(16, deserialized.Signers.Count);
        Assert.AreEqual(1000, deserialized.Script.Length);
    }

    [TestMethod]
    public void StackItem_NestedArray_Serialization()
    {
        // Arrange
        var innerArray = new GrpcStackItemArray();
        innerArray.Items.Add(new GrpcStackItem { Type = GrpcStackItemType.Integer, IntegerValue = ByteString.CopyFrom(new byte[] { 1 }) });
        innerArray.Items.Add(new GrpcStackItem { Type = GrpcStackItemType.Integer, IntegerValue = ByteString.CopyFrom(new byte[] { 2 }) });

        var original = new GrpcStackItem
        {
            Type = GrpcStackItemType.Array,
            ArrayValue = innerArray
        };

        // Act
        var bytes = original.ToByteArray();
        var deserialized = GrpcStackItem.Parser.ParseFrom(bytes);

        // Assert
        Assert.AreEqual(GrpcStackItemType.Array, deserialized.Type);
        Assert.AreEqual(2, deserialized.ArrayValue.Items.Count);
    }

    #endregion
}
