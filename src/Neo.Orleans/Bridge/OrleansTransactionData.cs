using Neo;
using Neo.Core.Interfaces;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Neo.Orleans.Bridge;

[GenerateSerializer]
[Alias("Neo.Orleans.Bridge.OrleansTransactionData")]
internal sealed class OrleansTransactionData : ITransactionData
{
    [Id(0)] public UInt256 Hash { get; private set; } = UInt256.Zero;
    [Id(1)] public byte Version { get; private set; }
    [Id(2)] public uint Nonce { get; private set; }
    [Id(3)] public long SystemFee { get; private set; }
    [Id(4)] public long NetworkFee { get; private set; }
    [Id(5)] public uint ValidUntilBlock { get; private set; }
    [Id(6)] private byte[] _script = Array.Empty<byte>();
    [Id(7)] public UInt160 Sender { get; private set; } = UInt160.Zero;
    [Id(8)] public long FeePerByte { get; private set; }
    [Id(9)] public int SignersCount { get; private set; }
    [Id(10)] public int AttributesCount { get; private set; }
    [Id(11)] private byte[] _transactionBytes = Array.Empty<byte>();

    public ReadOnlyMemory<byte> Script => _script;

    public int Size => _transactionBytes.Length;

    public OrleansTransactionData()
    {
    }

    private OrleansTransactionData(ITransactionData transaction)
    {
        Hash = transaction.Hash;
        Version = transaction.Version;
        Nonce = transaction.Nonce;
        SystemFee = transaction.SystemFee;
        NetworkFee = transaction.NetworkFee;
        ValidUntilBlock = transaction.ValidUntilBlock;
        _script = transaction.Script.ToArray();
        Sender = transaction.Sender;
        FeePerByte = transaction.FeePerByte;
        SignersCount = transaction.SignersCount;
        AttributesCount = transaction.AttributesCount;
        _transactionBytes = transaction.ToArray();
    }

    public static OrleansTransactionData From(ITransactionData transaction) =>
        transaction as OrleansTransactionData ?? new OrleansTransactionData(transaction);

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(_transactionBytes);
    }

    public void Deserialize(ref MemoryReader reader)
    {
        var tx = reader.ReadSerializable<Transaction>();
        PopulateFromTransaction(tx, tx.ToArray());
    }

    public void SerializeUnsigned(BinaryWriter writer)
    {
        if (_transactionBytes.Length == 0)
            throw new NotSupportedException("Unsigned serialization requires full transaction data.");

        var reader = new MemoryReader(_transactionBytes);
        var tx = reader.ReadSerializable<Transaction>();
        ((IVerifiableBase)tx).SerializeUnsigned(writer);
    }

    public void DeserializeUnsigned(ref MemoryReader reader)
    {
        var tx = (Transaction)RuntimeHelpers.GetUninitializedObject(typeof(Transaction));
        tx.DeserializeUnsigned(ref reader);

        Hash = tx.Hash;
        Version = tx.Version;
        Nonce = tx.Nonce;
        SystemFee = tx.SystemFee;
        NetworkFee = tx.NetworkFee;
        ValidUntilBlock = tx.ValidUntilBlock;
        _script = tx.Script.ToArray();
        Sender = tx.Sender;
        FeePerByte = 0;
        SignersCount = tx.Signers.Length;
        AttributesCount = tx.Attributes.Length;
        _transactionBytes = Array.Empty<byte>();
    }

    private void PopulateFromTransaction(Transaction tx, byte[] transactionBytes)
    {
        Hash = tx.Hash;
        Version = tx.Version;
        Nonce = tx.Nonce;
        SystemFee = tx.SystemFee;
        NetworkFee = tx.NetworkFee;
        ValidUntilBlock = tx.ValidUntilBlock;
        _script = tx.Script.ToArray();
        Sender = tx.Sender;
        FeePerByte = tx.FeePerByte;
        SignersCount = tx.Signers.Length;
        AttributesCount = tx.Attributes.Length;
        _transactionBytes = transactionBytes;
    }
}
