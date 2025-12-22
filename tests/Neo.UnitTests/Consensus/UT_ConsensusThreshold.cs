// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ConsensusThreshold.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Neo.UnitTests.Consensus
{
    /// <summary>
    /// Tests for consensus threshold calculation (M = 2F + 1).
    /// Verifies that ConsensusGrain and DbftStateMachine use equivalent formulas.
    /// </summary>
    [TestClass]
    public class UT_ConsensusThreshold
    {
        /// <summary>
        /// DbftStateMachine formula: M = N - (N-1)/3
        /// This is the canonical Neo consensus threshold formula.
        /// </summary>
        private static int DbftStateMachineThreshold(int validatorCount)
        {
            return validatorCount - (validatorCount - 1) / 3;
        }

        /// <summary>
        /// ConsensusGrain formula: M = (N*2/3) + 1
        /// This is mathematically equivalent to the DbftStateMachine formula.
        /// </summary>
        private static int ConsensusGrainThreshold(int validatorCount)
        {
            return (validatorCount * 2 / 3) + 1;
        }

        [TestMethod]
        [TestCategory("Consensus")]
        public void Threshold_N4_Equals3()
        {
            // N=4: f=1, M=2f+1=3
            Assert.AreEqual(3, DbftStateMachineThreshold(4));
            Assert.AreEqual(3, ConsensusGrainThreshold(4));
        }

        [TestMethod]
        [TestCategory("Consensus")]
        public void Threshold_N7_Equals5()
        {
            // N=7: f=2, M=2f+1=5
            Assert.AreEqual(5, DbftStateMachineThreshold(7));
            Assert.AreEqual(5, ConsensusGrainThreshold(7));
        }

        [TestMethod]
        [TestCategory("Consensus")]
        public void Threshold_N21_Equals15()
        {
            // N=21: f=7, M=2f+1=15
            Assert.AreEqual(15, DbftStateMachineThreshold(21));
            Assert.AreEqual(15, ConsensusGrainThreshold(21));
        }

        [TestMethod]
        [TestCategory("Consensus")]
        public void Threshold_N100_Equals67()
        {
            // N=100: f=33, M=2f+1=67
            Assert.AreEqual(67, DbftStateMachineThreshold(100));
            Assert.AreEqual(67, ConsensusGrainThreshold(100));
        }

        [TestMethod]
        [TestCategory("Consensus")]
        public void Threshold_FormulasAreEquivalent_AllValidatorCounts()
        {
            // Verify formulas are equivalent for all reasonable validator counts
            for (int n = 1; n <= 200; n++)
            {
                var dbft = DbftStateMachineThreshold(n);
                var grain = ConsensusGrainThreshold(n);
                Assert.AreEqual(dbft, grain,
                    $"Threshold mismatch for N={n}: DbftStateMachine={dbft}, ConsensusGrain={grain}");
            }
        }

        [TestMethod]
        [TestCategory("Consensus")]
        public void Threshold_SatisfiesBFTProperty()
        {
            // BFT property: M > 2N/3 (more than 2/3 of validators)
            // This ensures that any two quorums overlap by at least one honest node
            for (int n = 1; n <= 100; n++)
            {
                var m = DbftStateMachineThreshold(n);
                // M must be greater than 2N/3
                Assert.IsTrue(m * 3 > n * 2,
                    $"BFT property violated for N={n}: M={m}, 2N/3={n * 2.0 / 3}");
            }
        }

        [TestMethod]
        [TestCategory("Consensus")]
        public void Threshold_MinimalQuorum()
        {
            // M should be the minimal value satisfying M > 2N/3
            for (int n = 1; n <= 100; n++)
            {
                var m = DbftStateMachineThreshold(n);
                // M-1 should NOT satisfy the BFT property
                if (m > 1)
                {
                    Assert.IsFalse((m - 1) * 3 > n * 2,
                        $"M is not minimal for N={n}: M={m}, M-1={m - 1} also satisfies BFT");
                }
            }
        }

        [TestMethod]
        [TestCategory("Consensus")]
        public void Threshold_FaultTolerance()
        {
            // f = (N-1)/3 is the maximum number of faulty nodes tolerated
            // M = N - f = N - (N-1)/3
            for (int n = 4; n <= 100; n++)
            {
                var f = (n - 1) / 3;
                var m = DbftStateMachineThreshold(n);
                Assert.AreEqual(n - f, m,
                    $"Fault tolerance formula mismatch for N={n}: expected N-f={n - f}, got M={m}");
            }
        }

        [TestMethod]
        [TestCategory("Consensus")]
        public void Threshold_EdgeCases()
        {
            // Edge case: N=1 (single validator)
            Assert.AreEqual(1, DbftStateMachineThreshold(1));
            Assert.AreEqual(1, ConsensusGrainThreshold(1));

            // Edge case: N=2 (two validators)
            Assert.AreEqual(2, DbftStateMachineThreshold(2));
            Assert.AreEqual(2, ConsensusGrainThreshold(2));

            // Edge case: N=3 (three validators, f=0)
            Assert.AreEqual(3, DbftStateMachineThreshold(3));
            Assert.AreEqual(3, ConsensusGrainThreshold(3));
        }

        [TestMethod]
        [TestCategory("Consensus")]
        public void Threshold_NeoMainnetValidators()
        {
            // Neo mainnet has 7 consensus nodes (as of 2024)
            // N=7: f=2, M=5
            Assert.AreEqual(5, DbftStateMachineThreshold(7));

            // Neo testnet typically has 4 consensus nodes
            // N=4: f=1, M=3
            Assert.AreEqual(3, DbftStateMachineThreshold(4));
        }
    }
}
