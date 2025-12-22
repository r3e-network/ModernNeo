// Copyright (C) 2015-2025 The Neo Project.
//
// GlobalUsings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

// Core types
global using Neo;
// Builders
global using Neo.Builders;
global using Neo.Cryptography;
global using Neo.Cryptography.ECC;
global using Neo.Extensions;
global using Neo.IO;
global using Neo.IO.Caching;
global using Neo.Json;
// Ledger
global using Neo.Ledger;
// Network and Protocol
global using Neo.Network.P2P;
global using Neo.Network.P2P.Payloads;
global using Neo.Network.P2P.Payloads.Conditions;
// Persistence
global using Neo.Persistence;
// Services
global using Neo.Services;
// Sign
global using Neo.Sign;
// SmartContract (from Neo project, not separate module)
global using Neo.SmartContract;
global using Neo.SmartContract.Iterators;
global using Neo.SmartContract.Manifest;
global using Neo.SmartContract.Native;
// VM
global using Neo.VM;
// Wallets (from Neo project)
global using Neo.Wallets;
global using Neo.Wallets.NEP6;
// Note: Neo.VM.Types is NOT globally used to avoid Array ambiguity with System.Array

// System
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Numerics;
global using System.Threading.Tasks;
