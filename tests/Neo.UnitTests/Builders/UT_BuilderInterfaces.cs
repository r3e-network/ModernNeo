// Copyright (C) 2015-2025 The Neo Project.
//
// UT_BuilderInterfaces.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Builders;
using Neo.Core.Builders;
using Neo.Network.P2P.Payloads;

namespace Neo.UnitTests.Builders
{
    [TestClass]
    public class UT_BuilderInterfaces
    {
        [TestMethod]
        public void TestConcreteBuildersImplementInterfaces()
        {
            Assert.IsInstanceOfType(TransactionBuilder.CreateEmpty(), typeof(ITransactionBuilder<Transaction, Signer, Witness>));
            Assert.IsInstanceOfType(WitnessBuilder.CreateEmpty(), typeof(IWitnessBuilder<Witness>));
            Assert.IsInstanceOfType(SignerBuilder.Create(UInt160.Zero), typeof(ISignerBuilder<Signer>));
        }

        [TestMethod]
        public void TestBuilderFactoryCanBuildTransactionViaInterfaces()
        {
            IBuilderFactory<Transaction, Signer, Witness> factory = new BuilderFactory();

            var signer = factory.CreateSignerBuilder(new byte[UInt160.Length])
                .Scopes((byte)WitnessScope.None)
                .Build();

            var witness = factory.CreateWitnessBuilder()
                .InvocationScript([])
                .VerificationScript([])
                .Build();

            var tx = factory.CreateTransactionBuilder()
                .Version(0)
                .Nonce(123u)
                .SystemFee(0)
                .NetworkFee(0)
                .ValidUntil(1u)
                .Script([0x40])
                .AddSigner(signer)
                .AddWitness(witness)
                .Build();

            Assert.IsNotNull(tx.Hash);
            Assert.AreEqual(123u, tx.Nonce);
            Assert.AreEqual(1u, tx.ValidUntilBlock);
        }
    }
}

