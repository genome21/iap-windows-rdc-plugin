﻿//
// Copyright 2020 Google LLC
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

using Google.Solutions.Common.Locator;
using Google.Solutions.IapDesktop.Application.Data;
using Google.Solutions.IapDesktop.Application.ObjectModel;
using Google.Solutions.IapDesktop.Application.Services.Integration;
using Google.Solutions.IapDesktop.Application.Services.ProjectModel;
using Google.Solutions.IapDesktop.Application.Views;
using Google.Solutions.IapDesktop.Application.Views.ProjectExplorer;
using Google.Solutions.IapDesktop.Extensions.Shell.Properties;
using Google.Solutions.IapDesktop.Extensions.Shell.Services.ConnectionSettings;
using Google.Solutions.IapDesktop.Extensions.Shell.Services.Rdp;
using Google.Solutions.IapDesktop.Extensions.Shell.Services.Ssh;
using Google.Solutions.IapDesktop.Extensions.Shell.Views;
using Google.Solutions.IapDesktop.Extensions.Shell.Views.ConnectionSettings;
using Google.Solutions.IapDesktop.Extensions.Shell.Views.Credentials;
using Google.Solutions.IapDesktop.Extensions.Shell.Views.Diagnostics;
using Google.Solutions.IapDesktop.Extensions.Shell.Views.RemoteDesktop;
using Google.Solutions.IapDesktop.Extensions.Shell.Views.SshKeys;
using Google.Solutions.IapDesktop.Extensions.Shell.Views.SshTerminal;
using Google.Solutions.IapDesktop.Extensions.Shell.Views.TunnelsViewer;
using Google.Solutions.Mvvm.Binding.Commands;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Google.Solutions.IapDesktop.Extensions.Shell.Services
{
    /// <summary>
    /// Main class of the extension, instantiated on load.
    /// </summary>
    [Service(ServiceLifetime.Singleton, DelayCreation = false)]
    public class ShellExtension
    {
        private readonly IServiceProvider serviceProvider;
        private readonly IWin32Window window;
        private readonly ICommandContainer<ISession> sessionCommands;

        private static CommandState GetToolbarCommandStateWhenRunningWindowsInstanceRequired(
            IProjectModelNode node)
        {
            return node is IProjectModelInstanceNode vmNode &&
                        vmNode.IsRunning &&
                        vmNode.IsWindowsInstance()
                ? CommandState.Enabled
                : CommandState.Disabled;
        }

        private static CommandState GetContextMenuCommandStateWhenRunningWindowsInstanceRequired(IProjectModelNode node)
        {
            if (node is IProjectModelInstanceNode vmNode && vmNode.IsWindowsInstance())
            {
                return vmNode.IsRunning
                    ? CommandState.Enabled
                    : CommandState.Disabled;
            }
            else
            {
                return CommandState.Unavailable;
            }
        }

        //---------------------------------------------------------------------
        // Commands.
        //---------------------------------------------------------------------

        private async Task GenerateCredentialsAsync(IProjectModelNode node)
        {
            if (node is IProjectModelInstanceNode vmNode)
            {
                Debug.Assert(vmNode.IsWindowsInstance());

                var settingsService = this.serviceProvider
                    .GetService<IConnectionSettingsService>();
                var settings = settingsService.GetConnectionSettings(vmNode);

                await this.serviceProvider.GetService<ICreateCredentialsWorkflow>()
                    .CreateCredentialsAsync(
                        this.window,
                        vmNode.Instance,
                        settings.TypedCollection,
                        false)
                    .ConfigureAwait(true);

                settings.Save();
            }
        }

        private async Task ConnectAsync(
            IProjectModelNode node,
            bool allowPersistentCredentials,
            bool forceNewConnection)
        {
            ISession session = null;
            if (node is IProjectModelInstanceNode rdpNode && rdpNode.IsRdpSupported())
            {
                session = await this.serviceProvider
                    .GetService<IRdpConnectionService>()
                    .ActivateOrConnectInstanceAsync(
                        rdpNode,
                        allowPersistentCredentials)
                    .ConfigureAwait(true);

                Debug.Assert(session != null);
            }
            else if (node is IProjectModelInstanceNode sshNode && sshNode.IsSshSupported())
            {
                if (forceNewConnection)
                {
                    session = await this.serviceProvider
                        .GetService<ISshConnectionService>()
                        .ConnectInstanceAsync(sshNode)
                        .ConfigureAwait(true);
                }
                else
                {
                    session = await this.serviceProvider
                        .GetService<ISshConnectionService>()
                        .ActivateOrConnectInstanceAsync(sshNode)
                        .ConfigureAwait(true);
                }

                Debug.Assert(session != null);
            }

            if (session is SessionViewBase sessionPane &&
                sessionPane.ContextCommands == null)
            {
                //
                // Use commands from Session menu as
                // context menu.
                //
                sessionPane.ContextCommands = this.sessionCommands;
            }
        }

        private async Task DuplicateSessionAsync(ISshTerminalSession session)
        {
            //
            // Try to lookup node for this session. In some cases,
            // we might not find it (for example, if the project has
            // been unloaded in the meantime).
            //
            var node = await this.serviceProvider
                .GetService<IProjectModelService>()
                .GetNodeAsync(session.Instance, CancellationToken.None)
                .ConfigureAwait(true);

            if (node is IProjectModelInstanceNode vmNode && vmNode != null)
            {
                await ConnectAsync(vmNode, false, true)
                    .ConfigureAwait(true);
            }
        }

        //---------------------------------------------------------------------
        // Setup
        //---------------------------------------------------------------------

        public ShellExtension(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;

            var mainForm = serviceProvider.GetService<IMainWindow>();

            //
            // Session menu.
            //
            // On pop-up of the menu, query the active session and use it as context.
            //
            this.sessionCommands = mainForm.AddMenu(
                "&Session", 1,
                () => this.serviceProvider
                    .GetService<IGlobalSessionBroker>()
                    .ActiveSession);

            //
            // Let this extension handle all URL activations.
            //
            var sessionCommands = new SessionCommands();
            var connectCommands = new ConnectCommands(
                serviceProvider.GetService<UrlCommands>(),
                serviceProvider.GetService<Service<IRdpConnectionService>>(),
                serviceProvider.GetService<Service<ISshConnectionService>>(),
                serviceProvider.GetService<Service<IProjectModelService>>(),
                this.sessionCommands);
            Debug.Assert(serviceProvider
                .GetService<UrlCommands>()
                .LaunchRdpUrl.QueryState(new IapRdpUrl(
                    new InstanceLocator("project", "zone", "name"),
                    new NameValueCollection()))
                == CommandState.Enabled,
                "URL command installed");

            this.window = mainForm;

            //
            // Connect.
            //
            var projectExplorer = serviceProvider.GetService<IProjectExplorer>();

            projectExplorer.ContextMenuCommands.AddCommand(
                connectCommands.ContextMenuActivateOrConnectInstance,
                0);
            projectExplorer.ContextMenuCommands.AddCommand(
                connectCommands.ContextMenuConnectRdpAsUser,
                1);
            projectExplorer.ContextMenuCommands.AddCommand(
                connectCommands.ContextMenuConnectSshInNewTerminal,
                2);
            projectExplorer.ToolbarCommands.AddCommand(
                connectCommands.ToolbarActivateOrConnectInstance);

            //
            // Generate credentials (Windows/RDP only).
            //
            projectExplorer.ContextMenuCommands.AddCommand(
                new ContextCommand<IProjectModelNode>(
                    "&Generate Windows logon credentials...",
                    GetContextMenuCommandStateWhenRunningWindowsInstanceRequired,
                    GenerateCredentialsAsync)
                {
                    Image = Resources.AddCredentials_16,
                    ActivityText = "Generating Windows logon credentials"
                },
                3);

            projectExplorer.ToolbarCommands.AddCommand(
                new ContextCommand<IProjectModelNode>(
                    "Generate Windows logon credentials",
                    GetToolbarCommandStateWhenRunningWindowsInstanceRequired,
                    GenerateCredentialsAsync)
                {
                    Image = Resources.AddCredentials_16,
                    ActivityText = "Generating Windows logon credentials"
                });

            //
            // Connection settings.
            //
            var connectionSettingsCommands = serviceProvider.GetService<ConnectionSettingsCommands>();
            projectExplorer.ContextMenuCommands.AddCommand(
                connectionSettingsCommands.ContextMenuOpen,
                4);

            projectExplorer.ToolbarCommands.AddCommand(
                connectionSettingsCommands.ToolbarOpen,
                3);

            //
            // Authorized keys.
            //
            var authorizedKeyCommands = serviceProvider.GetService<AuthorizedPublicKeysCommands>();
            projectExplorer.ContextMenuCommands.AddCommand(
                authorizedKeyCommands.ContextMenuOpen,
                11);
#if DEBUG
            projectExplorer.ContextMenuCommands.AddCommand(
                serviceProvider.GetService<DiagnosticsCommands>().GenerateHtmlPage);
#endif

            //
            // View menu.
            //
            var tunnelsViewCommands = serviceProvider.GetService<TunnelsViewCommands>();
            mainForm.ViewMenu.AddCommand(
                tunnelsViewCommands.WindowMenuOpen,
                1);
            mainForm.ViewMenu.AddCommand(authorizedKeyCommands.WindowMenuOpen);

            //
            // Session menu.
            //

            this.sessionCommands.AddCommand(sessionCommands.EnterFullScreenOnSingleScreen);
            this.sessionCommands.AddCommand(sessionCommands.EnterFullScreenOnAllScreens);
            this.sessionCommands.AddCommand(connectCommands.DuplicateSession);
            this.sessionCommands.AddCommand(sessionCommands.Disconnect);
            this.sessionCommands.AddSeparator();
            this.sessionCommands.AddCommand(sessionCommands.DownloadFiles);
            this.sessionCommands.AddCommand(sessionCommands.ShowSecurityScreen);
            this.sessionCommands.AddCommand(sessionCommands.ShowTaskManager);
        }
    }
}
