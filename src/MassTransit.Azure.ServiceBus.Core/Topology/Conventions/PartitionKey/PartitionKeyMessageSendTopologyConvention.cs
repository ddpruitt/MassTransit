// Copyright 2007-2017 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
namespace MassTransit.Azure.ServiceBus.Core.Topology.Conventions.PartitionKey
{
    using MassTransit.Topology;


    public class PartitionKeyMessageSendTopologyConvention<TMessage> :
        IPartitionKeyMessageSendTopologyConvention<TMessage>
        where TMessage : class
    {
        IMessagePartitionKeyFormatter<TMessage> _formatter;

        public PartitionKeyMessageSendTopologyConvention(IPartitionKeyFormatter formatter)
        {
            if (formatter != null)
                SetFormatter(formatter);
        }

        public bool TryGetMessageSendTopology(out IMessageSendTopology<TMessage> messageSendTopology)
        {
            if (_formatter != null)
            {
                messageSendTopology = new SetPartitionKeyMessageSendTopology<TMessage>(_formatter);
                return true;
            }

            messageSendTopology = null;
            return false;
        }

        public bool TryGetMessageSendTopologyConvention<T>(out IMessageSendTopologyConvention<T> convention) where T : class
        {
            convention = this as IMessageSendTopologyConvention<T>;

            return convention != null;
        }

        public void SetFormatter(IPartitionKeyFormatter formatter)
        {
            _formatter = new MessagePartitionKeyFormatter<TMessage>(formatter);
        }

        public void SetFormatter(IMessagePartitionKeyFormatter<TMessage> formatter)
        {
            _formatter = formatter;
        }
    }
}