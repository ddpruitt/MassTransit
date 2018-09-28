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
namespace MassTransit.ActiveMqTransport.Builders
{
    using System;
    using Configuration;
    using MassTransit.Configurators;
    using Transport;


    public class ActiveMqReceiveEndpointFactory :
        IActiveMqReceiveEndpointFactory
    {
        readonly IActiveMqBusConfiguration _configuration;
        readonly ActiveMqHost _host;

        public ActiveMqReceiveEndpointFactory(IActiveMqBusConfiguration configuration, ActiveMqHost host)
        {
            _host = host;
            _configuration = configuration;
        }

        public void CreateReceiveEndpoint(string queueName, Action<IActiveMqReceiveEndpointConfigurator> configure)
        {
            if (!_configuration.TryGetHost(_host, out var hostConfiguration))
                throw new ConfigurationException("The host was not properly configured");

            var configuration = hostConfiguration.CreateReceiveEndpointConfiguration(queueName);

            configure?.Invoke(configuration.Configurator);

            BusConfigurationResult.CompileResults(configuration.Validate());

            configuration.Build();
        }
    }
}