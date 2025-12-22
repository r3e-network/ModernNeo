// Copyright (C) 2015-2025 The Neo Project.
//
// Program.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Cryptography;
using Neo.Extensions;
using Neo.IO;
using Neo.Json;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using System.Buffers.Binary;

Console.WriteLine("Neo NativeAOT Compatibility Test");
Console.WriteLine("================================");

var testsPassed = 0;
var testsFailed = 0;

void RunTest(string name, Action test)
{
    Console.WriteLine($"\n[Test] {name}...");
    try
    {
        test();
        Console.WriteLine($"  [PASS] {name}");
        testsPassed++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [FAIL] {name}: {ex.Message}");
        testsFailed++;
    }
}

// ============================================
// Test Group 1: Core Types
// ============================================
Console.WriteLine("\n--- Core Types ---");

RunTest("UInt160 Parse and ToString", () =>
{
    var hash = UInt160.Parse("0x0000000000000000000000000000000000000001");
    if (hash.ToString() != "0x0000000000000000000000000000000000000001")
        throw new Exception("UInt160 roundtrip failed");
});

RunTest("UInt256 Parse and ToString", () =>
{
    var hash = UInt256.Parse("0x0000000000000000000000000000000000000000000000000000000000000001");
    if (hash.ToString() != "0x0000000000000000000000000000000000000000000000000000000000000001")
        throw new Exception("UInt256 roundtrip failed");
});

RunTest("UInt160 Zero and Equality", () =>
{
    var zero1 = UInt160.Zero;
    var zero2 = new UInt160();
    if (zero1 != zero2)
        throw new Exception("UInt160.Zero equality failed");
});

RunTest("UInt256 Comparison", () =>
{
    var a = UInt256.Parse("0x0000000000000000000000000000000000000000000000000000000000000001");
    var b = UInt256.Parse("0x0000000000000000000000000000000000000000000000000000000000000002");
    if (a.CompareTo(b) >= 0)
        throw new Exception("UInt256 comparison failed");
});

// ============================================
// Test Group 2: Cryptography
// ============================================
Console.WriteLine("\n--- Cryptography ---");

RunTest("SHA256 Hash", () =>
{
    var data = "Hello Neo"u8.ToArray();
    var hash = data.Sha256();
    if (hash.Length != 32)
        throw new Exception($"SHA256 length wrong: {hash.Length}");
    Console.WriteLine($"  SHA256: {Convert.ToHexString(hash)}");
});

RunTest("RIPEMD160 Hash", () =>
{
    var data = "Hello Neo"u8.ToArray();
    var hash = data.RIPEMD160();
    if (hash.Length != 20)
        throw new Exception($"RIPEMD160 length wrong: {hash.Length}");
    Console.WriteLine($"  RIPEMD160: {Convert.ToHexString(hash)}");
});

RunTest("Murmur32 Hash", () =>
{
    var data = "Hello Neo"u8.ToArray();
    var hash = data.Murmur32(0);
    Console.WriteLine($"  Murmur32: {hash}");
});

RunTest("Double SHA256 (Hash256)", () =>
{
    var data = "Hello Neo"u8.ToArray();
    var hash = Crypto.Hash256(data);
    if (hash.Length != 32)
        throw new Exception($"Hash256 length wrong: {hash.Length}");
});

RunTest("SHA256 + RIPEMD160 (Hash160)", () =>
{
    var data = "Hello Neo"u8.ToArray();
    var hash = Crypto.Hash160(data);
    if (hash.Length != 20)
        throw new Exception($"Hash160 length wrong: {hash.Length}");
});

// ============================================
// Test Group 3: JSON Serialization
// ============================================
Console.WriteLine("\n--- JSON ---");

RunTest("JObject Create and Serialize", () =>
{
    var json = new JObject
    {
        ["name"] = "Neo",
        ["version"] = 3,
        ["active"] = true
    };
    var str = json.ToString();
    if (!str.Contains("Neo"))
        throw new Exception("JSON serialization failed");
    Console.WriteLine($"  JSON: {str}");
});

RunTest("JArray Operations", () =>
{
    var arr = new JArray { 1, 2, 3, "test" };
    if (arr.Count != 4)
        throw new Exception($"JArray count wrong: {arr.Count}");
    if (arr[3]!.AsString() != "test")
        throw new Exception("JArray element access failed");
});

