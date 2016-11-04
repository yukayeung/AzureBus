﻿namespace AzureBus.Queue
{
    using System;
    using System.Collections.Concurrent;
    using System.Configuration;
    using System.Linq;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;
    using AzureBus.Queue.Configuration;

    /// <summary>
    /// Microsoft QueueClient Abstraction.
    /// </summary>
    public class AzureQueue : IQueue
    {
        //private readonly string connectionString;
        private readonly NamespaceManager namespaceManager;
        private readonly ConcurrentDictionary<string, QueueClient> queueClients = new ConcurrentDictionary<string, QueueClient>();
        private readonly ConcurrentDictionary<string, QueueClient> subscriptions = new ConcurrentDictionary<string, QueueClient>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Queue" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        internal AzureQueue(IQueueConfiguration configuration)
        {
            this.Configuration = configuration;
            this.namespaceManager = NamespaceManager.CreateFromConnectionString(configuration.ConnectionString);
        }

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <value>
        /// The configuration.
        /// </value>
        public IQueueConfiguration Configuration
        {
            get;
            private set;
        }

        /// <summary>
        /// Sends a message to the Queue.
        /// </summary>
        /// <typeparam name="T">The message type</typeparam>    
        /// <param name="message">The message to publish</param>
        public void Send<T>(T message) where T : class
        {
            string queueName = this.CreateQueueIfNotExists<T>();

            if (!this.queueClients.ContainsKey(queueName))
            {
                this.queueClients.GetOrAdd(queueName, QueueClient.CreateFromConnectionString(this.Configuration.ConnectionString, queueName));
            }

            QueueClient queueClient = this.queueClients[queueName];
            queueClient.Send(new BrokeredMessage(message) { MessageId = message.GetHashCode().ToString() });
        }

        /// <summary>
        /// Subscribes to a stream of messages that match a .NET type.
        /// </summary>
        /// <typeparam name="T">The type to subscribe to</typeparam>        
        /// <param name="onMessage">
        /// The action to run when a message arrives.
        /// </param>
        public void Subscribe<T>(Action<T> onMessage)
        {
            string queueName = this.CreateQueueIfNotExists<T>();

            QueueClient queueClient = QueueClient.CreateFromConnectionString(this.Configuration.ConnectionString, queueName, ReceiveMode.ReceiveAndDelete);

            Queue.SubscriptionConfig.ISubscriptionConfiguration subscriptionConfiguration = this.Configuration.SubscriptionConfiguration();

            OnMessageOptions options = new OnMessageOptions();
            options.AutoComplete = true;
            options.MaxConcurrentCalls = subscriptionConfiguration.MaxConcurrentCalls;

            queueClient.OnMessage(message =>
                {
                    try
                    {
                        this.Configuration.Logger.InfoFormat(
                            "Message was received on Queue {0} with MessageId {1}",
                            queueName,
                            message.MessageId);

                        onMessage.Invoke(message.GetBody<T>());
                    }
                    catch (Exception ex)
                    {
                        this.Configuration.Logger.Fatal(ex);
                    }
                },
                options);

            this.subscriptions.GetOrAdd(queueName, queueClient);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.queueClients.ToList().ForEach((s) => s.Value.Close());
            this.subscriptions.ToList().ForEach((s) => s.Value.Close());
        }

        /// <summary>
        /// Creates the topic if not exists.
        /// </summary>
        /// <typeparam name="T">type of the message</typeparam>
        /// <returns>The queue name</returns>
        private string CreateQueueIfNotExists<T>()
        {
            string queueName = string.Format("{0}", typeof(T).FullName.ToLowerInvariant());
            
            if (!this.namespaceManager.QueueExists(queueName))
            {
                this.namespaceManager.CreateQueue(queueName);
            }

            return queueName;
        }

    }
}
