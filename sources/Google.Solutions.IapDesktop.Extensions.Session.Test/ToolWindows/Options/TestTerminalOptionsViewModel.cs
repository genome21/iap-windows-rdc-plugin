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

using Google.Solutions.IapDesktop.Extensions.Session.Settings;
using Google.Solutions.IapDesktop.Extensions.Session.ToolWindows.Options;
using Microsoft.Win32;
using NUnit.Framework;
using System.Drawing;
using System.Threading.Tasks;

namespace Google.Solutions.IapDesktop.Extensions.Session.Test.ToolWindows.Options
{
    [TestFixture]
    public class TestTerminalOptionsViewModel
    {
        private const string TestKeyPath = @"Software\Google\__Test";
        private static readonly RegistryKey hkcu = RegistryKey
            .OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);

        private static TerminalSettingsRepository CreateTerminalSettingsRepository()
        {
            hkcu.DeleteSubKeyTree(TestKeyPath, false);
            var baseKey = hkcu.CreateSubKey(TestKeyPath);

            return new TerminalSettingsRepository(baseKey);
        }

        //---------------------------------------------------------------------
        // IsCopyPasteUsingCtrlCAndCtrlVEnabled.
        //---------------------------------------------------------------------

        [Test]
        public void WhenSettingEnabled_ThenIsCopyPasteUsingCtrlCAndCtrlVEnabledIsTrue()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsCopyPasteUsingCtrlCAndCtrlVEnabled.Value = true;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsTrue(viewModel.IsCopyPasteUsingCtrlCAndCtrlVEnabled.Value);
        }

        [Test]
        public void WhenSettingDisabled_ThenIsCopyPasteUsingCtrlCAndCtrlVEnabledIsTrue()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsCopyPasteUsingCtrlCAndCtrlVEnabled.Value = false;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsFalse(viewModel.IsCopyPasteUsingCtrlCAndCtrlVEnabled.Value);
        }

        [Test]
        public async Task WhenDisablingIsCopyPasteUsingCtrlCAndCtrlVEnabled_ThenChangeIsApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsCopyPasteUsingCtrlCAndCtrlVEnabled.Value = true;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);
            viewModel.IsCopyPasteUsingCtrlCAndCtrlVEnabled.Value = false;
            await viewModel.ApplyChangesAsync();

            settings = settingsRepository.GetSettings();
            Assert.IsFalse(settings.IsCopyPasteUsingCtrlCAndCtrlVEnabled.Value);
        }

        [Test]
        public void WhenIsCopyPasteUsingCtrlCAndCtrlVEnabledChanged_ThenIsDirtyIsTrueUntilApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsFalse(viewModel.IsDirty.Value);

            viewModel.IsCopyPasteUsingCtrlCAndCtrlVEnabled.Value =
                !viewModel.IsCopyPasteUsingCtrlCAndCtrlVEnabled.Value;

            Assert.IsTrue(viewModel.IsDirty.Value);
        }


        //---------------------------------------------------------------------
        // IsCopyPasteUsingShiftInsertAndCtrlInsertEnabled.
        //---------------------------------------------------------------------

        [Test]
        public void WhenSettingEnabled_ThenIsCopyPasteUsingShiftInsertAndCtrlInsertEnabledIsTrue()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsCopyPasteUsingShiftInsertAndCtrlInsertEnabled.Value = true;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsTrue(viewModel.IsCopyPasteUsingShiftInsertAndCtrlInsertEnabled.Value);
        }

        [Test]
        public void WhenSettingDisabled_ThenIsCopyPasteUsingShiftInsertAndCtrlInsertEnabledIsTrue()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsCopyPasteUsingShiftInsertAndCtrlInsertEnabled.Value = false;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsFalse(viewModel.IsCopyPasteUsingShiftInsertAndCtrlInsertEnabled.Value);
        }

        [Test]
        public async Task WhenDisablingIsCopyPasteUsingShiftInsertAndCtrlInsertEnabled_ThenChangeIsApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsCopyPasteUsingShiftInsertAndCtrlInsertEnabled.Value = true;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);
            viewModel.IsCopyPasteUsingShiftInsertAndCtrlInsertEnabled.Value = false;
            await viewModel.ApplyChangesAsync();

            settings = settingsRepository.GetSettings();
            Assert.IsFalse(settings.IsCopyPasteUsingShiftInsertAndCtrlInsertEnabled.Value);
        }

        [Test]
        public void WhenIsCopyPasteUsingShiftInsertAndCtrlInsertEnabledChanged_ThenIsDirtyIsTrueUntilApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsFalse(viewModel.IsDirty.Value);

            viewModel.IsCopyPasteUsingShiftInsertAndCtrlInsertEnabled.Value =
                !viewModel.IsCopyPasteUsingShiftInsertAndCtrlInsertEnabled.Value;

            Assert.IsTrue(viewModel.IsDirty.Value);
        }

        //---------------------------------------------------------------------
        // IsSelectAllUsingCtrlAEnabled.
        //---------------------------------------------------------------------

        [Test]
        public void WhenSettingEnabled_ThenIsSelectAllUsingCtrlAEnabledIsTrue()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsSelectAllUsingCtrlAEnabled.Value = true;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsTrue(viewModel.IsSelectAllUsingCtrlAEnabled.Value);
        }

        [Test]
        public void WhenSettingDisabled_ThenIsSelectAllUsingCtrlAEnabledIsTrue()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsSelectAllUsingCtrlAEnabled.Value = false;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsFalse(viewModel.IsSelectAllUsingCtrlAEnabled.Value);
        }

        [Test]
        public async Task WhenDisablingIsSelectAllUsingCtrlAEnabled_ThenChangeIsApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsSelectAllUsingCtrlAEnabled.Value = true;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);
            viewModel.IsSelectAllUsingCtrlAEnabled.Value = false;
            await viewModel.ApplyChangesAsync();

            settings = settingsRepository.GetSettings();
            Assert.IsFalse(settings.IsSelectAllUsingCtrlAEnabled.Value);
        }

        [Test]
        public void WhenIsSelectAllUsingCtrlAEnabledChanged_ThenIsDirtyIsTrueUntilApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsFalse(viewModel.IsDirty.Value);

            viewModel.IsSelectAllUsingCtrlAEnabled.Value =
                !viewModel.IsSelectAllUsingCtrlAEnabled.Value;

            Assert.IsTrue(viewModel.IsDirty.Value);
        }

        //---------------------------------------------------------------------
        // IsSelectUsingShiftArrrowEnabled.
        //---------------------------------------------------------------------

        [Test]
        public void WhenSettingEnabled_ThenIsSelectUsingShiftArrrowEnabledIsTrue()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsSelectUsingShiftArrrowEnabled.Value = true;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsTrue(viewModel.IsSelectUsingShiftArrrowEnabled.Value);
        }

        [Test]
        public void WhenSettingDisabled_ThenIsSelectUsingShiftArrrowEnabledIsTrue()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsSelectUsingShiftArrrowEnabled.Value = false;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsFalse(viewModel.IsSelectUsingShiftArrrowEnabled.Value);
        }

        [Test]
        public async Task WhenDisablingIsSelectUsingShiftArrrowEnabled_ThenChangeIsApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsSelectUsingShiftArrrowEnabled.Value = true;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);
            viewModel.IsSelectUsingShiftArrrowEnabled.Value = false;
            await viewModel.ApplyChangesAsync();

            settings = settingsRepository.GetSettings();
            Assert.IsFalse(settings.IsSelectUsingShiftArrrowEnabled.Value);
        }

        [Test]
        public void WhenIsSelectUsingShiftArrrowEnabledChanged_ThenIsDirtyIsTrueUntilApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsFalse(viewModel.IsDirty.Value);

            viewModel.IsSelectUsingShiftArrrowEnabled.Value =
                !viewModel.IsSelectUsingShiftArrrowEnabled.Value;

            Assert.IsTrue(viewModel.IsDirty.Value);
        }

        //---------------------------------------------------------------------
        // IsQuoteConvertionOnPasteEnabled.
        //---------------------------------------------------------------------

        [Test]
        public void WhenSettingEnabled_ThenIsQuoteConvertionOnPasteEnabledIsTrue()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsQuoteConvertionOnPasteEnabled.Value = true;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsTrue(viewModel.IsQuoteConvertionOnPasteEnabled.Value);
        }

        [Test]
        public void WhenSettingDisabled_ThenIsQuoteConvertionOnPasteEnabledIsTrue()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsQuoteConvertionOnPasteEnabled.Value = false;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsFalse(viewModel.IsQuoteConvertionOnPasteEnabled.Value);
        }

        [Test]
        public async Task WhenDisablingIsQuoteConvertionOnPasteEnabled_ThenChangeIsApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsQuoteConvertionOnPasteEnabled.Value = true;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);
            viewModel.IsQuoteConvertionOnPasteEnabled.Value = false;
            await viewModel.ApplyChangesAsync();

            settings = settingsRepository.GetSettings();
            Assert.IsFalse(settings.IsQuoteConvertionOnPasteEnabled.Value);
        }

        [Test]
        public void WhenIsQuoteConvertionOnPasteEnabledChanged_ThenIsDirtyIsTrueUntilApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsFalse(viewModel.IsDirty.Value);

            viewModel.IsQuoteConvertionOnPasteEnabled.Value =
                !viewModel.IsQuoteConvertionOnPasteEnabled.Value;

            Assert.IsTrue(viewModel.IsDirty.Value);
        }

        //---------------------------------------------------------------------
        // IsScrollingUsingCtrlUpDownEnabled.
        //---------------------------------------------------------------------

        [Test]
        public void WhenSettingEnabled_ThenIsScrollingUsingCtrlUpDownEnabledIsTrue()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsScrollingUsingCtrlUpDownEnabled.Value = true;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsTrue(viewModel.IsScrollingUsingCtrlUpDownEnabled.Value);
        }

        [Test]
        public void WhenSettingDisabled_ThenIsScrollingUsingCtrlUpDownEnabledIsTrue()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsScrollingUsingCtrlUpDownEnabled.Value = false;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsFalse(viewModel.IsScrollingUsingCtrlUpDownEnabled.Value);
        }

        [Test]
        public async Task WhenDisablingIsScrollingUsingCtrlUpDownEnabled_ThenChangeIsApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsScrollingUsingCtrlUpDownEnabled.Value = true;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);
            viewModel.IsScrollingUsingCtrlUpDownEnabled.Value = false;

            await viewModel.ApplyChangesAsync();

            settings = settingsRepository.GetSettings();
            Assert.IsFalse(settings.IsScrollingUsingCtrlUpDownEnabled.Value);
        }

        [Test]
        public void WhenIsScrollingUsingCtrlUpDownEnabledChanged_ThenIsDirtyIsTrueUntilApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsFalse(viewModel.IsDirty.Value);

            viewModel.IsScrollingUsingCtrlUpDownEnabled.Value =
                !viewModel.IsScrollingUsingCtrlUpDownEnabled.Value;

            Assert.IsTrue(viewModel.IsDirty.Value);
        }

        //---------------------------------------------------------------------
        // IsScrollingUsingCtrlHomeEndEnabled.
        //---------------------------------------------------------------------

        [Test]
        public void WhenSettingEnabled_ThenIsScrollingUsingCtrlHomeEndEnabledIsTrue()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsScrollingUsingCtrlHomeEndEnabled.Value = true;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsTrue(viewModel.IsScrollingUsingCtrlHomeEndEnabled.Value);
        }

        [Test]
        public void WhenSettingDisabled_ThenIsScrollingUsingCtrlHomeEndEnabledIsTrue()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsScrollingUsingCtrlHomeEndEnabled.Value = false;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsFalse(viewModel.IsScrollingUsingCtrlHomeEndEnabled.Value);
        }

        [Test]
        public async Task WhenDisablingIsScrollingUsingCtrlHomeEndEnabled_ThenChangeIsApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.IsScrollingUsingCtrlHomeEndEnabled.Value = true;
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);
            viewModel.IsScrollingUsingCtrlHomeEndEnabled.Value = false;
            await viewModel.ApplyChangesAsync();

            settings = settingsRepository.GetSettings();
            Assert.IsFalse(settings.IsScrollingUsingCtrlHomeEndEnabled.Value);
        }

        [Test]
        public void WhenIsScrollingUsingCtrlHomeEndEnabledChanged_ThenIsDirtyIsTrueUntilApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.IsFalse(viewModel.IsDirty.Value);

            viewModel.IsScrollingUsingCtrlHomeEndEnabled.Value =
                !viewModel.IsScrollingUsingCtrlHomeEndEnabled.Value;

            Assert.IsTrue(viewModel.IsDirty.Value);
        }

        //---------------------------------------------------------------------
        // TerminalFont.
        //---------------------------------------------------------------------

        [Test]
        public void WhenSettingPresent_ThenTerminalFontIsSet()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var font = new Font(FontFamily.GenericMonospace, 24.0f);
            var settings = settingsRepository.GetSettings();
            settings.FontFamily.Value = font.Name;
            settings.FontSizeAsDword.Value =
                TerminalSettingsRepository.DwordFromFontSize(font.Size);
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.AreEqual(font.Name, viewModel.TerminalFont.Value.Name);
            Assert.AreEqual(font.Size, viewModel.TerminalFont.Value.Size);
        }

        [Test]
        public async Task WhenFontChanged_ThenChangeIsApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();
            var font = new Font(FontFamily.GenericMonospace, 24.0f);
            var viewModel = new TerminalOptionsViewModel(settingsRepository);
            viewModel.TerminalFont.Value = font;

            Assert.IsTrue(viewModel.IsDirty.Value);
            await viewModel.ApplyChangesAsync();
            Assert.IsFalse(viewModel.IsDirty.Value);

            var settings = settingsRepository.GetSettings();
            Assert.AreEqual(font.Name, settings.FontFamily.Value);
            Assert.AreEqual(
                TerminalSettingsRepository.DwordFromFontSize(font.Size),
                settings.FontSizeAsDword.Value);
        }

        //---------------------------------------------------------------------
        // ForegroundColor.
        //---------------------------------------------------------------------

        [Test]
        public void WhenSettingPresent_ThenForegroundColorIsSet()
        {
            var color = Color.Red;

            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.ForegroundColorArgb.Value = color.ToArgb();
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.AreEqual(color.R, viewModel.TerminalForegroundColor.Value.R);
            Assert.AreEqual(color.G, viewModel.TerminalForegroundColor.Value.G);
            Assert.AreEqual(color.B, viewModel.TerminalForegroundColor.Value.B);
        }

        [Test]
        public async Task WhenForegroundColorChanged_ThenChangeIsApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();

            var color = Color.Yellow;
            var viewModel = new TerminalOptionsViewModel(settingsRepository);
            viewModel.TerminalForegroundColor.Value = color;

            Assert.IsTrue(viewModel.IsDirty.Value);
            await viewModel.ApplyChangesAsync();
            Assert.IsFalse(viewModel.IsDirty.Value);

            var settings = settingsRepository.GetSettings();
            Assert.AreEqual(color.ToArgb(), settings.ForegroundColorArgb.Value);
        }

        //---------------------------------------------------------------------
        // BackgroundColor.
        //---------------------------------------------------------------------

        [Test]
        public void WhenSettingPresent_ThenBackgroundColorIsSet()
        {
            var color = Color.Red;

            var settingsRepository = CreateTerminalSettingsRepository();
            var settings = settingsRepository.GetSettings();
            settings.BackgroundColorArgb.Value = color.ToArgb();
            settingsRepository.SetSettings(settings);

            var viewModel = new TerminalOptionsViewModel(settingsRepository);

            Assert.AreEqual(color.R, viewModel.TerminalBackgroundColor.Value.R);
            Assert.AreEqual(color.G, viewModel.TerminalBackgroundColor.Value.G);
            Assert.AreEqual(color.B, viewModel.TerminalBackgroundColor.Value.B);
        }

        [Test]
        public async Task WhenBackgroundColorChanged_ThenChangeIsApplied()
        {
            var settingsRepository = CreateTerminalSettingsRepository();

            var color = Color.Yellow;
            var viewModel = new TerminalOptionsViewModel(settingsRepository);
            viewModel.TerminalBackgroundColor.Value = color;

            Assert.IsTrue(viewModel.IsDirty.Value);
            await viewModel.ApplyChangesAsync();
            Assert.IsFalse(viewModel.IsDirty.Value);

            var settings = settingsRepository.GetSettings();
            Assert.AreEqual(color.ToArgb(), settings.BackgroundColorArgb.Value);
        }
    }
}