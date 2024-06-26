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

using System;

namespace Google.Solutions.Common.Runtime
{
    /// <summary>
    /// Factory for instances of type T.
    /// </summary>
    public static class InstanceActivator
    {
        /// <summary>
        /// Create activator that returns an existing instance.
        /// </summary>
        public static IActivator<T> Create<T>(T instance)
        {
            return new Activator<T>(() => instance);
        }

        /// <summary>
        /// Create an activator that invokes a callback.
        /// </summary>
        public static IActivator<T> Create<T>(Func<T> createInstance)
        {
            return new Activator<T>(createInstance);
        }

        private class Activator<T> : IActivator<T>
        {
            private readonly Func<T> createInstance;

            public Activator(Func<T> createInstance)
            {
                this.createInstance = createInstance;
            }

            public T Activate()
            {
                return this.createInstance();
            }
        }
    }
}
