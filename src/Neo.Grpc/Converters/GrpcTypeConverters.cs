// Copyright (C) 2015-2025 The Neo Project.
//
// GrpcTypeConverters.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

using System;
using System.Linq;
using GrpcByteString = Google.Protobuf.ByteString;

using NeoBlock = Neo.Network.P2P.Payloads.Block;
using NeoHeader = Neo.Network.P2P.Payloads.Header;
using NeoTransaction = Neo.Network.P2P.Payloads.Transaction;
using NeoWitness = Neo.Network.P2P.Payloads.Witness;
using NeoSigner = Neo.Network.P2P.Payloads.Signer;
using NeoWitnessRule = Neo.Network.P2P.Payloads.WitnessRule;
using NeoWitnessCondition = Neo.Network.P2P.Payloads.Conditions.WitnessCondition;
using NeoTransactionAttribute = Neo.Network.P2P.Payloads.TransactionAttribute;
using NeoUInt256 = Neo.UInt256;
using NeoUInt160 = Neo.UInt160;

namespace Neo.Grpc.Converters;

/// <summary>
/// Provides conversion methods between Neo domain types and gRPC Proto types.
/// </summary>
public static class GrpcTypeConverters
{
    #region Hash Types

    /// <summary>
    /// Converts a Neo UInt256 to gRPC UInt256.
    /// </summary>
    public static V1.UInt256 ToGrpc(this NeoUInt256? hash)
    {
        if (hash is null)
            return new V1.UInt256 { Data = GrpcByteString.Empty };

        return new V1.UInt256 { Data = GrpcByteString.CopyFrom(hash.GetSpan()) };
    }

    /// <summary>
    /// Converts a gRPC UInt256 to Neo UInt256.
    /// </summary>
    public static NeoUInt256? ToNeo(this V1.UInt256? hash)
    {
        if (hash is null || hash.Data.IsEmpty)
            return null;

        return new NeoUInt256(hash.Data.Span);
    }

    /// <summary>
    /// Converts a Neo UInt160 to gRPC UInt160.
    /// </summary>
    public static V1.UInt160 ToGrpc(this NeoUInt160? hash)
    {
        if (hash is null)
            return new V1.UInt160 { Data = GrpcByteString.Empty };

        return new V1.UInt160 { Data = GrpcByteString.CopyFrom(hash.GetSpan()) };
    }

    /// <summary>
    /// Converts a gRPC UInt160 to Neo UInt160.
    /// </summary>
    public static NeoUInt160? ToNeo(this V1.UInt160? hash)
    {
        if (hash is null || hash.Data.IsEmpty)
            return null;

        return new NeoUInt160(hash.Data.Span);
    }

    #endregion

    #region Witness

    /// <summary>
    /// Converts a Neo Witness to gRPC Witness.
    /// </summary>
    public static V1.Witness ToGrpc(this NeoWitness witness)
    {
        return new V1.Witness
        {
            InvocationScript = GrpcByteString.CopyFrom(witness.InvocationScript.Span),
            VerificationScript = GrpcByteString.CopyFrom(witness.VerificationScript.Span)
        };
    }

    /// <summary>
    /// Converts a gRPC Witness to Neo Witness.
    /// </summary>
    public static NeoWitness ToNeo(this V1.Witness witness)
    {
        return new NeoWitness
        {
            InvocationScript = witness.InvocationScript.ToByteArray(),
            VerificationScript = witness.VerificationScript.ToByteArray()
        };
    }

    #endregion

    #region Signer

    /// <summary>
    /// Converts a Neo Signer to gRPC Signer.
    /// </summary>
    public static V1.Signer ToGrpc(this NeoSigner signer)
    {
        var grpcSigner = new V1.Signer
        {
            Account = signer.Account.ToGrpc(),
            Scopes = (V1.WitnessScope)(int)signer.Scopes
        };

        if (signer.AllowedContracts != null)
        {
            foreach (var contract in signer.AllowedContracts)
            {
                grpcSigner.AllowedContracts.Add(contract.ToGrpc());
            }
        }

        if (signer.AllowedGroups != null)
        {
            foreach (var group in signer.AllowedGroups)
            {
                grpcSigner.AllowedGroups.Add(GrpcByteString.CopyFrom(group.EncodePoint(true)));
            }
        }

        if (signer.Rules != null)
        {
            foreach (var rule in signer.Rules)
            {
                grpcSigner.Rules.Add(rule.ToGrpc());
            }
        }

        return grpcSigner;
    }

    #endregion

    #region WitnessRule

    /// <summary>
    /// Converts a Neo WitnessRule to gRPC WitnessRule.
    /// </summary>
    public static V1.WitnessRule ToGrpc(this NeoWitnessRule rule)
    {
        return new V1.WitnessRule
        {
            Action = (V1.WitnessRuleAction)(int)rule.Action,
            Condition = rule.Condition.ToGrpc()
        };
    }

