// Copyright 2007-2018 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
namespace MassTransit.AmazonSqsTransport.Pipeline
{
    using System.Threading;
    using System.Threading.Tasks;
    using Contexts;
    using GreenPipes;


    /// <summary>
    /// Creates a receiving model context using the connection
    /// </summary>
    public class ReceiveModelFilter :
        IFilter<ConnectionContext>
    {
        readonly IAmazonSqsHost _host;
        readonly IPipe<ModelContext> _pipe;

        public ReceiveModelFilter(IPipe<ModelContext> pipe, IAmazonSqsHost host)
        {
            _pipe = pipe;
            _host = host;
        }

        void IProbeSite.Probe(ProbeContext context)
        {
            var scope = context.CreateFilterScope("receiveModel");

            _pipe.Probe(scope);
        }

        async Task IFilter<ConnectionContext>.Send(ConnectionContext context, IPipe<ConnectionContext> next)
        {
            var amazonSqs = await context.CreateAmazonSqs().ConfigureAwait(false);
            var amazonSns = await context.CreateAmazonSns().ConfigureAwait(false);

            var modelContext = new AmazonSqsModelContext(context, amazonSqs, amazonSns, _host, context.CancellationToken);

            try
            {
                await _pipe.Send(modelContext).ConfigureAwait(false);
            }
            finally
            {
                await modelContext.DisposeAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await next.Send(context).ConfigureAwait(false);
        }
    }
}
