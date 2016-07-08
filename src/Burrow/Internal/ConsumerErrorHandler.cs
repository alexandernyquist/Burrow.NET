using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Burrow.Internal
{
    /// <summary>
    /// This is a simple error handler that will push the error message to a predefined queue
    /// </summary>
    public class ConsumerErrorHandler : IConsumerErrorHandler, IObserver<ISerializer>
    {
        private readonly Func<string, string> _errorQueue;
        private readonly Func<string, string> _routingKey;

        private readonly IDurableConnection _durableConnection;
        private readonly IRabbitWatcher _watcher;
        private readonly object _channelGate = new object();

        private ISerializer _serializer;

        private readonly string _errorExchange;

        private readonly HashSet<string> _initializedErrorQueues = new HashSet<string>();

        /// <summary>
        /// Initialize an error handler
        /// </summary>
        /// <param name="durableConnection"></param>
        /// <param name="serializer"></param>
        /// <param name="watcher"></param>
        public ConsumerErrorHandler(IDurableConnection durableConnection, ISerializer serializer, IRabbitWatcher watcher)
        {
            if (durableConnection == null)
            {
                throw new ArgumentNullException(nameof(durableConnection));
            }
            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }
            if (watcher == null)
            {
                throw new ArgumentNullException(nameof(watcher));
            }

            _durableConnection = durableConnection;
            _serializer = serializer;
            _watcher = watcher;

            _errorExchange = Global.DefaultErrorExchangeName ?? "Burrow.Exchange.Error";
            _errorQueue = Global.DefaultErrorQueueName ?? (queue => queue + ".errors");
            _routingKey = queueName => queueName;
        }

        private void InitializeErrorExchange(IModel model)
        {
            model.ExchangeDeclare(_errorExchange, ExchangeType.Topic, durable: true);
        }

        private void InitializeAndBindErrorQueue(IModel model, string errorQueueName, string routingKey)
        {
            // We are locked by _channelGate
            if (_initializedErrorQueues.Contains(errorQueueName))
            {
                return;
            }

            model.QueueDeclare(errorQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            model.QueueBind(errorQueueName, _errorExchange, routingKey);
            _initializedErrorQueues.Add(errorQueueName);
        }
        
        protected virtual byte[] CreateErrorMessage(BasicDeliverEventArgs devliverArgs, Exception exception)
        {
            var messageAsString = Encoding.UTF8.GetString(devliverArgs.Body);
            var error = new BurrowError
                            {
                                RoutingKey = devliverArgs.RoutingKey,
                                Exchange = devliverArgs.Exchange,
                                Exception = exception.ToString(),
                                Message = messageAsString,
                                DateTime = DateTime.Now,
                                BasicProperties = new BasicPropertiesWrapper(devliverArgs.BasicProperties)
                            };

            return _serializer.Serialize(error);
        }

        private string CreateConnectionCheckMessage(IDurableConnection durableConnection)
        {
            return
                "Please check connection string and that the RabbitMQ Service is running at the specified endpoint.\n" +
                $"\tHostname: '{durableConnection.HostName}'\n" +
                $"\tVirtualHost: '{durableConnection.VirtualHost}'\n" +
                $"\tUserName: '{durableConnection.UserName}'\n" +
                "Failed to write error message to error queue";
        }

        public void Dispose()
        {
        }

        public virtual void HandleError(string queue, BasicDeliverEventArgs deliverEventArgs, Exception exception)
        {
            var errorQueue = _errorQueue(queue);
            var routingKey = _routingKey(queue);

            try
            {
                using (var model = _durableConnection.CreateChannel())
                {
                    lock (_channelGate)
                    {
                        InitializeErrorExchange(model);
                        InitializeAndBindErrorQueue(model, errorQueue, routingKey);
                    }

                    var messageBody = CreateErrorMessage(deliverEventArgs, exception);
                    var properties = model.CreateBasicProperties();
                    properties.SetPersistent(true);
                    model.BasicPublish(_errorExchange, routingKey, properties, messageBody);
                }
            }
            catch (ConnectFailureException)
            {
                // thrown if the broker is unreachable during initial creation.
                _watcher.ErrorFormat("ConsumerErrorHandler: cannot connect to Broker.\n" + CreateConnectionCheckMessage(_durableConnection));
            }
            catch (BrokerUnreachableException)
            {
                // thrown if the broker is unreachable during initial creation.
                _watcher.ErrorFormat("ConsumerErrorHandler: cannot connect to Broker.\n" + CreateConnectionCheckMessage(_durableConnection));
            }
            catch (OperationInterruptedException interruptedException)
            {
                // thrown if the broker connection is broken during declare or publish.
                _watcher.ErrorFormat(
                    "ConsumerErrorHandler: Broker connection was closed while attempting to publish Error message.\n" +
                    $"Message was: '{interruptedException.Message}'\n" +
                    CreateConnectionCheckMessage(_durableConnection));
            }
            catch (Exception unexpecctedException)
            {
                _watcher.ErrorFormat("ConsumerErrorHandler: Failed to publish error message\nException is:\n" + unexpecctedException);
            }
        }
        
        [ExcludeFromCodeCoverage]
        public void OnNext(ISerializer value)
        {
            _serializer = value;
        }

        [ExcludeFromCodeCoverage]
        public void OnError(Exception error)
        {
        }

        [ExcludeFromCodeCoverage]
        public void OnCompleted()
        {
        }
    }
}