    /// <summary>
    /// Converts a Neo WitnessCondition to gRPC WitnessCondition.
    /// </summary>
    public static V1.WitnessCondition ToGrpc(this NeoWitnessCondition condition)
    {
        var grpcCondition = new V1.WitnessCondition
        {
            Type = (V1.WitnessConditionType)(int)condition.Type
        };

        switch (condition)
        {
            case Neo.Network.P2P.Payloads.Conditions.BooleanCondition boolCond:
                grpcCondition.BooleanValue = boolCond.Expression;
                break;
            case Neo.Network.P2P.Payloads.Conditions.NotCondition notCond:
                grpcCondition.NotCondition = new V1.WitnessConditionNot
                {
                    Expression = notCond.Expression.ToGrpc()
                };
                break;
            case Neo.Network.P2P.Payloads.Conditions.AndCondition andCond:
                var andGrpc = new V1.WitnessConditionAnd();
                foreach (var expr in andCond.Expressions)
                {
                    andGrpc.Expressions.Add(expr.ToGrpc());
                }
                grpcCondition.AndCondition = andGrpc;
                break;
            case Neo.Network.P2P.Payloads.Conditions.OrCondition orCond:
                var orGrpc = new V1.WitnessConditionOr();
                foreach (var expr in orCond.Expressions)
                {
                    orGrpc.Expressions.Add(expr.ToGrpc());
                }
                grpcCondition.OrCondition = orGrpc;
                break;
            case Neo.Network.P2P.Payloads.Conditions.ScriptHashCondition scriptCond:
                grpcCondition.ScriptHash = scriptCond.Hash.ToGrpc();
                break;
            case Neo.Network.P2P.Payloads.Conditions.GroupCondition groupCond:
                grpcCondition.Group = GrpcByteString.CopyFrom(groupCond.Group.EncodePoint(true));
                break;
            case Neo.Network.P2P.Payloads.Conditions.CalledByContractCondition calledByCond:
                grpcCondition.CallerByEntry = calledByCond.Hash.ToGrpc();
                break;
            case Neo.Network.P2P.Payloads.Conditions.CalledByGroupCondition calledByGroupCond:
                grpcCondition.CallerByGroup = new V1.UInt160 { Data = GrpcByteString.CopyFrom(calledByGroupCond.Group.EncodePoint(true)) };
                break;
            case Neo.Network.P2P.Payloads.Conditions.CalledByEntryCondition:
                // CalledByEntry has no additional data
                break;
        }

        return grpcCondition;
    }

    #endregion

    #region TransactionAttribute

    /// <summary>
    /// Converts a Neo TransactionAttribute to gRPC TransactionAttribute.
    /// </summary>
    public static V1.TransactionAttribute ToGrpc(this NeoTransactionAttribute attribute)
    {
        var grpcAttr = new V1.TransactionAttribute
        {
            Type = (V1.TransactionAttributeType)(int)attribute.Type
        };

        switch (attribute)
        {
            case Neo.Network.P2P.Payloads.HighPriorityAttribute:
                grpcAttr.HighPriority = new V1.HighPriorityAttribute();
                break;
            case Neo.Network.P2P.Payloads.OracleResponse oracleResp:
                grpcAttr.OracleResponse = new V1.OracleResponseAttribute
                {
                    Id = oracleResp.Id,
                    Code = (V1.OracleResponseCode)(int)oracleResp.Code,
                    Result = GrpcByteString.CopyFrom(oracleResp.Result.Span)
                };
                break;
            case Neo.Network.P2P.Payloads.NotValidBefore notValidBefore:
                grpcAttr.NotValidBefore = new V1.NotValidBeforeAttribute
                {
                    Height = notValidBefore.Height
                };
                break;
            case Neo.Network.P2P.Payloads.Conflicts conflicts:
                grpcAttr.Conflicts = new V1.ConflictsAttribute
                {
                    Hash = conflicts.Hash.ToGrpc()
                };
                break;
            case Neo.Network.P2P.Payloads.NotaryAssisted notaryAssisted:
                grpcAttr.NotaryAssisted = new V1.NotaryAssistedAttribute
                {
                    Nkeys = notaryAssisted.NKeys
                };
                break;
        }

        return grpcAttr;
    }

    #endregion

    #region Transaction

    /// <summary>
    /// Converts a Neo Transaction to gRPC Transaction.
    /// </summary>
    public static V1.Transaction ToGrpc(this NeoTransaction tx, uint? blockIndex = null)
    {
        var grpcTx = new V1.Transaction
        {
            Version = tx.Version,
            Nonce = tx.Nonce,
            SystemFee = tx.SystemFee,
            NetworkFee = tx.NetworkFee,
            ValidUntilBlock = tx.ValidUntilBlock,
            Script = GrpcByteString.CopyFrom(tx.Script.Span),
            Hash = tx.Hash.ToGrpc(),
            Size = tx.Size,
            Sender = tx.Sender.ToGrpc(),
            FeePerByte = tx.FeePerByte
        };

        foreach (var signer in tx.Signers)
        {
            grpcTx.Signers.Add(signer.ToGrpc());
        }

        foreach (var attr in tx.Attributes)
        {
            grpcTx.Attributes.Add(attr.ToGrpc());
        }

        foreach (var witness in tx.Witnesses)
        {
            grpcTx.Witnesses.Add(witness.ToGrpc());
        }

        return grpcTx;
    }

