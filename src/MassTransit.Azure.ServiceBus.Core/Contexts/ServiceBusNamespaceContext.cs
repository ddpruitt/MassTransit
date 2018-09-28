﻿// Copyright 2007-2018 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.Azure.ServiceBus.Core.Contexts
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using GreenPipes;
    using GreenPipes.Payloads;
    using Logging;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Management;
    using Util;


    public class ServiceBusNamespaceContext :
        BasePipeContext,
        NamespaceContext,
        IAsyncDisposable
    {
        static readonly ILog _log = Logger.Get<ServiceBusNamespaceContext>();

        readonly NamespaceManager _namespaceManager;

        public ServiceBusNamespaceContext(NamespaceManager namespaceManager, CancellationToken cancellationToken)
            : base(new PayloadCache(), cancellationToken)
        {
            _namespaceManager = namespaceManager;
        }

        public Task DisposeAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            if (_log.IsDebugEnabled)
                _log.DebugFormat("Closed namespace manager: {0}", _namespaceManager.Address);

            return TaskUtil.Completed;
        }

        public Uri ServiceAddress => _namespaceManager.Address;

        public async Task<QueueDescription> CreateQueue(QueueDescription queueDescription)
        {
            var queueExists = await _namespaceManager.QueueExistsAsync(queueDescription.Path).ConfigureAwait(false);

            if (queueExists)
            {
                queueDescription = await _namespaceManager.GetQueueAsync(queueDescription.Path).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    if (_log.IsDebugEnabled)
                        _log.DebugFormat("Creating queue {0}", queueDescription.Path);

                    queueDescription = await _namespaceManager.CreateQueueAsync(queueDescription).ConfigureAwait(false);
                }
                catch (MessagingEntityAlreadyExistsException)
                {
                    queueDescription = await _namespaceManager.GetQueueAsync(queueDescription.Path).ConfigureAwait(false);
                }
            }

            if (_log.IsDebugEnabled)
                _log.DebugFormat("Queue: {0} ({1})", queueDescription.Path,
                    string.Join(", ", new[]
                    {
                        queueDescription.RequiresDuplicateDetection ? "dupe detect" : "",
                        queueDescription.EnableDeadLetteringOnMessageExpiration ? "dead letter" : "",
                        queueDescription.RequiresSession ? "session" : ""
                    }.Where(x => !string.IsNullOrWhiteSpace(x))));

            return queueDescription;
        }

        public async Task<TopicDescription> CreateTopic(TopicDescription topicDescription)
        {
            var topicExists = await _namespaceManager.TopicExistsAsync(topicDescription.Path).ConfigureAwait(false);

            if (topicExists)
            {
                topicDescription = await _namespaceManager.GetTopicAsync(topicDescription.Path).ConfigureAwait(false);
            }
            else 
            {
                try
                {
                    if (_log.IsDebugEnabled)
                        _log.DebugFormat("Creating topic {0}", topicDescription.Path);

                    topicDescription = await _namespaceManager.CreateTopicAsync(topicDescription).ConfigureAwait(false);
                }
                catch (MessagingEntityAlreadyExistsException)
                {
                    await _namespaceManager.GetTopicAsync(topicDescription.Path).ConfigureAwait(false);
                }
            }

            if (_log.IsDebugEnabled)
                _log.DebugFormat("Topic: {0} ({1})", topicDescription.Path,
                    string.Join(", ", new[]
                    {
                        topicDescription.RequiresDuplicateDetection ? "dupe detect" : ""
                    }.Where(x => !string.IsNullOrWhiteSpace(x))));

            return topicDescription;
        }

        public async Task<SubscriptionDescription> CreateTopicSubscription(SubscriptionDescription description, RuleDescription rule, Filter filter)
        {
            var subscriptionExists = await _namespaceManager.SubscriptionExistsAsync(description.TopicPath, description.SubscriptionName).ConfigureAwait(false);
            SubscriptionDescription subscriptionDescription = null;

            if (subscriptionExists)
            {
                subscriptionDescription = await _namespaceManager.GetSubscriptionAsync(description.TopicPath, description.SubscriptionName).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(description.ForwardTo))
                {
                    if (!string.IsNullOrWhiteSpace(subscriptionDescription.ForwardTo))
                    {
                        if (_log.IsWarnEnabled)
                            _log.WarnFormat("Removing invalid subscription: {0} ({1} -> {2})", subscriptionDescription.SubscriptionName,
                                subscriptionDescription.TopicPath,
                                subscriptionDescription.ForwardTo);

                        await _namespaceManager.DeleteSubscriptionAsync(description.TopicPath, description.SubscriptionName).ConfigureAwait(false);
                    }
                }
                else
                {
                    var forwardTo = subscriptionDescription.ForwardTo;
                    var address = _namespaceManager.Address.ToString();
                    if (forwardTo.StartsWith(address))
                        forwardTo = forwardTo.Substring(address.Length).Trim('/');

                    if (description.ForwardTo.Equals(forwardTo))
                    {
                        if (_log.IsDebugEnabled)
                            _log.DebugFormat("Updating subscription: {0} ({1} -> {2})", subscriptionDescription.SubscriptionName, subscriptionDescription.TopicPath,
                                subscriptionDescription.ForwardTo);

                        await _namespaceManager.UpdateSubscriptionAsync(description).ConfigureAwait(false);
                    }
                    else
                    {
                        if (_log.IsWarnEnabled)
                            _log.WarnFormat("Removing invalid subscription: {0} ({1} -> {2})", subscriptionDescription.SubscriptionName,
                                subscriptionDescription.TopicPath,
                                subscriptionDescription.ForwardTo);

                        await _namespaceManager.DeleteSubscriptionAsync(description.TopicPath, description.SubscriptionName).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                try
                {
                    if (_log.IsDebugEnabled)
                        _log.DebugFormat("Creating subscription {0} -> {1}", description.TopicPath, description.ForwardTo);

                    subscriptionDescription = rule != null
                        ? await _namespaceManager.CreateSubscriptionAsync(description, rule).ConfigureAwait(false)
                        : filter != null
                            ? await _namespaceManager.CreateSubscriptionAsync(description, filter).ConfigureAwait(false)
                            : await _namespaceManager.CreateSubscriptionAsync(description).ConfigureAwait(false);
                }
                catch (MessagingEntityAlreadyExistsException)
                {
                    subscriptionDescription = await _namespaceManager.GetSubscriptionAsync(description.TopicPath, description.SubscriptionName).ConfigureAwait(false);
                }
            }

            if (_log.IsDebugEnabled)
                _log.DebugFormat("Subscription: {0} ({1} -> {2})", subscriptionDescription.SubscriptionName, subscriptionDescription.TopicPath,
                    subscriptionDescription.ForwardTo);

            return subscriptionDescription;
        }

        public async Task DeleteTopicSubscription(SubscriptionDescription description)
        {
            try
            {
                await _namespaceManager.DeleteSubscriptionAsync(description.TopicPath, description.SubscriptionName).ConfigureAwait(false);
            }
            catch (MessagingEntityNotFoundException)
            {
            }

            if (_log.IsDebugEnabled)
                _log.DebugFormat("Subscription Deleted: {0} ({1} -> {2})", description.SubscriptionName, description.TopicPath, description.ForwardTo);
        }
    }
}