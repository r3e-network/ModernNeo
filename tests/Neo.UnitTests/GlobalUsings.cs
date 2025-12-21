// Copyright (C) 2015-2025 The Neo Project.
//
// GlobalUsings.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

// Core types
global using Neo;
global using Neo.Cryptography;
global using Neo.Cryptography.ECC;
global using Neo.Extensions;
global using Neo.IO;
global using Neo.IO.Caching;
global using Neo.Json;

// Persistence
global using Neo.Persistence;

// Network and Protocol
global using Neo.Network.P2P;
global using Neo.Network.P2P.Payloads;
global using Neo.Network.P2P.Payloads.Conditions;

// Ledger
global using Neo.Ledger;

// SmartContract (from Neo project, not separate module)
global using Neo.SmartContract;
global using Neo.SmartContract.Manifest;
global using Neo.SmartContract.Native;
global using Neo.SmartContract.Iterators;

// Wallets (from Neo project)
global using Neo.Wallets;
global using Neo.Wallets.NEP6;

// Builders
global using Neo.Builders;

// Services
global using Neo.Services;

// Sign
global using Neo.Sign;

// VM
global using Neo.VM;
// Note: Neo.VM.Types is NOT globally used to avoid Array ambiguity with System.Array

// System
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Numerics;
global using System.Threading.Tasks;