    #endregion

    #region Header

    /// <summary>
    /// Converts a Neo Header to gRPC Header.
    /// </summary>
    public static V1.Header ToGrpc(this NeoHeader header)
    {
        return new V1.Header
        {
            Version = header.Version,
            PrevHash = header.PrevHash.ToGrpc(),
            MerkleRoot = header.MerkleRoot.ToGrpc(),
            Timestamp = header.Timestamp,
            Nonce = header.Nonce,
            Index = header.Index,
            PrimaryIndex = header.PrimaryIndex,
            NextConsensus = header.NextConsensus.ToGrpc(),
            Witness = header.Witness.ToGrpc(),
            Hash = header.Hash.ToGrpc(),
            Size = header.Size
        };
    }

    #endregion

    #region Block

    /// <summary>
    /// Converts a Neo Block to gRPC Block.
    /// </summary>
    public static V1.Block ToGrpc(this NeoBlock block, uint? confirmations = null, NeoUInt256? nextBlockHash = null)
    {
        var grpcBlock = new V1.Block
        {
            Header = block.Header.ToGrpc(),
            Hash = block.Hash.ToGrpc(),
            Size = block.Size,
            Confirmations = confirmations ?? 0
        };

        if (nextBlockHash != null)
        {
            grpcBlock.NextBlockHash = nextBlockHash.ToGrpc();
        }

        foreach (var tx in block.Transactions)
        {
            grpcBlock.Transactions.Add(tx.ToGrpc(block.Index));
        }

        return grpcBlock;
    }

    #endregion

    #region StackItem

    /// <summary>
    /// Converts a Neo VM StackItem to gRPC StackItem.
    /// </summary>
    public static V1.StackItem ToGrpc(this Neo.VM.Types.StackItem item)
    {
        var grpcItem = new V1.StackItem
        {
            Type = (V1.StackItemType)(int)item.Type
        };

        switch (item)
        {
            case Neo.VM.Types.Boolean boolItem:
                grpcItem.BooleanValue = boolItem.GetBoolean();
                break;
            case Neo.VM.Types.Integer intItem:
                grpcItem.IntegerValue = GrpcByteString.CopyFrom(intItem.GetInteger().ToByteArray());
                break;
            case Neo.VM.Types.ByteString byteStringItem:
                grpcItem.ByteStringValue = GrpcByteString.CopyFrom(byteStringItem.GetSpan());
                break;
            case Neo.VM.Types.Buffer bufferItem:
                grpcItem.BufferValue = GrpcByteString.CopyFrom(bufferItem.GetSpan());
                break;
            case Neo.VM.Types.Struct structItem:
                var grpcStruct = new V1.StackItemStruct();
                foreach (var subItem in structItem)
                {
                    grpcStruct.Items.Add(subItem.ToGrpc());
                }
                grpcItem.StructValue = grpcStruct;
                break;
            case Neo.VM.Types.Array arrayItem:
                var grpcArray = new V1.StackItemArray();
                foreach (var subItem in arrayItem)
                {
                    grpcArray.Items.Add(subItem.ToGrpc());
                }
                grpcItem.ArrayValue = grpcArray;
                break;
            case Neo.VM.Types.Map mapItem:
                var grpcMap = new V1.StackItemMap();
                foreach (var kvp in mapItem)
                {
                    grpcMap.Entries.Add(new V1.StackItemMapEntry
                    {
                        Key = kvp.Key.ToGrpc(),
                        Value = kvp.Value.ToGrpc()
                    });
                }
                grpcItem.MapValue = grpcMap;
                break;
            case Neo.VM.Types.Pointer pointerItem:
                grpcItem.PointerValue = GrpcByteString.CopyFrom(BitConverter.GetBytes(pointerItem.Position));
                break;
            case Neo.VM.Types.InteropInterface:
                grpcItem.InteropInterfaceValue = GrpcByteString.Empty;
                break;
        }

        return grpcItem;
    }

    #endregion

    #region VMState

    /// <summary>
    /// Converts a Neo VMState to gRPC VMState.
    /// </summary>
    public static V1.VMState ToGrpc(this Neo.VM.VMState state)
    {
        return state switch
        {
            Neo.VM.VMState.NONE => V1.VMState.None,
            Neo.VM.VMState.HALT => V1.VMState.Halt,
            Neo.VM.VMState.FAULT => V1.VMState.Fault,
            Neo.VM.VMState.BREAK => V1.VMState.Break,
            _ => V1.VMState.None
        };
    }

    #endregion
}
