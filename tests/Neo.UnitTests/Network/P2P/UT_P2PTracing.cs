// Copyright (C) 2015-2025 The Neo Project.
//
// UT_P2PTracing.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.Observability;
using Neo.Network.P2P;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Neo.UnitTests.Network.P2P
{
    [TestClass]
    public class UT_P2PTracing
    {
        private ActivityListener _listener = null!;
        private List<Activity> _capturedActivities = null!;

        [TestInitialize]
        public void Setup()
        {
            _capturedActivities = new List<Activity>();
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == P2PTracing.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => _capturedActivities.Add(activity)
            };
            ActivitySource.AddActivityListener(_listener);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _listener.Dispose();
        }

        #region ActivitySource Tests

        [TestMethod]
        public void ActivitySourceName_IsCorrect()
        {
            Assert.AreEqual("Neo.Network.P2P", P2PTracing.ActivitySourceName);
        }

        [TestMethod]
        public void ActivitySource_IsNotNull()
        {
            Assert.IsNotNull(P2PTracing.ActivitySource);
        }

        #endregion

        #region Message Receive Span Tests

        [TestMethod]
        public void StartMessageReceiveSpan_CreatesActivity()
        {
            using var activity = P2PTracing.StartMessageReceiveSpan(
                MessageCommand.Block,
                "192.168.1.100:10333",
                1024);

            Assert.IsNotNull(activity);
            Assert.AreEqual("p2p.receive.Block", activity.OperationName);
            Assert.AreEqual(ActivityKind.Consumer, activity.Kind);
        }

        [TestMethod]
        public void StartMessageReceiveSpan_SetsCorrectTags()
        {
            using var activity = P2PTracing.StartMessageReceiveSpan(
                MessageCommand.Transaction,
                "10.0.0.1:10333",
                512);

            Assert.IsNotNull(activity);
            Assert.AreEqual("neo.p2p", activity.GetTagItem("messaging.system"));
            Assert.AreEqual("Transaction", activity.GetTagItem("neo.p2p.command"));
            Assert.AreEqual(512, activity.GetTagItem("messaging.message.body.size"));
            Assert.AreEqual("10.0.0.1:10333", activity.GetTagItem("net.peer.name"));
            Assert.AreEqual("receive", activity.GetTagItem("messaging.operation"));
        }

        [TestMethod]
        public void StartMessageReceiveSpan_WithNullEndpoint_DoesNotSetPeerTag()
        {
            using var activity = P2PTracing.StartMessageReceiveSpan(
                MessageCommand.Ping,
                null,
                8);

            Assert.IsNotNull(activity);
            Assert.IsNull(activity.GetTagItem("net.peer.name"));
        }

        #endregion

        #region Message Send Span Tests

        [TestMethod]
        public void StartMessageSendSpan_CreatesActivity()
        {
            using var activity = P2PTracing.StartMessageSendSpan(
                MessageCommand.Pong,
                "192.168.1.100:10333",
                8);

            Assert.IsNotNull(activity);
            Assert.AreEqual("p2p.send.Pong", activity.OperationName);
            Assert.AreEqual(ActivityKind.Producer, activity.Kind);
        }

        [TestMethod]
        public void StartMessageSendSpan_SetsCorrectTags()
        {
            using var activity = P2PTracing.StartMessageSendSpan(
                MessageCommand.Inv,
                "10.0.0.2:10333",
                256);

            Assert.IsNotNull(activity);
            Assert.AreEqual("neo.p2p", activity.GetTagItem("messaging.system"));
            Assert.AreEqual("Inv", activity.GetTagItem("neo.p2p.command"));
            Assert.AreEqual(256, activity.GetTagItem("messaging.message.body.size"));
            Assert.AreEqual("send", activity.GetTagItem("messaging.operation"));
        }

        #endregion

        #region Broadcast Span Tests

        [TestMethod]
        public void StartBroadcastSpan_CreatesActivity()
        {
            using var activity = P2PTracing.StartBroadcastSpan(
                MessageCommand.Block,
                10,
                2048);

            Assert.IsNotNull(activity);
            Assert.AreEqual("p2p.broadcast.Block", activity.OperationName);
            Assert.AreEqual(ActivityKind.Producer, activity.Kind);
        }

        [TestMethod]
        public void StartBroadcastSpan_SetsCorrectTags()
        {
            using var activity = P2PTracing.StartBroadcastSpan(
                MessageCommand.Transaction,
                25,
                512);

            Assert.IsNotNull(activity);
            Assert.AreEqual("neo.p2p", activity.GetTagItem("messaging.system"));
            Assert.AreEqual("broadcast", activity.GetTagItem("messaging.operation"));
            Assert.AreEqual("Transaction", activity.GetTagItem("neo.p2p.command"));
            Assert.AreEqual(25, activity.GetTagItem("neo.p2p.peer_count"));
            Assert.AreEqual(512, activity.GetTagItem("messaging.message.body.size"));
        }

        #endregion

        #region Inventory Correlation Tests

        [TestMethod]
        public void AddTransactionLink_SetsTagsAndEvent()
        {
            using var activity = P2PTracing.StartMessageReceiveSpan(
                MessageCommand.Transaction,
                "peer",
                100);

            var txHash = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
            P2PTracing.AddTransactionLink(activity, txHash);

            Assert.AreEqual(txHash, activity?.GetTagItem("neo.tx.hash"));
            var events = activity?.Events.ToList();
            Assert.IsNotNull(events);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("tx.correlated", events[0].Name);
        }

        [TestMethod]
        public void AddTransactionLink_WithNullActivity_DoesNotThrow()
        {
            // Should not throw
            P2PTracing.AddTransactionLink(null, "0x1234");
        }

        [TestMethod]
        public void AddTransactionLink_WithEmptyHash_DoesNotSetTags()
        {
            using var activity = P2PTracing.StartMessageReceiveSpan(
                MessageCommand.Transaction,
                "peer",
                100);

            P2PTracing.AddTransactionLink(activity, "");

            Assert.IsNull(activity?.GetTagItem("neo.tx.hash"));
        }

        [TestMethod]
        public void AddBlockLink_SetsTagsAndEvent()
        {
            using var activity = P2PTracing.StartMessageReceiveSpan(
                MessageCommand.Block,
                "peer",
                2048);

            var blockHash = "0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
            P2PTracing.AddBlockLink(activity, blockHash, 12345);

            Assert.AreEqual(blockHash, activity?.GetTagItem("neo.block.hash"));
            Assert.AreEqual(12345u, activity?.GetTagItem("neo.block.index"));
            var events = activity?.Events.ToList();
            Assert.IsNotNull(events);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("block.correlated", events[0].Name);
        }

        [TestMethod]
        public void AddInventoryCorrelation_SetsTags()
        {
            using var activity = P2PTracing.StartMessageReceiveSpan(
                MessageCommand.Inv,
                "peer",
                64);

            P2PTracing.AddInventoryCorrelation(activity, "TX", "0x1234");

            Assert.AreEqual("TX", activity?.GetTagItem("neo.inventory.type"));
            Assert.AreEqual("0x1234", activity?.GetTagItem("neo.inventory.hash"));
        }

        [TestMethod]
        public void AddInventoryCorrelations_SetsMultipleHashes()
        {
            using var activity = P2PTracing.StartMessageReceiveSpan(
                MessageCommand.Inv,
                "peer",
                256);

            var hashes = new[] { "0x1111", "0x2222", "0x3333", "0x4444", "0x5555", "0x6666" };
            P2PTracing.AddInventoryCorrelations(activity, "TX", hashes);

            Assert.AreEqual("TX", activity?.GetTagItem("neo.inventory.type"));
            Assert.AreEqual(6, activity?.GetTagItem("neo.inventory.count"));
            // Only first 5 hashes are stored as individual tags
            Assert.AreEqual("0x1111", activity?.GetTagItem("neo.inventory.hash.0"));
            Assert.AreEqual("0x5555", activity?.GetTagItem("neo.inventory.hash.4"));
            Assert.IsNull(activity?.GetTagItem("neo.inventory.hash.5"));
        }

        #endregion

        #region Connection Span Tests

        [TestMethod]
        public void StartConnectSpan_CreatesActivity()
        {
            using var activity = P2PTracing.StartConnectSpan("192.168.1.100:10333");

            Assert.IsNotNull(activity);
            Assert.AreEqual("p2p.connect", activity.OperationName);
            Assert.AreEqual(ActivityKind.Client, activity.Kind);
            Assert.AreEqual("192.168.1.100:10333", activity.GetTagItem("net.peer.name"));
            Assert.AreEqual("connect", activity.GetTagItem("neo.p2p.operation"));
        }

        [TestMethod]
        public void StartAcceptSpan_CreatesActivity()
        {
            using var activity = P2PTracing.StartAcceptSpan("10.0.0.1:54321");

            Assert.IsNotNull(activity);
            Assert.AreEqual("p2p.accept", activity.OperationName);
            Assert.AreEqual(ActivityKind.Server, activity.Kind);
            Assert.AreEqual("10.0.0.1:54321", activity.GetTagItem("net.peer.name"));
            Assert.AreEqual("accept", activity.GetTagItem("neo.p2p.operation"));
        }

        [TestMethod]
        public void StartHandshakeSpan_CreatesActivity()
        {
            using var activity = P2PTracing.StartHandshakeSpan("peer:10333");

            Assert.IsNotNull(activity);
            Assert.AreEqual("p2p.handshake", activity.OperationName);
            Assert.AreEqual(ActivityKind.Internal, activity.Kind);
            Assert.AreEqual("handshake", activity.GetTagItem("neo.p2p.operation"));
        }

        [TestMethod]
        public void RecordConnectionSuccess_SetsStatusAndTags()
        {
            using var activity = P2PTracing.StartConnectSpan("peer:10333");
            P2PTracing.RecordConnectionSuccess(activity, nodeVersion: 3090000, userAgent: "/Neo:3.9.0/");

            Assert.AreEqual(ActivityStatusCode.Ok, activity?.Status);
            Assert.AreEqual("connected", activity?.GetTagItem("neo.p2p.connection.status"));
            Assert.AreEqual(3090000u, activity?.GetTagItem("neo.p2p.node.version"));
            Assert.AreEqual("/Neo:3.9.0/", activity?.GetTagItem("neo.p2p.node.user_agent"));
        }

        [TestMethod]
        public void RecordConnectionFailure_SetsErrorStatus()
        {
            using var activity = P2PTracing.StartConnectSpan("peer:10333");
            P2PTracing.RecordConnectionFailure(activity, "Connection refused");

            Assert.AreEqual(ActivityStatusCode.Error, activity?.Status);
            Assert.AreEqual("failed", activity?.GetTagItem("neo.p2p.connection.status"));
            Assert.AreEqual("Connection refused", activity?.GetTagItem("neo.p2p.connection.failure_reason"));
        }

        [TestMethod]
        public void RecordDisconnection_SetsTags()
        {
            using var activity = P2PTracing.StartConnectSpan("peer:10333");
            P2PTracing.RecordDisconnection(activity, "Timeout");

            Assert.AreEqual("disconnected", activity?.GetTagItem("neo.p2p.connection.status"));
            Assert.AreEqual("Timeout", activity?.GetTagItem("neo.p2p.disconnection.reason"));
        }

        #endregion

        #region Synchronization Span Tests

        [TestMethod]
        public void StartBlockSyncSpan_CreatesActivity()
        {
            using var activity = P2PTracing.StartBlockSyncSpan(1000, 2000);

            Assert.IsNotNull(activity);
            Assert.AreEqual("p2p.sync.blocks", activity.OperationName);
            Assert.AreEqual(ActivityKind.Internal, activity.Kind);
            Assert.AreEqual(1000u, activity.GetTagItem("neo.sync.start_index"));
            Assert.AreEqual(2000u, activity.GetTagItem("neo.sync.end_index"));
            Assert.AreEqual(1001u, activity.GetTagItem("neo.sync.block_count"));
        }

        [TestMethod]
        public void StartHeaderSyncSpan_CreatesActivity()
        {
            using var activity = P2PTracing.StartHeaderSyncSpan(5000, 500);

            Assert.IsNotNull(activity);
            Assert.AreEqual("p2p.sync.headers", activity.OperationName);
            Assert.AreEqual(5000u, activity.GetTagItem("neo.sync.start_index"));
            Assert.AreEqual(500, activity.GetTagItem("neo.sync.header_count"));
        }

        [TestMethod]
        public void RecordSyncProgress_SetsTags()
        {
            using var activity = P2PTracing.StartBlockSyncSpan(0, 10000);
            P2PTracing.RecordSyncProgress(activity, 5000, 10000);

            Assert.AreEqual(5000u, activity?.GetTagItem("neo.sync.current_index"));
            Assert.AreEqual(10000u, activity?.GetTagItem("neo.sync.total_blocks"));
            Assert.AreEqual(50.0, activity?.GetTagItem("neo.sync.progress_percent"));
        }

        #endregion

        #region Helper Method Tests

        [TestMethod]
        public void RecordException_SetsErrorStatusAndEvent()
        {
            using var activity = P2PTracing.StartMessageReceiveSpan(
                MessageCommand.Block,
                "peer",
                100);

            var exception = new InvalidOperationException("Test error");
            P2PTracing.RecordException(activity, exception);

            Assert.AreEqual(ActivityStatusCode.Error, activity?.Status);
            Assert.AreEqual("Test error", activity?.StatusDescription);

            var events = activity?.Events.ToList();
            Assert.IsNotNull(events);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("exception", events[0].Name);
        }

        [TestMethod]
        public void RecordException_WithNullActivity_DoesNotThrow()
        {
            var exception = new InvalidOperationException("Test");
            // Should not throw
            P2PTracing.RecordException(null, exception);
        }

        [TestMethod]
        public void RecordSuccess_SetsOkStatus()
        {
            using var activity = P2PTracing.StartMessageReceiveSpan(
                MessageCommand.Block,
                "peer",
                100);

            P2PTracing.RecordSuccess(activity);

            Assert.AreEqual(ActivityStatusCode.Ok, activity?.Status);
        }

        [TestMethod]
        public void RecordSuccess_WithNullActivity_DoesNotThrow()
        {
            // Should not throw
            P2PTracing.RecordSuccess(null);
        }

        #endregion
    }
}
