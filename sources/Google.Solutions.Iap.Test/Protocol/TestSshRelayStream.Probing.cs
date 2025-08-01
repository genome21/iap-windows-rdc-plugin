﻿//
// Copyright 2019 Google LLC
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//

using Google.Solutions.Apis.Auth;
using Google.Solutions.Apis.Locator;
using Google.Solutions.Iap.Net;
using Google.Solutions.Iap.Protocol;
using Google.Solutions.Testing.Apis;
using Google.Solutions.Testing.Apis.Integration;
using NUnit.Framework;
using System;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Google.Solutions.Iap.Test.Protocol
{
    [TestFixture]
    [UsesCloudResources]
    public class TestSshRelayStreamProbing : IapFixtureBase
    {
        [Test]
        public async Task ProbeConnection_WhenProjectDoesntExist(
            [Credential(Role = PredefinedRole.IapTunnelUser)] 
            ResourceTask<IAuthorization> auth)
        {
            var client = new IapClient(
                IapClient.CreateEndpoint(),
                await auth,
                TestProject.UserAgent);

            using (var stream = new SshRelayStream(
                client.GetTarget(
                    new InstanceLocator("invalid", TestProject.Zone, "invalid"),
                    80,
                    IapClient.DefaultNetworkInterface)))
            {
                await ExceptionAssert
                    .ThrowsAsync<SshRelayDeniedException>(() =>
                        stream.ProbeConnectionAsync(TimeSpan.FromSeconds(10)))
                    .ConfigureAwait(false);
            }
        }

        [Test]
        public async Task ProbeConnection_WhenZoneDoesntExist(
            [Credential(Role = PredefinedRole.IapTunnelUser)] 
            ResourceTask<IAuthorization> auth)
        {
            var client = new IapClient(
                IapClient.CreateEndpoint(),
                await auth,
                TestProject.UserAgent);

            using (var stream = new SshRelayStream(
               client.GetTarget(
                    new InstanceLocator(
                        TestProject.ProjectId,
                        "invalid",
                        "invalid"),
                    80,
                    IapClient.DefaultNetworkInterface)))
            {
                await ExceptionAssert
                    .ThrowsAsync<SshRelayBackendNotFoundException>(
                        () => stream.ProbeConnectionAsync(TimeSpan.FromSeconds(10)))
                    .ConfigureAwait(false);
            }
        }

        [Test]
        public async Task ProbeConnection_WhenInstanceDoesntExist(
            [Credential(Role = PredefinedRole.IapTunnelUser)] 
            ResourceTask<IAuthorization> auth)
        {
            var client = new IapClient(
                IapClient.CreateEndpoint(),
                await auth,
                TestProject.UserAgent);

            using (var stream = new SshRelayStream(
                client.GetTarget(
                    new InstanceLocator(
                        TestProject.ProjectId,
                        TestProject.Zone,
                        "invalid"),
                    80,
                    IapClient.DefaultNetworkInterface)))
            {
                await ExceptionAssert
                    .ThrowsAsync<SshRelayBackendNotFoundException>(
                        () => stream.ProbeConnectionAsync(TimeSpan.FromSeconds(10)))
                    .ConfigureAwait(false);
            }
        }

        [Test]
        public async Task ProbeConnection_WhenInstanceExistsAndIsListening(
            [WindowsInstance] ResourceTask<InstanceLocator> testInstance,
            [Credential(Role = PredefinedRole.IapTunnelUser)]
            ResourceTask<IAuthorization> auth)
        {
            var client = new IapClient(
                IapClient.CreateEndpoint(),
                await auth,
                TestProject.UserAgent);

            using (var stream = new SshRelayStream(
                client.GetTarget(
                    await testInstance,
                    3389,
                    IapClient.DefaultNetworkInterface)))
            {
                await stream
                    .ProbeConnectionAsync(TimeSpan.FromSeconds(10))
                    .ConfigureAwait(false);
            }
        }

        [Test]
        public async Task ProbeConnection_WhenInstanceExistsButNotListening(
            [WindowsInstance] ResourceTask<InstanceLocator> testInstance,
            [Credential(Role = PredefinedRole.IapTunnelUser)] 
            ResourceTask<IAuthorization> auth)
        {
            var client = new IapClient(
                IapClient.CreateEndpoint(),
                await auth,
                TestProject.UserAgent);

            using (var stream = new SshRelayStream(
               client.GetTarget(
                    await testInstance,
                    22,
                    IapClient.DefaultNetworkInterface)))
            {
                await ExceptionAssert
                    .ThrowsAsync<NetworkStreamClosedException>(
                        () => stream.ProbeConnectionAsync(TimeSpan.FromSeconds(5)))
                    .ConfigureAwait(false);
            }
        }

        [Test]
        public async Task ProbeConnection_WhenServerClosesConnectionWithNotAuthorized()
        {
            var stream = new MockStream()
            {
                ExpectServerCloseCodeOnRead =
                    (WebSocketCloseStatus)SshRelayCloseCode.NOT_AUTHORIZED
            };
            var endpoint = new MockSshRelayEndpoint()
            {
                ExpectedStream = stream
            };

            var relay = new SshRelayStream(endpoint);
            await ExceptionAssert
                .ThrowsAsync<SshRelayDeniedException>(
                    () => relay.ProbeConnectionAsync(TimeSpan.FromSeconds(2)))
                .ConfigureAwait(false);
        }
    }
}
