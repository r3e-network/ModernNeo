// Copyright (C) 2015-2025 The Neo Project.
//
// IContractQueryService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;
using System.Collections.Generic;

namespace Neo.Services.Contracts
{
    /// <summary>
    /// Service for querying deployed smart contracts.
    /// </summary>
    public interface IContractQueryService
    {
        /// <summary>
        /// Gets a contract by its script hash.
        /// </summary>
        /// <param name="hashHex">Contract hash (hex, with or without 0x prefix).</param>
        /// <returns>Contract state if found, null otherwise.</returns>
        ContractState? GetContract(string hashHex);

        /// <summary>
        /// Gets a contract by its ID.
        /// </summary>
        /// <param name="id">Contract ID.</param>
        /// <returns>Contract state if found, null otherwise.</returns>
        ContractState? GetContractById(int id);

        /// <summary>
        /// Checks if a contract exists.
        /// </summary>
        /// <param name="hashHex">Contract hash (hex).</param>
        /// <returns>True if contract exists, false otherwise.</returns>
        bool ContractExists(string hashHex);

        /// <summary>
        /// Checks if a contract has a specific method.
        /// </summary>
        /// <param name="hashHex">Contract hash (hex).</param>
        /// <param name="method">Method name.</param>
        /// <param name="parameterCount">Number of parameters.</param>
        /// <returns>True if method exists, false otherwise.</returns>
        bool HasMethod(string hashHex, string method, int parameterCount);

        /// <summary>
        /// Lists all deployed contracts (paginated).
        /// </summary>
        /// <param name="skip">Number of contracts to skip.</param>
        /// <param name="take">Number of contracts to return (max 100).</param>
        /// <returns>Collection of contract states.</returns>
        IEnumerable<ContractState> ListContracts(int skip, int take);

        /// <summary>
        /// Gets the total count of deployed contracts.
        /// </summary>
        /// <returns>Total number of deployed contracts.</returns>
        int GetContractCount();
    }
}
