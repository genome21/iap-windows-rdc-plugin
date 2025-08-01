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

using Google.Apis.Compute.v1.Data;
using Google.Solutions.Apis;
using Google.Solutions.Apis.Auth;
using Google.Solutions.Apis.Compute;
using Google.Solutions.Apis.Crm;
using Google.Solutions.Apis.Diagnostics;
using Google.Solutions.Apis.Locator;
using Google.Solutions.Common.Diagnostics;
using Google.Solutions.Common.Linq;
using Google.Solutions.Common.Util;
using Google.Solutions.IapDesktop.Application;
using Google.Solutions.Ssh.Cryptography;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Solutions.IapDesktop.Extensions.Session.Protocol.Ssh
{
    public abstract class ComputeMetadata
    {
        public const string EnableOsLoginFlag = "enable-oslogin";
        public const string EnableOsLoginWithSecurityKeyFlag = "enable-oslogin-sk";
        public const string BlockProjectSshKeysFlag = "block-project-ssh-keys";

        /// <summary>
        /// Indicates if OS Login is enforced for the project or instance.
        /// </summary>
        public abstract bool IsOsLoginEnabled { get; }

        /// <summary>
        /// Indicates if OS Login-SK is enforced for the project or instance.
        /// </summary>
        public abstract bool IsOsLoginWithSecurityKeyEnabled { get; }

        /// <summary>
        /// Indicates if VMs in this project ignore project-level keys.
        /// </summary>
        public abstract bool AreProjectSshKeysBlocked { get; }

        internal static void AddPublicKeyToMetadata(
            Metadata metadata,
            MetadataAuthorizedPublicKey newKey)
        {
            //
            // Merge new key into existing keyset, and take 
            // the opportunity to purge expired keys.
            //
            var newKeySet = MetadataAuthorizedPublicKeySet.FromMetadata(metadata)
                .RemoveExpiredKeys()
                .Add(newKey);
            metadata.Add(MetadataAuthorizedPublicKeySet.MetadataKey, newKeySet.ToString());
        }

        internal static void RemovePublicKeyFromMetadata(
            Metadata metadata,
            MetadataAuthorizedPublicKey key)
        {
            //
            // Merge new key into existing keyset, and take 
            // the opportunity to purge expired keys.
            //
            var newKeySet = MetadataAuthorizedPublicKeySet
                .FromMetadata(metadata)
                .Remove(key);
            metadata.Add(MetadataAuthorizedPublicKeySet.MetadataKey, newKeySet.ToString());
        }

        protected static async Task ModifyMetadataAndHandleErrorsAsync(
            Func<CancellationToken, Task> modifyMetadata,
            CancellationToken token)
        {
            try
            {
                await modifyMetadata(token).ConfigureAwait(false);
            }
            catch (GoogleApiException e) when (e.Error == null || e.Error.Code == 403)
            {
                ApplicationTraceSource.Log.TraceVerbose(
                    "Setting request payload metadata failed with 403: {0} ({1})",
                    e.Message,
                    e.Error?.Errors.EnsureNotNull().Select(er => er.Reason).FirstOrDefault());

                //
                // Setting metadata failed due to lack of permissions. Note that
                // the Error object is not always populated, hence the OR filter.
                //

                throw new SshKeyPushFailedException(
                    "You do not have sufficient permissions to publish an SSH key. " +
                    "You need the 'Service Account User' and " +
                    "'Compute Instance Admin' roles (or equivalent custom roles) " +
                    "to perform this action.",
                    HelpTopics.ManagingMetadataAuthorizedKeys);
            }
            catch (GoogleApiException e) when (e.IsBadRequestCausedByServiceAccountAccessDenied())
            {
                ApplicationTraceSource.Log.TraceVerbose(
                    "Setting request payload metadata failed with 400: {0} ({1})",
                    e.Message,
                    e.Error?.Errors.EnsureNotNull().Select(er => er.Reason).FirstOrDefault());

                //
                // This error happens if the user has the necessary
                // permissions on the VM, but lacks ActAs permission on
                // the associated service account.
                //

                throw new SshKeyPushFailedException(
                    "You do not have sufficient permissions to publish an SSH key. " +
                    "Because this VM instance uses a service account, you also need the " +
                    "'Service Account User' role.",
                    HelpTopics.ManagingMetadataAuthorizedKeys);
            }
        }

        public abstract IEnumerable<MetadataAuthorizedPublicKey> ListAuthorizedKeys(
           KeyAuthorizationMethods allowedMethods);
    }

    public class ProjectMetadata : ComputeMetadata
    {
        private readonly IComputeEngineClient computeClient;
        private readonly Project projectDetails;

        public override bool IsOsLoginEnabled
        {
            get => this.projectDetails.GetFlag(EnableOsLoginFlag) == true;
        }

        public override bool IsOsLoginWithSecurityKeyEnabled
        {
            get => this.projectDetails.GetFlag(EnableOsLoginWithSecurityKeyFlag) == true;
        }

        public override bool AreProjectSshKeysBlocked
        {
            get => this.projectDetails.GetFlag(BlockProjectSshKeysFlag) == true;
        }

        private ProjectMetadata(
            IComputeEngineClient computeClient,
            Project projectDetails)
        {
            this.computeClient = computeClient;
            this.projectDetails = projectDetails;
        }

        /// <summary>
        /// Load and analyze project metadata.
        /// </summary>
        public static async Task<ProjectMetadata> GetAsync(
            IComputeEngineClient computeClient,
            ProjectLocator project,
            CancellationToken token)
        {
            Precondition.ExpectNotNull(computeClient, nameof(computeClient));
            Precondition.ExpectNotNull(project, nameof(project));

            var projectDetails = await computeClient
                .GetProjectAsync(project, token)
                .ConfigureAwait(false);

            return new ProjectMetadata(
                computeClient,
                projectDetails);
        }

        public override IEnumerable<MetadataAuthorizedPublicKey> ListAuthorizedKeys(
            KeyAuthorizationMethods allowedMethods)
        {
            if (allowedMethods.HasFlag(KeyAuthorizationMethods.ProjectMetadata))
            {
                var keySet = MetadataAuthorizedPublicKeySet
                    .FromMetadata(this.projectDetails.CommonInstanceMetadata);

                return (keySet?.Keys).EnsureNotNull();
            }
            else
            {
                return Enumerable.Empty<MetadataAuthorizedPublicKey>();
            }
        }

        public async Task RemoveAuthorizedKeyAsync(
            MetadataAuthorizedPublicKey key,
            CancellationToken cancellationToken)
        {
            await this.computeClient
                .UpdateCommonInstanceMetadataAsync(
                    new ProjectLocator(this.projectDetails.Name),
                    metadata => RemovePublicKeyFromMetadata(metadata, key),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public class InstanceMetadata : ComputeMetadata
    {
        private readonly IComputeEngineClient computeClient;
        private readonly IResourceManagerClient resourceManagerAdapter;

        private readonly InstanceLocator instance;
        private readonly Instance instanceDetails;
        private readonly Project projectDetails;

        private InstanceMetadata(
            IComputeEngineClient computeClient,
            IResourceManagerClient resourceManagerAdapter,
            InstanceLocator instance,
            Instance instanceDetails,
            Project projectDetails)
        {
            this.computeClient = computeClient;
            this.resourceManagerAdapter = resourceManagerAdapter;
            this.instance = instance;
            this.instanceDetails = instanceDetails;
            this.projectDetails = projectDetails;
        }

        /// <summary>
        /// Load and analyze instance metadata.
        /// </summary>
        public static async Task<InstanceMetadata> GetAsync(
            IComputeEngineClient computeClient,
            IResourceManagerClient resourceManagerAdapter,
            InstanceLocator instance,
            CancellationToken token)
        {
            Precondition.ExpectNotNull(computeClient, nameof(computeClient));
            Precondition.ExpectNotNull(resourceManagerAdapter, nameof(resourceManagerAdapter));
            Precondition.ExpectNotNull(instance, nameof(instance));

            //
            // Query metadata for instance and project in parallel.
            //
            var instanceDetailsTask = computeClient
                .GetInstanceAsync(
                    instance,
                    token)
                .ConfigureAwait(false);
            var projectDetailsTask = computeClient
                .GetProjectAsync(
                    instance.Project,
                    token)
                .ConfigureAwait(false);

            return new InstanceMetadata(
                computeClient,
                resourceManagerAdapter,
                instance,
                await instanceDetailsTask,
                await projectDetailsTask);
        }

        public override bool IsOsLoginEnabled
        {
            get => this.instanceDetails.GetFlag(
                this.projectDetails, 
                EnableOsLoginFlag) == true;
        }

        public override bool IsOsLoginWithSecurityKeyEnabled
        {
            get => this.instanceDetails.GetFlag(
                this.projectDetails, 
                EnableOsLoginWithSecurityKeyFlag) == true;
        }

        public override bool AreProjectSshKeysBlocked
        {
            get => this.instanceDetails.GetFlag(
                this.projectDetails, 
                BlockProjectSshKeysFlag) == true;
        }

        private bool IsLegacySshKeyPresent
        {
            get => !string.IsNullOrEmpty(this.instanceDetails
                .Metadata
                .GetValue(MetadataAuthorizedPublicKeySet.LegacyMetadataKey));
        }

        /// <summary>
        /// Unique ID of the instance.
        /// </summary>
        public ulong InstanceId
        {
            get => this.instanceDetails.Id!.Value;
        }

        /// <summary>
        /// Email of attached service account, if any.
        /// </summary>
        public ServiceAccountEmail? AttachedServiceAccount
        {
            get
            {
                if (this.instanceDetails.ServiceAccounts
                    .EnsureNotNull()
                    .FirstOrDefault()?
                    .Email is string email)
                {
                    return new ServiceAccountEmail(email);
                }
                else
                {
                    return null;
                }
            }
        }

        public override IEnumerable<MetadataAuthorizedPublicKey> ListAuthorizedKeys(
            KeyAuthorizationMethods allowedMethods)
        {
            var keys = new List<MetadataAuthorizedPublicKey>();
            if (allowedMethods.HasFlag(KeyAuthorizationMethods.ProjectMetadata))
            {
                keys.AddRange(MetadataAuthorizedPublicKeySet
                    .FromMetadata(this.projectDetails.CommonInstanceMetadata)
                    .Keys);
            }

            if (allowedMethods.HasFlag(KeyAuthorizationMethods.InstanceMetadata))
            {
                keys.AddRange(MetadataAuthorizedPublicKeySet
                    .FromMetadata(this.instanceDetails.Metadata)
                    .Keys);
            }

            return keys;
        }

        public async Task RemoveAuthorizedKeyAsync(
            MetadataAuthorizedPublicKey key,
            KeyAuthorizationMethods allowedMethods,
            CancellationToken cancellationToken)
        {
            if (allowedMethods.HasFlag(KeyAuthorizationMethods.ProjectMetadata))
            {
                await this.computeClient
                    .UpdateCommonInstanceMetadataAsync(
                        this.instance.Project,
                        metadata => RemovePublicKeyFromMetadata(metadata, key),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (allowedMethods.HasFlag(KeyAuthorizationMethods.InstanceMetadata))
            {
                await this.computeClient
                    .UpdateMetadataAsync(
                        this.instance,
                        metadata => RemovePublicKeyFromMetadata(metadata, key),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public async Task<PlatformCredential> AuthorizeKeyAsync(
            IAsymmetricKeySigner key,
            TimeSpan validity,
            string username,
            KeyAuthorizationMethods allowedMethods,
            IAuthorization authorization,
            CancellationToken cancellationToken)
        {
            key.ExpectNotNull(nameof(key));
            username.ExpectNotNull(nameof(username));

            Debug.Assert(!this.IsOsLoginEnabled);

            var instanceMetadata = this.instanceDetails.Metadata;
            var projectMetadata = this.projectDetails.CommonInstanceMetadata;

            //
            // Check if there is a legacy SSH key. If there is one,
            // other keys are ignored.
            //
            // NB. legacy SSH keys were instance-only, so checking
            // the instance metadata is sufficient.
            //
            if (this.IsLegacySshKeyPresent)
            {
                throw new UnsupportedLegacySshKeyEncounteredException(
                    $"Connecting to the VM instance {this.instance.Name} is not supported " +
                    "because the instance uses legacy SSH keys in its metadata (sshKeys)",
                    HelpTopics.ManagingMetadataAuthorizedKeys);
            }

            //
            // There is no legacy key, so we're good to push a new key.
            // 
            // Now figure out which username to use and where to push it.
            //
            var blockProjectSshKeys = this.AreProjectSshKeysBlocked;

            bool useInstanceKeySet;
            if (allowedMethods.HasFlag(KeyAuthorizationMethods.ProjectMetadata) &&
                allowedMethods.HasFlag(KeyAuthorizationMethods.InstanceMetadata))
            {
                //
                // Both allowed - use project metadata unless:
                // - project keys are blocked
                // - we do not have the permission to update project metadata.
                //
                var canUpdateProjectMetadata = await this.resourceManagerAdapter
                    .IsAccessGrantedAsync(
                        this.instance.Project,
                        new[] {
                            Permissions.ComputeProjectsSetCommonInstanceMetadata,
                            Permissions.ServiceAccountsActAs
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                useInstanceKeySet = blockProjectSshKeys || !canUpdateProjectMetadata;
            }
            else if (allowedMethods.HasFlag(KeyAuthorizationMethods.ProjectMetadata))
            {
                //
                // Only project allowed.
                //
                if (blockProjectSshKeys)
                {
                    throw new InvalidOperationException(
                        $"Project {this.instance.ProjectId} does not allow project-level SSH keys");
                }
                else
                {
                    useInstanceKeySet = false;
                }
            }
            else if (allowedMethods.HasFlag(KeyAuthorizationMethods.InstanceMetadata))
            {
                //
                // Only instance allowed.
                //
                useInstanceKeySet = true;
            }
            else
            {
                //
                // Neither project nor instance allowed.
                //
                throw new ArgumentException(
                    "Unrecognized authorization method",
                    nameof(allowedMethods));
            }

            var metadataKey = new ManagedMetadataAuthorizedPublicKey(
                username,
                key.PublicKey.Type,
                Convert.ToBase64String(key.PublicKey.WireFormatValue),
                new ManagedMetadataAuthorizedPublicKey.PublicKeyMetadata(
                    authorization.Session.Username,
                    DateTime.UtcNow.Add(validity)));

            var existingKeySet = MetadataAuthorizedPublicKeySet.FromMetadata(
                useInstanceKeySet
                    ? instanceMetadata
                    : projectMetadata);

            if (existingKeySet
                .RemoveExpiredKeys()
                .Contains(metadataKey))
            {
                //
                // The key is there already, so we are all set.
                //
                ApplicationTraceSource.Log.TraceVerbose(
                    "Existing SSH key found for {0}",
                    username);
            }
            else
            {
                //
                // Key not known yet, so we have to push it to
                // the metadata.
                //
                ApplicationTraceSource.Log.TraceVerbose(
                    "Pushing new SSH key for {0}",
                    username);

                await ModifyMetadataAndHandleErrorsAsync(
                    async token =>
                    {
                        if (useInstanceKeySet)
                        {
                            await this.computeClient
                                .UpdateMetadataAsync(
                                    this.instance,
                                    metadata => AddPublicKeyToMetadata(
                                        metadata, 
                                        metadataKey),
                                    token)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            await this.computeClient
                                .UpdateCommonInstanceMetadataAsync(
                                    this.instance.Project,
                                    metadata => AddPublicKeyToMetadata(
                                        metadata, 
                                        metadataKey),
                                    token)
                               .ConfigureAwait(false);
                        }
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            }

            return new PlatformCredential(
                key,
                useInstanceKeySet
                    ? KeyAuthorizationMethods.InstanceMetadata
                    : KeyAuthorizationMethods.ProjectMetadata,
                username);
        }
    }

    public class UnsupportedLegacySshKeyEncounteredException : Exception, IExceptionWithHelpTopic
    {
        public IHelpTopic Help { get; }

        internal UnsupportedLegacySshKeyEncounteredException(
            string message,
            IHelpTopic helpTopic)
            : base(message)
        {
            this.Help = helpTopic;
        }
    }

    public class SshKeyPushFailedException : Exception, IExceptionWithHelpTopic
    {
        public IHelpTopic Help { get; }

        internal SshKeyPushFailedException(
            string message,
            IHelpTopic helpTopic)
            : base(message)
        {
            this.Help = helpTopic;
        }
    }
}
