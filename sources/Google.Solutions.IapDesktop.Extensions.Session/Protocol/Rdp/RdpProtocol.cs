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

using Google.Solutions.Common.Linq;
using Google.Solutions.IapDesktop.Core.ClientModel.Protocol;
using Google.Solutions.IapDesktop.Core.ClientModel.Traits;
using System.Linq;

namespace Google.Solutions.IapDesktop.Extensions.Session.Protocol.Rdp
{
    public class RdpProtocol : IProtocol
    {
        public static RdpProtocol Protocol { get; } = new RdpProtocol();

        private RdpProtocol()
        {
        }

        //---------------------------------------------------------------------
        // IProtocol.
        //---------------------------------------------------------------------

        public string Name => "RDP";

        public bool IsAvailable(IProtocolTarget target)
        {
            return target.Traits
                .EnsureNotNull()
                .Any(t => t is WindowsTrait);
        }

        //---------------------------------------------------------------------
        // Equality.
        //---------------------------------------------------------------------

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as RdpProtocol);
        }

        public bool Equals(IProtocol? other)
        {
            return other is RdpProtocol && other != null;
        }

        public static bool operator ==(RdpProtocol? obj1, RdpProtocol? obj2)
        {
            if (obj1 is null)
            {
                return obj2 is null;
            }

            return obj1.Equals(obj2);
        }

        public static bool operator !=(RdpProtocol? obj1, RdpProtocol? obj2)
        {
            return !(obj1 == obj2);
        }

        //---------------------------------------------------------------------
        // Overrides.
        //---------------------------------------------------------------------

        public override string ToString()
        {
            return this.Name;
        }
    }
}
