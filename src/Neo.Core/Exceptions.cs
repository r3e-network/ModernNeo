// Copyright (C) 2015-2025 The Neo Project.
//
// Exceptions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo
{
    /// <summary>
    /// Represents errors that occur during blockchain operations.
    /// </summary>
    public class BlockchainException : Exception
    {
        public BlockchainException() { }
        public BlockchainException(string message) : base(message) { }
        public BlockchainException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Represents errors that occur during transaction validation.
    /// </summary>
    public class TransactionValidationException : Exception
    {
        public TransactionValidationException() { }
        public TransactionValidationException(string message) : base(message) { }
        public TransactionValidationException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Represents errors that occur during smart contract execution.
    /// </summary>
    public class SmartContractException : Exception
    {
        public SmartContractException() { }
        public SmartContractException(string message) : base(message) { }
        public SmartContractException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Represents errors that occur during consensus operations.
    /// </summary>
    public class ConsensusException : Exception
    {
        public ConsensusException() { }
        public ConsensusException(string message) : base(message) { }
        public ConsensusException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Represents errors that occur during storage operations.
    /// </summary>
    public class StorageException : Exception
    {
        public StorageException() { }
        public StorageException(string message) : base(message) { }
        public StorageException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Represents errors that occur during network operations.
    /// </summary>
    public class NetworkException : Exception
    {
        public NetworkException() { }
        public NetworkException(string message) : base(message) { }
        public NetworkException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Represents errors that occur during cryptography operations.
    /// </summary>
    public class CryptographyException : Exception
    {
        public CryptographyException() { }
        public CryptographyException(string message) : base(message) { }
        public CryptographyException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Represents errors that occur during plugin operations.
    /// </summary>
    public class PluginException : Exception
    {
        public PluginException() { }
        public PluginException(string message) : base(message) { }
        public PluginException(string message, Exception inner) : base(message, inner) { }
    }
}
