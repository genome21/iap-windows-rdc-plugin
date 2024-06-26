﻿//
// Copyright 2022 Google LLC
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

using Google.Solutions.Common.Diagnostics;
using Google.Solutions.Common.Threading;
using Google.Solutions.Ssh.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Solutions.Ssh
{
    public class SshConnection : SshWorkerThread
    {
        private readonly Queue<SendOperation> sendQueue = new Queue<SendOperation>();
        private readonly TaskCompletionSource<int> connectionCompleted
            = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        //
        // List of open channels. Only accessed on worker thread,
        // so no locking required.
        //
        private readonly LinkedList<SshChannelBase> channels
            = new LinkedList<SshChannelBase>();

        public SshConnection(
            IPEndPoint endpoint,
            ISshCredential credential,
            IKeyboardInteractiveHandler keyboardHandler,
            SynchronizationContext callbackContext)
            : base(
                  endpoint,
                  credential,
                  keyboardHandler,
                  callbackContext)
        {
        }

        //---------------------------------------------------------------------
        // Overrides.
        //---------------------------------------------------------------------

        private protected override void OnConnected()
        {
            //
            // Complete task on callback context.
            //
            this.CallbackContext.Post(() => this.connectionCompleted.SetResult(0));
        }

        private protected override void OnConnectionError(Exception exception)
        {
            if (!this.connectionCompleted.Task.IsCompleted)
            {
                //
                // Complete task (with asynchronous continuation).
                //
                this.connectionCompleted.SetException(exception);
            }
        }

        private protected override void OnReadyToSend(Libssh2AuthenticatedSession session)
        {
            lock (this.sendQueue)
            {
                Debug.Assert(this.IsRunningOnWorkerThread);
                Debug.Assert(this.sendQueue.Count > 0);

                var packet = this.sendQueue.Peek();

                //
                // NB. The operation can throw an exception. It is
                // important that we let this exception escape because
                // it might be simply an EAGAIN situation. If the
                // exception is bad, we'll receive an OnSendError 
                // callback later.
                //
                packet.Operation(session);

                //
                // Sending succeeded - complete packet.
                //
                // N.B. Force continuations onto callback
                // continuation context so that we don't block the
                // current worker thread.
                //
                this.sendQueue.Dequeue();

                this.CallbackContext.Post(() => packet.CompletionSource.SetResult(0));

                if (this.sendQueue.Count == 0)
                {
                    //
                    // Do not ask us for more data, we do not have any
                    // right now.
                    //
                    NotifyReadyToSend(false);
                }
            }
        }

        private protected override void OnSendError(Exception exception)
        {
            lock (this.sendQueue)
            {
                Debug.Assert(this.sendQueue.Count > 0);
                this.sendQueue.Dequeue().CompletionSource.SetException(exception);
            }
        }

        private protected override void OnReadyToReceive(Libssh2AuthenticatedSession session)
        {
            foreach (var channel in this.channels)
            {
                channel.OnReceive();
            }
        }

        private protected override void OnReceiveError(Exception exception)
        {
            foreach (var channel in this.channels)
            {
                channel.OnReceiveError(exception);
            }
        }

        private protected override void OnBeforeCloseSession()
        {
            Debug.Assert(this.IsRunningOnWorkerThread);

            //
            // Close all open channels.
            //
            foreach (var channel in this.channels)
            {
                channel.Dispose();
            }
        }

        //---------------------------------------------------------------------
        // Helper methods for channels.
        //---------------------------------------------------------------------

        /// <summary>
        /// Run an sending operation on the worker thread
        /// when the connection is ready to send data.
        /// </summary>
        internal Task RunSendOperationAsync(
            Action<Libssh2AuthenticatedSession> sendOperation)
        {
            if (!this.IsConnected)
            {
                throw new SshException("Connection is closed");
            }

            lock (this.sendQueue)
            {
                var packet = new SendOperation(sendOperation);
                this.sendQueue.Enqueue(packet);

                // 
                // Nofify that we have data and expect a Send()
                // callback.
                //
                NotifyReadyToSend(true);

                //
                // Return a task - it'll be completed once we've
                // actually sent the data.
                //
                return packet.CompletionSource.Task;
            }
        }

        internal async Task<TResult> RunSendOperationAsync<TResult>(
            Func<Libssh2AuthenticatedSession, TResult> sendOperation)
            where TResult : class
        {
            TResult? result = null;

            await RunSendOperationAsync(
                session =>
                {
                    result = sendOperation(session);
                })
                .ConfigureAwait(false);

            Debug.Assert(result != null);
            return result!;
        }

        internal async Task<TResult> RunThrowingOperationAsync<TResult>(
            Func<Libssh2AuthenticatedSession, TResult> sendOperation)
            where TResult : class
        {
            //
            // Some operations (such as SFTP operations) might throw 
            // exceptions, and these need to be passed thru to the caller
            // (as opposed to letting them bubble up to OnReceiveError).
            //
            TResult? result = null;
            Exception? exception = null;

            await RunSendOperationAsync(
                session =>
                {
                    try
                    {
                        result = sendOperation(session);
                    }
                    catch (Exception e)
                    {
                        exception = e;
                    }
                })
                .ConfigureAwait(false);

            if (exception != null)
            {
                SshTraceSource.Log.TraceError(exception);
                throw exception;
            }
            else
            {
                Debug.Assert(result != null);
                return result!;
            }
        }

        //---------------------------------------------------------------------
        // Publics.
        //---------------------------------------------------------------------

        public Task ConnectAsync()
        {
            StartConnection();
            return this.connectionCompleted.Task;
        }

        public async Task<SshShellChannel> OpenShellAsync(
            ITextTerminal terminal,
            TerminalSize initialSize)
        {
            IEnumerable<EnvironmentVariable>? environmentVariables = null;
            if (terminal.Locale != null)
            {
                //
                // Format language so that Linux understands it.
                //
                var languageFormatted = terminal.Locale.Name.Replace('-', '_');
                environmentVariables = new[]
                {
                    //
                    // Try to pass locale - but do not fail the connection if
                    // the server rejects it.
                    //
                    new EnvironmentVariable(
                        "LC_ALL",
                        $"{languageFormatted}.UTF-8",
                        false)
                };
            }

            return await RunSendOperationAsync(
                session =>
                {
                    Debug.Assert(this.IsRunningOnWorkerThread);

                    using (session.Session.AsBlocking())
                    {
                        var nativeChannel = session.OpenShellChannel(
                            LIBSSH2_CHANNEL_EXTENDED_DATA.MERGE,
                            terminal.TerminalType,
                            initialSize.Columns,
                            initialSize.Rows,
                            environmentVariables);

                        var channel = new SshShellChannel(
                            this,
                            nativeChannel,
                            terminal);

                        this.channels.AddLast(channel);

                        return channel;
                    }
                })
                .ConfigureAwait(false);
        }

        public async Task<SshFileSystemChannel> OpenFileSystemAsync()
        {
            return await RunSendOperationAsync(
                session =>
                {
                    Debug.Assert(this.IsRunningOnWorkerThread);

                    using (session.Session.AsBlocking())
                    {
                        var channel = new SshFileSystemChannel(
                            this,
                            session.OpenSftpChannel());

                        this.channels.AddLast(channel);

                        return channel;
                    }
                })
                .ConfigureAwait(false);
        }

        //---------------------------------------------------------------------
        // Inner classes.
        //---------------------------------------------------------------------

        protected internal class SendOperation
        {
            internal readonly TaskCompletionSource<uint> CompletionSource;
            internal readonly Action<Libssh2AuthenticatedSession> Operation;

            internal SendOperation(Action<Libssh2AuthenticatedSession> operation)
            {
                this.Operation = operation;
                this.CompletionSource = new TaskCompletionSource<uint>();
            }
        }
    }
}
