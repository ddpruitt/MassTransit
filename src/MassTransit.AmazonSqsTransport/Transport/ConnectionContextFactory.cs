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
namespace MassTransit.AmazonSqsTransport.Transport
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Contexts;
    using GreenPipes;
    using GreenPipes.Agents;
    using Logging;
    using Topology;


    public class ConnectionContextFactory :
        IPipeContextFactory<ConnectionContext>
    {
        static readonly ILog _log = Logger.Get<ConnectionContextFactory>();
        readonly string _description;
        readonly AmazonSqsHostSettings _settings;
        readonly IAmazonSqsHostTopology _topology;

        public ConnectionContextFactory(AmazonSqsHostSettings settings, IAmazonSqsHostTopology topology)
        {
            _settings = settings;
            _topology = topology;

            _description = settings.ToString();
        }

        IPipeContextAgent<ConnectionContext> IPipeContextFactory<ConnectionContext>.CreateContext(ISupervisor supervisor)
        {
            IAsyncPipeContextAgent<ConnectionContext> asyncContext = supervisor.AddAsyncContext<ConnectionContext>();

            var context = Task.Factory.StartNew(() => CreateConnection(asyncContext, supervisor), supervisor.Stopping, TaskCreationOptions.None,
                    TaskScheduler.Default)
                .Unwrap();

            return asyncContext;
        }

        IActivePipeContextAgent<ConnectionContext> IPipeContextFactory<ConnectionContext>.CreateActiveContext(ISupervisor supervisor,
            PipeContextHandle<ConnectionContext> context, CancellationToken cancellationToken)
        {
            return supervisor.AddActiveContext(context, CreateSharedConnection(context.Context, cancellationToken));
        }

        async Task<ConnectionContext> CreateSharedConnection(Task<ConnectionContext> context, CancellationToken cancellationToken)
        {
            var connectionContext = await context.ConfigureAwait(false);

            var sharedConnection = new SharedConnectionContext(connectionContext, cancellationToken);

            return sharedConnection;
        }

        async Task<ConnectionContext> CreateConnection(IAsyncPipeContextAgent<ConnectionContext> asyncContext, IAgent supervisor)
        {
            IConnection connection = null;
            try
            {
                if (supervisor.Stopping.IsCancellationRequested)
                    throw new OperationCanceledException($"The connection is stopping and cannot be used: {_description}");

                connection = _settings.CreateConnection();

                var connectionContext = new AmazonSqsConnectionContext(connection, _settings, _topology, _description, supervisor.Stopped);
                connectionContext.GetOrAddPayload(() => _settings);

                await asyncContext.Created(connectionContext).ConfigureAwait(false);

                return connectionContext;
            }
            catch (OperationCanceledException)
            {
                await asyncContext.CreateCanceled().ConfigureAwait(false);
                throw;
            }
        }
    }
}