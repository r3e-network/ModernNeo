// Copyright (C) 2015-2025 The Neo Project.
//
// WitnessBuilder.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Core.Builders;
using Neo.Network.P2P.Payloads;
using Neo.Network.P2P.Payloads.Conditions;
using System;

namespace Neo.Builders
{
    public sealed class WitnessBuilder : IWitnessBuilder<Witness>
    {
        private byte[] _invocationScript = [];
        private byte[] _verificationScript = [];

        private WitnessBuilder() { }

        public static WitnessBuilder CreateEmpty()
        {
            return new WitnessBuilder();
        }

        public WitnessBuilder AddInvocation(byte[] bytes)
        {
            if (_invocationScript.Length > 0)
                throw new InvalidOperationException("Invocation script already exists in the witness builder. Only one invocation script can be added per witness.");

            _invocationScript = bytes;
            return this;
        }

        public WitnessBuilder AddVerification(byte[] bytes)
        {
            if (_verificationScript.Length > 0)
                throw new InvalidOperationException("Verification script already exists in the witness builder. Only one verification script can be added per witness.");

            _verificationScript = bytes;
            return this;
        }

        public Witness Build()
        {
            return new Witness()
            {
                InvocationScript = _invocationScript,
                VerificationScript = _verificationScript,
            };
        }

        IWitnessBuilder<Witness> IWitnessBuilder<Witness>.InvocationScript(byte[] script)
        {
            AddInvocation(script);
            return this;
        }

        IWitnessBuilder<Witness> IWitnessBuilder<Witness>.VerificationScript(byte[] script)
        {
            AddVerification(script);
            return this;
        }
    }
}
