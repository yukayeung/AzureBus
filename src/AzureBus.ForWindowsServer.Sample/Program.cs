﻿namespace AzureBus.Sample
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Reflection;
    
    class Program
    {
        static void Main(string[] args)
        {
            // Create a bus instance.
            IBus bus = AzureCloud
                .ConfigureBus()
                .WithPublishConfiguration((c) => c.WithMessageMetadata((m, configure) => 
                {
                    configure.Add("Message.Activo", ((SampleMessage)m).Activo);
                }))
                ////.WithSubscriptionConfiguration((c) => c.WithTopicByMessageNamespace())
                .CreateBus();

            // Subscribe to messages.
            var autoSubscriber = new AutoSubscriber(bus, "AzureBus.Sample.Console");
            autoSubscriber.Subscribe(Assembly.GetExecutingAssembly());

            var activo = true;
            // Send 100 messages.
            for (int i = 0; i < 10; i++)
            {
                activo = !activo;
                bus.Publish(new SampleMessage(i.ToString(), activo));
            }

            ////// Create a Queue instance
            ////IQueue queue = AzureCloud.CreateQueue();

            ////// Send 100 messages.
            ////for (int i = 0; i < 10; i++)
            ////{
            ////    queue.Send(new SampleMessage(i.ToString()));
            ////}

            ////// Subscribe to queue for messages of type SampleMessage
            ////queue.Subscribe<SampleMessage>((m) => Console.WriteLine(string.Format("Message received from Queue: Value = {0}", m.Value)));

            Console.ReadKey();
        }
    }
}
