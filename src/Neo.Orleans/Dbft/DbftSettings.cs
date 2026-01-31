// Copyright (C) 2015-2025 The Neo Project.
//
// DbftSettings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable disable
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.Plugins;
using System;

namespace Neo.Orleans.Dbft
{
    public class DbftSettings : IPluginSettings
    {
        public string RecoveryLogs { get; }
        public bool IgnoreRecoveryLogs { get; }
        public bool AutoStart { get; }
        public uint Network { get; }
        public uint MaxBlockSize { get; }
        public long MaxBlockSystemFee { get; }

        public UnhandledExceptionPolicy ExceptionPolicy { get; }

        public DbftSettings()
        {
            RecoveryLogs = "ConsensusState";
            IgnoreRecoveryLogs = false;
            AutoStart = false;
            Network = ProtocolSettings.Default.Network;
            MaxBlockSize = 262144u;
            MaxBlockSystemFee = 150000000000L;
            ExceptionPolicy = UnhandledExceptionPolicy.StopNode;
        }

        public DbftSettings(IProtocolSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            RecoveryLogs = "ConsensusState";
            IgnoreRecoveryLogs = false;
            AutoStart = false;
            Network = settings.Network;
            MaxBlockSize = 262144u;
            MaxBlockSystemFee = 150000000000L;
            ExceptionPolicy = UnhandledExceptionPolicy.StopNode;
        }

        public DbftSettings(IConfigurationSection section)
        {
            RecoveryLogs = section.GetValue("RecoveryLogs", "ConsensusState");
            IgnoreRecoveryLogs = section.GetValue("IgnoreRecoveryLogs", false);
            AutoStart = section.GetValue("AutoStart", false);
            Network = section.GetValue("Network", ProtocolSettings.Default.Network);
            MaxBlockSize = section.GetValue("MaxBlockSize", 262144u);
            MaxBlockSystemFee = section.GetValue("MaxBlockSystemFee", 150000000000L);
            ExceptionPolicy = section.GetValue("UnhandledExceptionPolicy", UnhandledExceptionPolicy.StopNode);
        }
    }
}
