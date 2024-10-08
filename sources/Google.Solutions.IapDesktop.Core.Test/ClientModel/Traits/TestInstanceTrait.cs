﻿//
// Copyright 2023 Google LLC
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

using Google.Solutions.IapDesktop.Core.ClientModel.Traits;
using Google.Solutions.Testing.Apis;
using NUnit.Framework;

namespace Google.Solutions.IapDesktop.Core.Test.ClientModel.Traits
{
    [TestFixture]
    public class TestInstanceTrait : EquatableFixtureBase<InstanceTrait, ITrait>
    {
        protected override InstanceTrait CreateInstance()
        {
            return InstanceTrait.Instance;
        }

        //---------------------------------------------------------------------
        // DisplayName.
        //---------------------------------------------------------------------

        [Test]
        public void DisplayName()
        {
            Assert.AreEqual("isInstance()", InstanceTrait.Instance.DisplayName);
        }

        //---------------------------------------------------------------------
        // ToString.
        //---------------------------------------------------------------------

        [Test]
        public void ToString_ReturnsExpression()
        {
            Assert.AreEqual("isInstance()", InstanceTrait.Instance.ToString());
        }

        //---------------------------------------------------------------------
        // TryParse.
        //---------------------------------------------------------------------

        [Test]
        public void TryParse_WhenExpressionIsNullOrEmpty(
            [Values(" \t", "", null)] string expression)
        {
            Assert.IsFalse(InstanceTrait.TryParse(expression, out var _));
        }

        [Test]
        public void TryParse_WhenExpressionIsValid(
            [Values("isInstance()", " isInstance(  \n) \n\r\t ")] string expression)
        {
            Assert.IsTrue(InstanceTrait.TryParse(expression, out var trait));
            Assert.IsNotNull(trait);
        }
    }
}
