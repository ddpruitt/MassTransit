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
namespace MassTransit.AmazonSqsTransport.Contexts
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using GreenPipes;
    using GreenPipes.Payloads;
    using Logging;
    using Pipeline;
    using Topology;
    using Util;


    public class AmazonSqsModelContext :
        BasePipeContext,
        ModelContext,
        IAsyncDisposable
    {
        static readonly ILog _log = Logger.Get<AmazonSqsModelContext>();

        readonly ConnectionContext _connectionContext;
        readonly IAmazonSqsHost _host;
        readonly IAmazonSQS _amazonSqs;
        readonly IAmazonSimpleNotificationService _amazonSns;
        readonly LimitedConcurrencyLevelTaskScheduler _taskScheduler;

        public AmazonSqsModelContext(ConnectionContext connectionContext, IAmazonSQS amazonSqs, IAmazonSimpleNotificationService amazonSns, IAmazonSqsHost host, CancellationToken cancellationToken)
            : base(new PayloadCacheScope(connectionContext), cancellationToken)
        {
            _connectionContext = connectionContext;
            _amazonSqs = amazonSqs;
            _amazonSns = amazonSns;
            _host = host;

            _taskScheduler = new LimitedConcurrencyLevelTaskScheduler(1);
        }

        public Task DisposeAsync(CancellationToken cancellationToken)
        {
            if (_log.IsDebugEnabled)
                _log.DebugFormat("Closing model: {0}", _connectionContext.Description);

            _amazonSqs?.Dispose();
            _amazonSns?.Dispose();

            return GreenPipes.Util.TaskUtil.Completed;
        }

        IAmazonSqsPublishTopology ModelContext.PublishTopology => _host.Topology.PublishTopology;

        ConnectionContext ModelContext.ConnectionContext => _connectionContext;

        public async Task<string> GetTopic(string topicName)
        {
            var response = await _amazonSns.CreateTopicAsync(topicName).ConfigureAwait(false);

            await Task.Delay(500).ConfigureAwait(false);

            return response.TopicArn;
        }

        public async Task<string> GetQueue(string queueName)
        {
            var response = await _amazonSqs.CreateQueueAsync(queueName).ConfigureAwait(false);

            await Task.Delay(500).ConfigureAwait(false);

            return response.QueueUrl;
        }

        public async Task GetTopicSubscription(string topicName, string queueName)
        {
            var topicArn = await GetTopic(topicName).ConfigureAwait(false);
            var queueUrl = await GetQueue(queueName).ConfigureAwait(false);

            await _amazonSns.SubscribeQueueAsync(topicArn, _amazonSqs, queueUrl).ConfigureAwait(false);

            await Task.Delay(200).ConfigureAwait(false);
        }

        public async Task DeleteTopic(string topicName)
        {
            var topicArn = await GetTopic(topicName).ConfigureAwait(false);
            await _amazonSns.DeleteTopicAsync(topicArn).ConfigureAwait(false);
        }

        public async Task DeleteQueue(string queueName)
        {
            var queueUrl = await GetQueue(queueName).ConfigureAwait(false);
            await _amazonSqs.DeleteQueueAsync(queueUrl).ConfigureAwait(false);
        }

        public Task BasicConsume(string queueUrl, ReceiveSettings receiveSettings, IBasicConsumer consumer)
        {
            return Task.Factory.StartNew(async () =>
            {
                while (!CancellationToken.IsCancellationRequested)
                {
                    var request = new ReceiveMessageRequest(queueUrl)
                    {
                        MaxNumberOfMessages = receiveSettings.PrefetchCount,
                        WaitTimeSeconds = receiveSettings.WaitTimeSeconds,
                        AttributeNames = new List<string> {"All"},
                        MessageAttributeNames = new List<string> {"All"}
                    };

                    var response = await _amazonSqs.ReceiveMessageAsync(request, CancellationToken).ConfigureAwait(false);

                    await Task.WhenAll(response.Messages.Select(consumer.HandleMessage)).ConfigureAwait(false);
                }
            }, CancellationToken, TaskCreationOptions.None, _taskScheduler);
        }

        public PublishRequest CreateTransportMessage(string topicArn, byte[] body)
        {
            var message = Encoding.UTF8.GetString(body);

            return new PublishRequest(topicArn, message);
        }

        public Task Publish(PublishRequest request, CancellationToken cancellationToken)
        {
            return _amazonSns.PublishAsync(request, cancellationToken);
        }

        public Task Send(SendMessageRequest request, CancellationToken cancellationToken)
        {
            return _amazonSqs.SendMessageAsync(request, cancellationToken);
        }

        public Task DeleteMessage(string queueUrl, string receiptHandle, CancellationToken cancellationToken = default(CancellationToken))
        {
            return _amazonSqs.DeleteMessageAsync(queueUrl, receiptHandle, cancellationToken);
        }
    }
}
