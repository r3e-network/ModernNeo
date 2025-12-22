# Neo Compatibility Verification Plan

## Overview
Comprehensive compatibility audit between ModernNeo and origin neo-project/neo to ensure byte-for-byte protocol compatibility.

## Task Breakdown

### Task 1: Serialization Golden Vector Verification
**ID:** COMPAT-SERIAL
**Description:** Run and verify all serialization golden vector tests
**File Scope:**
- tests/Neo.UnitTests/Network/P2P/Payloads/UT_BlockGolden.cs
- tests/Neo.UnitTests/Network/P2P/Payloads/UT_HeaderGolden.cs
- tests/Neo.UnitTests/Network/P2P/Payloads/UT_TransactionGolden.cs
- tests/Neo.UnitTests/Network/P2P/Payloads/UT_WitnessGolden.cs
- tests/Neo.UnitTests/Network/P2P/Payloads/UT_TransactionSignedGolden.cs
- tests/Neo.UnitTests/Network/P2P/Payloads/UT_ProtocolCompatibility.cs
**Dependencies:** None
**Test Command:** `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~Payloads" --verbosity minimal`

### Task 2: Cryptographic Operations Verification
**ID:** COMPAT-CRYPTO
**Description:** Verify hash functions, ECDSA signatures, address encoding against known test vectors
**File Scope:**
- tests/Neo.UnitTests/Cryptography/UT_HashGoldenVectors.cs
- tests/Neo.UnitTests/Cryptography/UT_Crypto.cs
- tests/Neo.UnitTests/Cryptography/UT_Base58.cs
- tests/Neo.UnitTests/Cryptography/UT_ECPoint.cs
**Dependencies:** None
**Test Command:** `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~Cryptography" --verbosity minimal`

### Task 3: Network Protocol Verification
**ID:** COMPAT-NETWORK
**Description:** Verify P2P message formats, VarInt encoding, compression handling
**File Scope:**
- tests/Neo.UnitTests/Network/P2P/Payloads/UT_ProtocolCompatibility.cs
- tests/Neo.UnitTests/Network/P2P/Transport/UT_ProtocolNegotiator.cs
**Dependencies:** None
**Test Command:** `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~Protocol" --verbosity minimal`

### Task 4: Consensus Mechanism Verification
**ID:** COMPAT-CONSENSUS
**Description:** Verify dBFT message types, threshold calculations, state machine behavior
**File Scope:**
- tests/Neo.UnitTests/Consensus/UT_DbftStateMachine.cs
- tests/Neo.UnitTests/Consensus/UT_ConsensusThreshold.cs
- tests/Neo.Orleans.Tests/Grains/ConsensusGrainTests.cs
**Dependencies:** None
**Test Command:** `dotnet test tests/Neo.UnitTests --filter "FullyQualifiedName~Consensus" && dotnet test tests/Neo.Orleans.Tests --filter "FullyQualifiedName~Consensus"`

## Execution Order
All 4 tasks are independent and can run in parallel.

## Success Criteria
- All existing tests pass (0 failures)
- No byte-level serialization mismatches with origin Neo
- Consensus threshold formula verified for N=1 to N=200
