﻿namespace Burrow
{
    /// <summary>
    /// Implement this interface and set it with the <see cref="ITunnel"/>
    /// <para>This can also be used when subscribe to queue, set it to the SubscriptionOption or AsyncSubscriptionOption</para>
    /// </summary>
    public interface IRouteFinder
    {
        /// <summary>
        /// Find the exchange name based on the message type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        string FindExchangeName<T>();
        
        /// <summary>
        /// Find the routing key based on the message type.
        /// </summary>
        /// <typeparam name="T">AKA Topic</typeparam>
        /// <returns></returns>
        string FindRoutingKey<T>();

        /// <summary>
        /// Find the queue name based on the message type and subscription name
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="subscriptionName"></param>
        /// <returns></returns>
        string FindQueueName<T>(string subscriptionName);
    }

    /// <summary>
    /// Implement this interface to provide flexible topic routing key based on the message itself
    /// </summary>
    public interface ITopicExchangeRouteFinder : IRouteFinder
    {
        /// <summary>
        /// Find the routing key based on the message 
        /// </summary>
        /// <typeparam name="T">AKA Topic</typeparam>
        /// <returns></returns>
        string FindRoutingKey<T>(T message);
    }
}
