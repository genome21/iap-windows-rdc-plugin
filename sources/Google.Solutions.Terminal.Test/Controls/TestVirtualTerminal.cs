﻿//
// Copyright 2024 Google LLC
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

using Google.Solutions.Platform.IO;
using Google.Solutions.Terminal.Controls;
using Moq;
using Newtonsoft.Json.Converters;
using NUnit.Framework;
using System;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Google.Solutions.Terminal.Test.Controls
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    internal class TestVirtualTerminal
    {
        private class TerminalForm : Form
        {
            public VirtualTerminal VirtualTerminal { get; }

            private TerminalForm()
            {
                this.VirtualTerminal = new VirtualTerminal()
                {
                    Dock = DockStyle.Fill
                };

                this.Controls.Add(this.VirtualTerminal);
            }

            internal static TerminalForm Create()
            {
                var form = new TerminalForm()
                {
                    Size = new Size(800, 600)
                };

                //
                // When run in a headless environment, the terminal
                // might not be initialized automatically, so force
                // initialization if necessary.
                //
                if (!form.VirtualTerminal.TerminalHandleCreated)
                {
                    form.VirtualTerminal.CreateTerminalHandle();
                }

                return form;
            }
        }

        //---------------------------------------------------------------------
        // Resizing.
        //---------------------------------------------------------------------

        [Test]
        public void Size_WhenChanged_ThenUpdatesDimensions()
        {
            using (var form = TerminalForm.Create())
            {
                form.Show();

                var initialDimensions = form.VirtualTerminal.Dimensions;

                Assert.AreNotEqual(0, initialDimensions.Width);
                Assert.AreNotEqual(0, initialDimensions.Height);

                form.Size = new Size(form.Size.Width + 100, form.Size.Height + 100);
                Application.DoEvents();

                Assert.Greater(form.VirtualTerminal.Dimensions.Width, initialDimensions.Width);
                Assert.Greater(form.VirtualTerminal.Dimensions.Height, initialDimensions.Height);

                form.Close();
            }
        }

        //---------------------------------------------------------------------
        // UserInput - key handling.
        //---------------------------------------------------------------------

        [Test]
        public void UserInput_WhenCharKey_ThenTerminalSendsData()
        {
            using (var form = TerminalForm.Create())
            {
                form.Show();

                VirtualTerminalAssert.RaisesUserInputEvent(
                    form.VirtualTerminal,
                    "A",
                    () => form.VirtualTerminal.SimulateKey(Keys.A));

                form.Close();
            }
        }

        [Test]
        public void UserInput_WhenEnterKey_ThenTerminalSendsData()
        {
            using (var form = TerminalForm.Create())
            {
                form.Show();

                VirtualTerminalAssert.RaisesUserInputEvent(
                    form.VirtualTerminal,
                    "\r",
                    () => form.VirtualTerminal.SimulateKey(Keys.Enter));

                form.Close();
            }
        }

        [Test]
        public void UserInput_WhenLeftKey_ThenTerminalSendsData()
        {
            using (var form = TerminalForm.Create())
            {
                form.Show();

                VirtualTerminalAssert.RaisesUserInputEvent(
                    form.VirtualTerminal,
                    "\u001b[D\u001b[D",
                    () => form.VirtualTerminal.SimulateKey(Keys.Left));

                form.Close();
            }
        }

        //---------------------------------------------------------------------
        // Device.
        //---------------------------------------------------------------------

        [Test]
        public void Device_WhenChanged_ThenClearsScreen()
        {
            using (var form = TerminalForm.Create())
            {
                form.VirtualTerminal.Device = new Mock<IPseudoTerminal>().Object;
                form.Show();

                var receivedData = string.Empty;
                form.VirtualTerminal.Output += (_, args) => receivedData += args.Data;

                // Change device.
                form.VirtualTerminal.Device = new Mock<IPseudoTerminal>().Object;

                form.Close();

                StringAssert.Contains("\u001b[2J", receivedData);
            }
        }

        [Test]
        public void Device_WhenChanged_ThenDisposesPreviousBindingAndClearsScreen()
        {
            using (var form = TerminalForm.Create())
            {
                var device = new Mock<IPseudoTerminal>();

                form.VirtualTerminal.Device = device.Object;
                form.Show();

                // Change device.
                form.VirtualTerminal.Device = new Mock<IPseudoTerminal>().Object;

                form.Close();

                device.Verify(d => d.Dispose(), Times.Once);
            }
        }

        [Test]
        public void Device_WhenDisposed_ThenDisposesDevice()
        {
            var device = new Mock<IPseudoTerminal>();

            using (var form = TerminalForm.Create())
            {
                form.VirtualTerminal.Device = device.Object;
                form.Show();
                form.Close();
            }

            device.Verify(d => d.Dispose(), Times.Once);
        }

        //---------------------------------------------------------------------
        // Theme.
        //---------------------------------------------------------------------

        [Test]
        public void ForeColor_WhenChanged_ThenRaisesEvent()
        {
            using (var form = TerminalForm.Create())
            {
                form.Show();

                var themeChangeEventRaised = false;
                form.VirtualTerminal.ThemeChanged += (_, __) => themeChangeEventRaised = true;

                form.VirtualTerminal.ForeColor = Color.Yellow;
                form.Close();

                Assert.IsTrue(themeChangeEventRaised);
            }
        }

        [Test]
        public void BackColor_WhenChanged_ThenRaisesEvent()
        {
            using (var form = TerminalForm.Create())
            {
                form.Show();

                var themeChangeEventRaised = false;
                form.VirtualTerminal.ThemeChanged += (_, __) => themeChangeEventRaised = true;

                form.VirtualTerminal.BackColor = Color.Yellow;
                form.Close();

                Assert.IsTrue(themeChangeEventRaised);
            }
        }

        [Test]
        public void Font_WhenChanged_ThenRaisesEvent()
        {
            using (var form = TerminalForm.Create())
            {
                form.Show();

                var themeChangeEventRaised = false;
                form.VirtualTerminal.ThemeChanged += (_, __) => themeChangeEventRaised = true;

                form.VirtualTerminal.Font = SystemFonts.DialogFont;
                form.Close();

                Assert.IsTrue(themeChangeEventRaised);
            }
        }

        [Test]
        public void SelectionBackColor_WhenChanged_ThenRaisesEvent()
        {
            using (var form = TerminalForm.Create())
            {
                form.Show();

                var themeChangeEventRaised = false;
                form.VirtualTerminal.ThemeChanged += (_, __) => themeChangeEventRaised = true;

                form.VirtualTerminal.SelectionBackColor = Color.AliceBlue;
                form.Close();

                Assert.IsTrue(themeChangeEventRaised);
            }
        }

        [Test]
        public void SelectionBackgroundAlpha_WhenChanged_ThenRaisesEvent()
        {
            using (var form = TerminalForm.Create())
            {
                form.Show();

                var themeChangeEventRaised = false;
                form.VirtualTerminal.ThemeChanged += (_, __) => themeChangeEventRaised = true;

                form.VirtualTerminal.SelectionBackgroundAlpha = .99f;
                form.Close();

                Assert.IsTrue(themeChangeEventRaised);
            }
        }

        [Test]
        public void CaretStyle_WhenChanged_ThenRaisesEvent()
        {
            using (var form = TerminalForm.Create())
            {
                form.Show();

                var themeChangeEventRaised = false;
                form.VirtualTerminal.ThemeChanged += (_, __) => themeChangeEventRaised = true;

                form.VirtualTerminal.Caret = VirtualTerminal.CaretStyle.BlinkingUnderline;
                form.Close();

                Assert.IsTrue(themeChangeEventRaised);
            }
        }

        //---------------------------------------------------------------------
        // Dimensions.
        //---------------------------------------------------------------------

        [Test]
        public void Dimensions()
        {
            using (var form = TerminalForm.Create())
            {
                form.Show();

                Assert.Greater(form.VirtualTerminal.Dimensions.Width, 80);
                Assert.Greater(form.VirtualTerminal.Dimensions.Height, 20);

                form.Close();
            }
        }

        [Test]
        public void Dimensions_WhenMinimized_ThenDimensionsAreKept()
        {
            using (var form = TerminalForm.Create())
            {
                form.Show();

                var dimensions = form.VirtualTerminal.Dimensions;

                form.WindowState = FormWindowState.Minimized;
                Assert.AreEqual(dimensions, form.VirtualTerminal.Dimensions);

                form.WindowState = FormWindowState.Normal;
                Assert.AreEqual(dimensions, form.VirtualTerminal.Dimensions);

                form.Close();
            }
        }

        //---------------------------------------------------------------------
        // SanitizeTextForPasting.
        //---------------------------------------------------------------------

        [Test]
        public void SanitizeTextForPasting_TypographicQuoteConversion()
        {
            var terminal = new VirtualTerminal()
            {
                EnableBracketedPaste = false,
                EnableTypographicQuoteConversion = false,
            };

            Assert.AreEqual(
                "\u00ABThis\u00BB\rand that", 
                terminal.SanitizeTextForPasting("\u00ABThis\u00BB\r\nand that"));

            terminal.EnableTypographicQuoteConversion = true;

            Assert.AreEqual(
                "\"This\"\rand that",
                terminal.SanitizeTextForPasting("\u00ABThis\u00BB\r\nand that"));
        }

        [Test]
        public void SanitizeTextForPasting_BracketedPasting()
        {
            var terminal = new VirtualTerminal()
            {
                EnableBracketedPaste = false,
                EnableTypographicQuoteConversion = false,
            };

            Assert.AreEqual(
                "\u00ABThis\u00BB\rand that",
                terminal.SanitizeTextForPasting("\u00ABThis\u00BB\r\nand that"));

            terminal.EnableBracketedPaste = true;

            Assert.AreEqual(
                "\u001b[200~\u00ABThis\u00BB\rand that\u001b[201~",
                terminal.SanitizeTextForPasting("\u00ABThis\u00BB\r\nand that"));
        }
    }

    public static class VirtualTerminalAssert
    {
        public static void RaisesUserInputEvent(
            VirtualTerminal terminal,
            string expectedData,
            Action action)
        {
            var receiveBuffer = new StringBuilder();
            void AccumulateData(object sender, VirtualTerminalInputEventArgs args)
            {
                receiveBuffer.Append(args.Data);
            }

            terminal.UserInput += AccumulateData;
            action();
            terminal.UserInput -= AccumulateData;

            Assert.AreEqual(expectedData, receiveBuffer.ToString());
        }
    }
}

