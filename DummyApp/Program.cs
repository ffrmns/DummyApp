using Autofac;
using MassTransit;

namespace MassTransitVisitorPattern
{
    public interface IMessage
    {
        Task AcceptAsync(IMessageVisitor visitor);
    }

    public interface IMessageVisitor
    {
        Task VisitAsync<T>(T message) where T : IMessage;
    }

    public class MessageProcessor : IMessageVisitor
    {
        private readonly IBus _bus;

        public MessageProcessor(IBus bus)
        {
            _bus = bus;
        }

        public async Task VisitAsync<T>(T message) where T : IMessage
        {
            // Process message logic goes here
            Console.WriteLine($"Processing message of type {message.GetType().Name}");

            // Simulate processing time
            await Task.Delay(1000);

            // Publish processed message
            await _bus.Publish(message);
        }
    }

    public class MessageConsumer : IConsumer<IMessage>
    {
        private readonly IMessageVisitor _visitor;

        public MessageConsumer(IMessageVisitor visitor)
        {
            _visitor = visitor;
        }

        public async Task Consume(ConsumeContext<IMessage> context)
        {
            await context.Message.AcceptAsync(_visitor);
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var builder = new ContainerBuilder();

            builder.RegisterType<MessageConsumer>().As<IConsumer<IMessage>>();

            builder.Register(context =>
            {
                var messageProcessor = context.Resolve<IMessageVisitor>();
                var busControl = Bus.Factory.CreateUsingInMemory(cfg =>
                {
                    cfg.ReceiveEndpoint("message-queue", ep =>
                    {
                        ep.Consumer(() => new MessageConsumer(messageProcessor));
                    });
                });

                return busControl;
            })
            .As<IBusControl>()
            .As<IBus>()
            .SingleInstance();

            builder.RegisterType<MessageProcessor>().As<IMessageVisitor>();

            var container = builder.Build();

            var busControl = container.Resolve<IBusControl>();

            var source = new CancellationTokenSource();
            var cancellationToken = source.Token;

            await busControl.StartAsync(cancellationToken);

            var semaphoreSlim = new SemaphoreSlim(5); // Limit concurrency to 5

            // Simulate sending 10 messages
            for (int i = 0; i < 10; i++)
            {
                await semaphoreSlim.WaitAsync(cancellationToken);

                try
                {
                    // Send message
                    await busControl.Publish<IMessage>(new SampleMessage());

                    // Simulate message interval
                    await Task.Delay(500);
                }
                finally
                {
                    semaphoreSlim.Release();
                }
            }

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();

            await busControl.StopAsync();
        }
    }

    public class SampleMessage : IMessage
    {
        public Task AcceptAsync(IMessageVisitor visitor)
        {
            return visitor.VisitAsync(this);
        }
    }
}
