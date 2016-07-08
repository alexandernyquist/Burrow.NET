using System;
using RabbitMQ.Client.Events;

namespace Burrow
{
    public interface IConsumerErrorHandler : IDisposable
    {
        /// <summary>
        /// Provide an error handling strategy when there is an error processing the message
        /// </summary>
        /// <param name="queue">The queue the message was retreived from.</param>
        /// <param name="deliverEventArgs"></param>
        /// <param name="exception"></param>
        void HandleError(string queue, BasicDeliverEventArgs deliverEventArgs, Exception exception);
    }
}