RunTest("JSON Parse Complex Object", () =>
{
    var jsonStr = """{"nested":{"value":42},"array":[1,2,3]}""";
    var parsed = (JObject)JToken.Parse(jsonStr)!;
    var nested = (JObject)parsed["nested"]!;
    if (nested["value"]!.AsNumber() != 42)
        throw new Exception("Nested JSON parse failed");
});

RunTest("JSON Null Handling", () =>
{
    var json = new JObject { ["nullable"] = JToken.Null };
    if (json["nullable"] is not null && json["nullable"]!.GetType() != typeof(JToken))
        throw new Exception("JSON null handling failed");
});

// ============================================
// Test Group 4: Storage
// ============================================
Console.WriteLine("\n--- Storage ---");

RunTest("MemoryStore Put and Get", () =>
{
    using var store = new MemoryStore();
    var key = new byte[] { 0x01, 0x02, 0x03 };
    var value = new byte[] { 0xAA, 0xBB, 0xCC };
    store.Put(key, value);
    var retrieved = store.TryGet(key);
    if (retrieved == null || !retrieved.SequenceEqual(value))
        throw new Exception("MemoryStore Put/Get failed");
});

RunTest("MemoryStore Delete", () =>
{
    using var store = new MemoryStore();
    var key = new byte[] { 0x01 };
    store.Put(key, new byte[] { 0xFF });
    store.Delete(key);
    if (store.Contains(key))
        throw new Exception("MemoryStore Delete failed");
});

RunTest("MemoryStore Find Forward", () =>
{
    using var store = new MemoryStore();
    store.Put(new byte[] { 0x01, 0x01 }, new byte[] { 0x11 });
    store.Put(new byte[] { 0x01, 0x02 }, new byte[] { 0x12 });
    store.Put(new byte[] { 0x02, 0x01 }, new byte[] { 0x21 });

    var results = store.Find(new byte[] { 0x01 }, SeekDirection.Forward).ToList();
    if (results.Count < 2)
        throw new Exception($"Find returned {results.Count} results, expected >= 2");
    Console.WriteLine($"  Found {results.Count} items with prefix 0x01");
});

RunTest("MemoryStore Snapshot", () =>
{
    using var store = new MemoryStore();
    store.Put(new byte[] { 0x01 }, new byte[] { 0xAA });

    using var snapshot = store.GetSnapshot();
    snapshot.Put(new byte[] { 0x02 }, new byte[] { 0xBB });

    // Before commit, store should not have 0x02
    if (store.Contains(new byte[] { 0x02 }))
        throw new Exception("Snapshot leaked before commit");

    snapshot.Commit();

    // After commit, store should have 0x02
    if (!store.Contains(new byte[] { 0x02 }))
        throw new Exception("Snapshot commit failed");
});

RunTest("MemoryStore Snapshot Isolation", () =>
{
    using var store = new MemoryStore();
    store.Put(new byte[] { 0x01 }, new byte[] { 0xAA });

    using var snapshot = store.GetSnapshot();

    // Modify store after snapshot
    store.Put(new byte[] { 0x01 }, new byte[] { 0xBB });

    // Snapshot should still see original value
    if (!snapshot.TryGet(new byte[] { 0x01 }, out var snapshotValue) || snapshotValue[0] != 0xAA)
        throw new Exception("Snapshot isolation failed");
});

// ============================================
// Test Group 5: Protocol Types
// ============================================
Console.WriteLine("\n--- Protocol Types ---");

RunTest("MessageCommand Enum", () =>
{
    if ((byte)MessageCommand.Version != 0)
        throw new Exception("MessageCommand.Version wrong");
    if ((byte)MessageCommand.Verack != 1)
        throw new Exception("MessageCommand.Verack wrong");
    Console.WriteLine($"  MessageCommand.Version: {(byte)MessageCommand.Version}");
    Console.WriteLine($"  MessageCommand.Inv: {(byte)MessageCommand.Inv}");
});

RunTest("InventoryType Enum", () =>
{
    if ((byte)InventoryType.TX != 0x2b)
        throw new Exception("InventoryType.TX wrong");
    if ((byte)InventoryType.Block != 0x2c)
        throw new Exception("InventoryType.Block wrong");
    Console.WriteLine($"  InventoryType.TX: 0x{(byte)InventoryType.TX:X2}");
});

