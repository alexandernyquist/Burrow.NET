﻿using System;
using System.Threading.Tasks;
using Burrow.Internal;
using RabbitMQ.Client;

namespace Burrow.Extras.Internal
{
    internal class PriorityConsumerManager : ConsumerManager
    {
        public PriorityConsumerManager(IRabbitWatcher watcher,
                                       IMessageHandlerFactory messageHandlerFactory,
                                       ISerializer serializer)
            : base(watcher, messageHandlerFactory, serializer)
        {
        }

        public override IBasicConsumer CreateConsumer<T>(IModel channel, string subscriptionName, Action<T> onReceiveMessage)
        {
            var action = CreateJobFactory(onReceiveMessage);
            var messageHandler = MessageHandlerFactory.Create(action);
            var consumer = new PriorityBurrowConsumer(channel, messageHandler, _watcher, true, 1);
            _createdConsumers.Add(consumer);
            return consumer;
        }

        public override IBasicConsumer CreateConsumer<T>(IModel channel, string subscriptionName, Action<T, MessageDeliverEventArgs> onReceiveMessage)
        {
            var action = CreateJobFactory(subscriptionName, onReceiveMessage);
            var messageHandler = MessageHandlerFactory.Create(action);
            var consumer = new PriorityBurrowConsumer(channel, messageHandler, _watcher, false, 1);
            _createdConsumers.Add(consumer);
            return consumer;
        }

        public override IBasicConsumer CreateAsyncConsumer<T>(IModel channel, string subscriptionName, Action<T> onReceiveMessage, ushort? batchSize)
        {
            var action = CreateJobFactory(onReceiveMessage);
            var messageHandler = MessageHandlerFactory.Create(action);
            var consumer = new PriorityBurrowConsumer(channel, messageHandler, _watcher, true, (batchSize > 1 ? batchSize.Value : Global.DefaultConsumerBatchSize));
            _createdConsumers.Add(consumer);
            return consumer;
        }

        public override IBasicConsumer CreateAsyncConsumer<T>(IModel channel, string subscriptionName, Action<T, MessageDeliverEventArgs> onReceiveMessage, ushort? batchSize)
        {
            var action = CreateJobFactory(subscriptionName, onReceiveMessage);
            var messageHandler = MessageHandlerFactory.Create(action);
            var consumer = new PriorityBurrowConsumer(channel, messageHandler, _watcher, false, (batchSize > 1 ? batchSize.Value : Global.DefaultConsumerBatchSize));
            _createdConsumers.Add(consumer);
            return consumer;
        }

        protected override Func<RabbitMQ.Client.Events.BasicDeliverEventArgs, Task> CreateJobFactory<T>(string subscriptionName, Action<T, MessageDeliverEventArgs> onReceiveMessage)
        {
            return eventArgs => Task.Factory.StartNew(() =>
            {
                var priority = PriorityMessageHandler.GetMsgPriority(eventArgs);
                var currentThread = System.Threading.Thread.CurrentThread;
                currentThread.IsBackground = true;
#if DEBUG
                _watcher.DebugFormat("4. A task to execute the provided callback with DTag: {0} by CTag: {1}, Priority {2} has been started using {3}.", 
                                     eventArgs.DeliveryTag, 
                                     eventArgs.ConsumerTag, 
                                     Math.Max(priority, 0), 
                                     currentThread.IsThreadPoolThread ? "ThreadPool" : "dedicated Thread");
#endif                
                CheckMessageType<T>(eventArgs.BasicProperties);
                var message = _serializer.Deserialize<T>(eventArgs.Body);
                onReceiveMessage(message, new MessageDeliverEventArgs
                {
                    ConsumerTag = eventArgs.ConsumerTag,
                    DeliveryTag = eventArgs.DeliveryTag,
                    SubscriptionName = subscriptionName,
                    MessagePriority = (uint)Math.Max(priority, 0)
                });
#if DEBUG
                _watcher.DebugFormat("5. A task to execute the provided callback with DTag: {0} by CTag: {1}, Priority {2} has been finished successfully.", 
                                     eventArgs.DeliveryTag, 
                                     eventArgs.ConsumerTag, 
                                     Math.Max(priority, 0));
#endif
            }, Global.DefaultTaskCreationOptionsProvider());
        }
    }
}
