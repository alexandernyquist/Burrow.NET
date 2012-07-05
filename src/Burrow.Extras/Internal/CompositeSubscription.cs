﻿using System;
using System.Collections.Generic;

namespace Burrow.Extras.Internal
{
    /// <summary>
    /// A composite subscription that contains others child Subscription object
    /// This is a result of subscribe to priority queues without auto ack, use the instance of this class to ack messages later
    /// </summary>
    public class CompositeSubscription
    {
        private readonly Dictionary<string, Subscription>  _internalCache = new Dictionary<string, Subscription>();

        //NOTE: To allow mock this class
        protected internal CompositeSubscription()
        {            
        }

        //NOTE: To allow call this method out side this library such as mocking
        protected internal void AddSubscription(Subscription subscription)
        {
            if (subscription == null)
            {
                throw new ArgumentNullException("subscription");
            }
            _internalCache[subscription.ConsumerTag] = subscription;
        }

        public int Count
        {
            get { return _internalCache.Count; }
        }

        public Subscription GetByConsumerTag(string consumerTag)
        {
            if (_internalCache.ContainsKey(consumerTag))
            {
                return _internalCache[consumerTag];
            }
            return null;
        }

        #region -- http://www.rabbitmq.com/amqp-0-9-1-reference.html#basic.ack.multiple --
        public void Ack(string consumerTag, ulong deliveryTag)
        {
            TryAckOrNAck(consumerTag,  x => x.Ack(deliveryTag));
        }

        public void Ack(string consumerTag, IEnumerable<ulong> deliveryTags)
        {
            TryAckOrNAck(consumerTag, x => x.Ack(deliveryTags));
        }

        public void AckAllOutstandingMessages(string consumerTag)
        {
            TryAckOrNAck(consumerTag, x => x.AckAllOutstandingMessages());
        }

        public void Nack(string consumerTag, ulong deliveryTag, bool requeue)
        {
            TryAckOrNAck(consumerTag, x => x.Nack(deliveryTag, requeue));
        }

        public void Nack(string consumerTag, IEnumerable<ulong> deliveryTags, bool requeue)
        {
            TryAckOrNAck(consumerTag, x => x.Nack(deliveryTags, requeue));
        }

        public void NackAllOutstandingMessages(string consumerTag, bool requeue)
        {
            TryAckOrNAck(consumerTag, x => x.NackAllOutstandingMessages(requeue));
        }

        private void TryAckOrNAck(string consumerTag, Action<Subscription> action)
        {
            var sub = GetByConsumerTag(consumerTag);
            if (sub == null)
            {
                throw new SubscriptionNotFoundException(consumerTag, string.Format("Subscription {0} not found, this problem could happen after a retry for new connection. You properly just ignore the old objects you're trying to ack/nack", consumerTag));
            }
            action(sub);
        }

        #endregion
    }
}