RunTest("WitnessScope Flags", () =>
{
    var scope = WitnessScope.CalledByEntry | WitnessScope.CustomContracts;
    if (!scope.HasFlag(WitnessScope.CalledByEntry))
        throw new Exception("WitnessScope flags failed");
    Console.WriteLine($"  Combined scope: {scope}");
});

// ============================================
// Test Group 6: IO Extensions
// ============================================
Console.WriteLine("\n--- IO Extensions ---");

RunTest("Byte Array to Hex String", () =>
{
    var bytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
    var hex = Convert.ToHexString(bytes);
    if (hex != "DEADBEEF")
        throw new Exception($"ToHexString failed: {hex}");
});

RunTest("Hex String to Byte Array", () =>
{
    var hex = "DEADBEEF";
    var bytes = Convert.FromHexString(hex);
    if (bytes.Length != 4 || bytes[0] != 0xDE)
        throw new Exception("FromHexString failed");
});

RunTest("VarInt Encoding", () =>
{
    // Test small value
    using var ms1 = new MemoryStream();
    using var writer1 = new BinaryWriter(ms1);
    writer1.WriteVarInt(100);
    if (ms1.Length != 1)
        throw new Exception($"VarInt small encoding wrong: {ms1.Length} bytes");

    // Test medium value
    using var ms2 = new MemoryStream();
    using var writer2 = new BinaryWriter(ms2);
    writer2.WriteVarInt(1000);
    if (ms2.Length != 3)
        throw new Exception($"VarInt medium encoding wrong: {ms2.Length} bytes");
});

RunTest("VarString Encoding", () =>
{
    var testString = "Hello Neo";
    using var ms = new MemoryStream();
    using var writer = new BinaryWriter(ms);
    writer.WriteVarString(testString);

    ms.Position = 0;
    using var reader = new BinaryReader(ms);
    var length = (int)reader.ReadVarInt(100);
    var decoded = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(length));
    if (decoded != testString)
        throw new Exception($"VarString roundtrip failed: {decoded}");
});

// ============================================
// Test Group 7: Edge Cases and Error Handling
// ============================================
Console.WriteLine("\n--- Edge Cases ---");

RunTest("Empty Byte Array Operations", () =>
{
    var empty = Array.Empty<byte>();
    var hash = empty.Sha256();
    if (hash.Length != 32)
        throw new Exception("Empty array SHA256 failed");
});

RunTest("Large Data Hashing", () =>
{
    var largeData = new byte[1024 * 1024]; // 1MB
    new Random(42).NextBytes(largeData);
    var hash = largeData.Sha256();
    if (hash.Length != 32)
        throw new Exception("Large data hashing failed");
    Console.WriteLine($"  Hashed 1MB of data");
});

RunTest("Storage Key Boundary", () =>
{
    using var store = new MemoryStore();
    var maxKey = new byte[256];
    Array.Fill(maxKey, (byte)0xFF);
    store.Put(maxKey, new byte[] { 0x01 });
    if (!store.Contains(maxKey))
        throw new Exception("Max key storage failed");
});

RunTest("JSON Special Characters", () =>
{
    var json = new JObject { ["text"] = "Hello\n\"World\"\t\\End" };
    var str = json.ToString();
    var parsed = (JObject)JToken.Parse(str)!;
    if (parsed["text"]!.AsString() != "Hello\n\"World\"\t\\End")
        throw new Exception("JSON special chars failed");
});

// ============================================
// Summary
// ============================================
Console.WriteLine("\n================================");
Console.WriteLine($"Tests Passed: {testsPassed}");
Console.WriteLine($"Tests Failed: {testsFailed}");
Console.WriteLine($"Total: {testsPassed + testsFailed}");
Console.WriteLine("================================");

if (testsFailed > 0)
{
    Console.WriteLine("\n[FAILURE] Some tests failed!");
    return 1;
}

Console.WriteLine("\n[SUCCESS] All NativeAOT compatibility tests passed!");
Console.WriteLine($"Process ID: {Environment.ProcessId}");
Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
Console.WriteLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");

return 0;
