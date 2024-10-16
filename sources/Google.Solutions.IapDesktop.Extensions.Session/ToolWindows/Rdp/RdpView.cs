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

using Google.Solutions.Apis.Locator;
using Google.Solutions.Common.Diagnostics;
using Google.Solutions.Common.Linq;
using Google.Solutions.Common.Security;
using Google.Solutions.IapDesktop.Application;
using Google.Solutions.IapDesktop.Application.Host;
using Google.Solutions.IapDesktop.Application.Profile.Settings;
using Google.Solutions.IapDesktop.Application.Theme;
using Google.Solutions.IapDesktop.Application.Windows;
using Google.Solutions.IapDesktop.Application.Windows.Dialog;
using Google.Solutions.IapDesktop.Core.ObjectModel;
using Google.Solutions.IapDesktop.Extensions.Session.Protocol.Rdp;
using Google.Solutions.Mvvm.Binding;
using Google.Solutions.Mvvm.Controls;
using Google.Solutions.Mvvm.Theme;
using Google.Solutions.Settings.Collection;
using Google.Solutions.Terminal.Controls;
using System;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Google.Solutions.IapDesktop.Extensions.Session.ToolWindows.Rdp
{
    [Service]
    public partial class RdpView
        : SessionViewBase, IRdpSession, IView<RdpViewModel>
    {
        /// <summary>
        /// Hotkey to toggle full-screen.
        /// </summary>
        public const Keys ToggleFullScreenHotKey = Keys.Control | Keys.Alt | Keys.F11;

        private readonly IExceptionDialog exceptionDialog;
        private readonly IEventQueue eventService;
        private readonly IControlTheme theme;
        private readonly IRepository<IApplicationSettings> settingsRepository;

        private Bound<RdpViewModel> viewModel;

        // For testing only.
        internal event EventHandler? AuthenticationWarningDisplayed;

        public bool IsClosing { get; private set; } = false;

        private void UpdateLayout()
        {
            if (this.rdpClient == null)
            {
                return;
            }

            //
            // NB. Docking does not work reliably with the OCX, so keep the size
            // in sync programmatically.
            //
            this.rdpClient.Size = this.Size;
        }

        private bool IsRdsSessionHostRedirectionError(Exception e)
        {
            try
            {
                //
                // When connecting to an RDSH in non-admin mode, we might
                // be redirected to a different RDSH. This redirect always
                // fails because it's using the internal IP address, not
                // the tunnel address.
                //
                // The control sets the Server property to the redirect
                // address, and we can use that to detect this situation.
                //
                return e is RdpDisconnectedException disconnected &&
                    disconnected.DisconnectReason == 516 && // Unable to establish a connection
                    this.rdpClient.Server != this.viewModel.Value.Server;
            }
            catch
            {
                return false;
            }
        }

        private async Task ShowErrorAndCloseAsync(string caption, Exception e)
        {
            using (ApplicationTraceSource.Log.TraceMethod().WithParameters(e.Message))
            {
                await this.eventService
                    .PublishAsync(new SessionAbortedEvent(this.Instance, e))
                    .ConfigureAwait(true);

                if (IsRdsSessionHostRedirectionError(e))
                {
                    this.exceptionDialog.Show(
                        this,
                        caption,
                        new RdsRedirectException(
                            "The server initiated a redirect to a different " +
                            "server. IAP Desktop does not support redirects.\n\n" +
                            "To connect to a RD Session Host, change your connection settings " +
                            "to use an 'Admin' session.",
                            e));
                }
                else
                {
                    this.exceptionDialog.Show(this, caption, e);
                }

                Close();
            }
        }

        //---------------------------------------------------------------------
        // Ctor.
        //---------------------------------------------------------------------

        public RdpView(IServiceProvider serviceProvider)
            : base(
                  serviceProvider.GetService<IMainWindow>(),
                  serviceProvider.GetService<ToolWindowStateRepository>(),
                  serviceProvider.GetService<IBindingContext>())
        {
            this.exceptionDialog = serviceProvider.GetService<IExceptionDialog>();
            this.eventService = serviceProvider.GetService<IEventQueue>();
            this.theme = serviceProvider.GetService<IToolWindowTheme>();
            this.settingsRepository = serviceProvider.GetService<IRepository<IApplicationSettings>>();
        }

        //---------------------------------------------------------------------
        // Publics.
        //---------------------------------------------------------------------

        public InstanceLocator Instance => this.viewModel.Value.Instance!;

        public override string Text
        {
            get => this.viewModel.TryGet()?.Instance?.Name ?? "Remote Desktop";
            set { }
        }

        public void Bind(
            RdpViewModel viewModel,
            IBindingContext bindingContext)
        {
            this.viewModel.Value = viewModel;
        }

        public void Connect()
        {
            Debug.Assert(this.rdpClient == null, "Not initialized yet");

            var viewModel = this.viewModel.Value;

            using (ApplicationTraceSource.Log.TraceMethod().WithParameters(
                viewModel.Server,
                viewModel.Port,
                viewModel.Parameters!.ConnectionTimeout))
            {
                //
                // NB. The initialization needs to happen after the pane is shown, otherwise
                // an error happens indicating that the control does not have a Window handle.
                //
                InitializeComponent();
                Debug.Assert(this.rdpClient != null);

                //
                // Because we're not initializing controls in the constructor, the
                // theme isn't applied by default.
                //
                Debug.Assert(this.theme != null || Install.IsExecutingTests);

                SuspendLayout();
                this.theme?.ApplyTo(this);
                UpdateLayout();
                ResumeLayout();

                this.rdpClient!.MainWindow = (Form)this.MainWindow;

                //
                // Basic connection settings.
                //
                this.rdpClient.Server = viewModel.Server;
                this.rdpClient.Domain = viewModel.Credential!.Domain;
                this.rdpClient.Username = viewModel.Credential.User;
                this.rdpClient.ServerPort = viewModel.Port!.Value;
                this.rdpClient.ConnectionTimeout = viewModel.Parameters.ConnectionTimeout;
                this.rdpClient.EnableAdminMode = viewModel.Parameters.SessionType == RdpSessionType.Admin;

                //
                // Connection security settings.
                //
                switch (viewModel.Parameters.AuthenticationLevel)
                {
                    case RdpAuthenticationLevel.NoServerAuthentication:
                        this.rdpClient.ServerAuthenticationLevel = 0;
                        break;

                    case RdpAuthenticationLevel.RequireServerAuthentication:
                        this.rdpClient.ServerAuthenticationLevel = 1;
                        break;

                    case RdpAuthenticationLevel.AttemptServerAuthentication:
                        this.rdpClient.ServerAuthenticationLevel = 2;
                        break;
                }

                switch (viewModel.Parameters.UserAuthenticationBehavior)
                {
                    case RdpAutomaticLogon.Enabled:
                        this.rdpClient.EnableCredentialPrompt = true;
                        this.rdpClient.Password = viewModel.Credential.Password?.AsClearText() ?? string.Empty;
                        break;

                    case RdpAutomaticLogon.Disabled:
                        this.rdpClient.EnableCredentialPrompt = true;
                        // Leave password blank.
                        break;

                    case RdpAutomaticLogon.LegacyAbortOnFailure:
                        this.rdpClient.EnableCredentialPrompt = false;
                        this.rdpClient.Password = viewModel.Credential.Password?.AsClearText() ?? string.Empty;
                        break;
                }

                this.rdpClient.EnableNetworkLevelAuthentication =
                    (viewModel.Parameters.NetworkLevelAuthentication != RdpNetworkLevelAuthentication.Disabled);
                this.rdpClient.EnableRestrictedAdminMode =
                    (viewModel.Parameters.RestrictedAdminMode == RdpRestrictedAdminMode.Enabled);

                //
                // Connection bar settings.
                //
                this.rdpClient.EnableConnectionBar =
                    (viewModel.Parameters.ConnectionBar != RdpConnectionBarState.Off);
                this.rdpClient.EnableConnectionBarMinimizeButton = true;
                this.rdpClient.EnableConnectionBarPin =
                    (viewModel.Parameters.ConnectionBar == RdpConnectionBarState.Pinned);
                this.rdpClient.ConnectionBarText = this.Instance.Name;

                //
                // Local resources settings.
                //
                this.rdpClient.EnableClipboardRedirection =
                    viewModel.Parameters.RedirectClipboard == RdpRedirectClipboard.Enabled;
                this.rdpClient.EnablePrinterRedirection =
                    viewModel.Parameters.RedirectPrinter == RdpRedirectPrinter.Enabled;
                this.rdpClient.EnableSmartCardRedirection =
                    viewModel.Parameters.RedirectSmartCard == RdpRedirectSmartCard.Enabled;
                this.rdpClient.EnablePortRedirection =
                    viewModel.Parameters.RedirectPort == RdpRedirectPort.Enabled;
                this.rdpClient.EnableDriveRedirection =
                    viewModel.Parameters.RedirectDrive == RdpRedirectDrive.Enabled;
                this.rdpClient.EnableDeviceRedirection =
                    viewModel.Parameters.RedirectDevice == RdpRedirectDevice.Enabled;

                switch (viewModel.Parameters.AudioMode)
                {
                    case RdpAudioMode.PlayLocally:
                        this.rdpClient.AudioRedirectionMode = 0;
                        break;
                    case RdpAudioMode.PlayOnServer:
                        this.rdpClient.AudioRedirectionMode = 1;
                        break;
                    case RdpAudioMode.DoNotPlay:
                        this.rdpClient.AudioRedirectionMode = 2;
                        break;
                }

                //
                // Display settings.
                //
                this.rdpClient.EnableDpiScaling =
                    viewModel.Parameters.DpiScaling == RdpDpiScaling.Enabled;
                this.rdpClient.EnableAutoResize =
                    viewModel.Parameters.DesktopSize == RdpDesktopSize.AutoAdjust;

                switch (viewModel.Parameters.ColorDepth)
                {
                    case RdpColorDepth.HighColor:
                        this.rdpClient.ColorDepth = 16;
                        break;
                    case RdpColorDepth.TrueColor:
                        this.rdpClient.ColorDepth = 24;
                        break;
                    case RdpColorDepth.DeepColor:
                        this.rdpClient.ColorDepth = 32;
                        break;
                }

                //
                // Keyboard settings.
                //
                this.rdpClient.KeyboardHookMode =
                    (int)viewModel.Parameters.HookWindowsKeys;

                //
                // Set hotkey to trigger OnFocusReleasedEvent. This should be
                // the same as the main window uses to move the focus to the
                // control.
                //
                this.rdpClient.FocusHotKey = ToggleFocusHotKey;
                this.rdpClient.FullScreenHotKey = ToggleFullScreenHotKey;

                this.rdpClient.EnableWebAuthnRedirection =
                    (viewModel.Parameters.RedirectWebAuthn == RdpRedirectWebAuthn.Enabled);

                this.rdpClient.Connect();
            }
        }

        public bool IsConnected
        {
            get =>
                this.rdpClient.State == RdpClient.ConnectionState.Connected ||
                this.rdpClient.State == RdpClient.ConnectionState.LoggedOn;
        }

        public bool CanEnterFullScreen => this.rdpClient.CanEnterFullScreen;

        //---------------------------------------------------------------------
        // Window events.
        //---------------------------------------------------------------------

        protected override void OnSizeChanged(EventArgs e)
        {
            if (this.rdpClient != null && this.rdpClient.IsFullScreen)
            {
                //
                // Ignore, any attempted size change might
                // just screw up full-screen mode.
                //
                return;
            }

            base.OnSizeChanged(e);

            //
            // Rearrange controls based on new size.
            //
            UpdateLayout();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            //
            // Mark this pane as being in closing state even though it is still
            // visible at this point. The flag ensures that this pane is
            // not considered by TryGetExistingPane anymore.
            //
            this.IsClosing = true;

            _ = this.eventService
                .PublishAsync(new SessionEndedEvent(this.Instance))
                .ContinueWith(_ => { });
        }

        //---------------------------------------------------------------------
        // RDP callbacks.
        //---------------------------------------------------------------------

        private void rdpClient_ConnectionClosed(object sender, RdpClient.ConnectionClosedEventArgs e)
        {
            switch (e.Reason)
            {
                case ClientBase.DisconnectReason.ReconnectInitiatedByUser:
                    //
                    // User initiated a reconnect -- leave everything as is.
                    //
                    break;

                case RdpClient.DisconnectReason.FormClosed:
                    //
                    // User closed the form.
                    //
                    break;

                case RdpClient.DisconnectReason.DisconnectedByUser:
                    //
                    // User-initiated signout.
                    //
                    Close();
                    break;

                default:
                    //
                    // Something else - allow user to reconnect.
                    //
                    break;
            }
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "")]
        private async void rdpClient_ConnectionFailed(object _, ExceptionEventArgs e)
        {
            await ShowErrorAndCloseAsync(
                    "Connect Remote Desktop session failed",
                    e.Exception)
                .ConfigureAwait(true);
        }

        private void rdpClient_StateChanged(object _, System.EventArgs e)
        {
            if (this.rdpClient.State == RdpClient.ConnectionState.Connected)
            {
                _ = this.eventService
                    .PublishAsync(new SessionStartedEvent(this.Instance))
                    .ContinueWith(_ => { });
            }
        }

        private void rdpClient_ServerAuthenticationWarningDisplayed(object _, System.EventArgs e)
        {
            this.AuthenticationWarningDisplayed?.Invoke(this, e);
        }

        //---------------------------------------------------------------------
        // IRemoteDesktopSession.
        //---------------------------------------------------------------------

        public bool TrySetFullscreen(FullScreenMode mode)
        {
            Rectangle? customBounds;
            if (mode == FullScreenMode.SingleScreen)
            {
                //
                // Normal full screen.
                //
                customBounds = null;
            }
            else
            {
                //
                // Use all configured screns.
                //
                // NB. The list of devices might include devices that
                // do not exist anymore. 
                //
                var selectedDevices = (this.settingsRepository.GetSettings()
                    .FullScreenDevices.Value ?? string.Empty)
                        .Split(ApplicationSettingsRepository.FullScreenDevicesSeparator)
                        .ToHashSet();

                var screens = Screen.AllScreens
                    .Where(s => selectedDevices.Contains(s.DeviceName));

                if (!screens.Any())
                {
                    //
                    // Default to all screens.
                    //
                    screens = Screen.AllScreens;
                }

                var r = new Rectangle();
                foreach (var s in screens)
                {
                    r = Rectangle.Union(r, s.Bounds);
                }

                customBounds = r;
            }

            return this.rdpClient.TryEnterFullScreen(customBounds);
        }

        public void ShowSecurityScreen()
        {
            this.rdpClient.ShowSecurityScreen();
        }

        public void ShowTaskManager()
        {
            this.rdpClient.ShowTaskManager();
        }

        public void Logoff()
        {
            this.rdpClient.Logoff();
        }

        public void Reconnect()
        {
            this.rdpClient.Reconnect(); 
        }

        public void SendText(string text)
        {
            this.rdpClient.SendText(text);
        }

        public bool CanTransferFiles
        {
            get => this.viewModel
                .Value
                .Parameters!
                .RedirectClipboard == RdpRedirectClipboard.Enabled;
        }

        public Task DownloadFilesAsync()
        {
            ShowTooltip(
                "Copy and paste files here",
                "Use copy and paste to transfer files between " +
                "your local computer and the VM.");

            return Task.CompletedTask;
        }

        public Task UploadFilesAsync()
        {
            ShowTooltip(
                "Paste files to upload",
                "Use copy and paste to transfer files between " +
                "your local computer and the VM.");

            return Task.CompletedTask;
        }

        //---------------------------------------------------------------------
        // Drag/docking.
        //
        // The RDP control must always have a parent. But when a document is
        // dragged to become a floating window, or when a window is re-docked,
        // then its parent is temporarily set to null.
        // 
        // To "rescue" the RDP control in these situations, we temporarily
        // move the the control to a rescue form when the drag begins, and
        // restore it when it ends.
        //---------------------------------------------------------------------

        private Form? rescueWindow = null;

        protected override Size DefaultFloatWindowClientSize => this.Size;

        protected override void OnDockBegin()
        {
            //
            // NB. It's possible that another rescue operation is still in
            // progress. So don't create a window if there is one already.
            //
            if (this.rescueWindow == null && this.rdpClient != null)
            {
                this.rescueWindow = new Form();
                this.rdpClient.Parent = this.rescueWindow;
            }

            base.OnDockBegin();
        }

        protected override void OnDockEnd()
        {
            if (this.rescueWindow != null && this.rdpClient != null)
            {
                this.rdpClient.Parent = this;
                this.rdpClient.Size = this.Size;
                this.rescueWindow.Close();
                this.rescueWindow = null;
            }

            base.OnDockEnd();
        }


        //---------------------------------------------------------------------
        // Inner classes.
        //---------------------------------------------------------------------

        private class RdsRedirectException : RdpException
        {
            public RdsRedirectException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }
    }
}
