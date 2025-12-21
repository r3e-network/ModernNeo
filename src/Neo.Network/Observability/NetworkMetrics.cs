// Copyright (C) 2015-2025 The Neo Project.
//
// NetworkMetrics.cs file belongs to the neo project and is free
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

namespace Neo.Network.Observability
{
    internal static class NetworkMetrics
    {
        internal static readonly Meter Meter = new("Neo.Network");

        internal static readonly Counter<long> BytesSent = Meter.CreateCounter<long>(
            "neo_network_bytes_sent",
            unit: "By",
            description: "Total bytes sent over P2P connections.");

        internal static readonly Counter<long> BytesReceived = Meter.CreateCounter<long>(
            "neo_network_bytes_received",
            unit: "By",
            description: "Total bytes received over P2P connections.");

        internal static readonly Counter<long> MessagesSent = Meter.CreateCounter<long>(
            "neo_network_messages_sent",
            unit: "msg",
            description: "Total P2P messages sent.");

        internal static readonly Counter<long> MessagesReceived = Meter.CreateCounter<long>(
            "neo_network_messages_received",
            unit: "msg",
            description: "Total P2P messages received.");

        internal static ReadOnlySpan<KeyValuePair<string, object?>> TagCommand(string command)
        {
            return new[] { new KeyValuePair<string, object?>("command", command) };
        }
    }
}
