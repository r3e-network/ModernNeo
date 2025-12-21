// Copyright (C) 2015-2025 The Neo Project.
//
// LedgerMetrics.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Neo.Ledger
{
    internal static class LedgerMetrics
    {
        internal static readonly Meter Meter = new("Neo.Ledger");

        internal static readonly Counter<long> BlocksPersisted = Meter.CreateCounter<long>(
            "neo_ledger_blocks_persisted",
            unit: "blk",
            description: "Total number of blocks persisted.");

        internal static readonly Histogram<double> BlockPersistDurationMs = Meter.CreateHistogram<double>(
            "neo_ledger_block_persist_duration_ms",
            unit: "ms",
            description: "Block persistence duration in milliseconds.");

        internal static readonly Counter<long> TxPreverifyTotal = Meter.CreateCounter<long>(
            "neo_ledger_tx_preverify_total",
            unit: "tx",
            description: "Total transactions preverified (state-independent).");

        internal static ReadOnlySpan<KeyValuePair<string, object?>> TagResult(string result)
        {
            return new[] { new KeyValuePair<string, object?>("result", result) };
        }
    }
}
