namespace MsgPulse.Services;

using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExt
{
    extension(IServiceCollection collection)
    {
        public IServiceCollection AddMessageScheduler()
        {
            collection.AddSingleton<MessageScheduler>();
            return collection;
        }

        public IServiceCollection AddMessageProcessor()
        {
            collection.AddSingleton<MessageProcessor>();
            return collection;
        }

        public IServiceCollection AddDynamicEvents()
        {
            collection.AddSingleton<DynamicEvents>();
            return collection;
        }

        public IServiceCollection AddCustomCommands()
        {
            collection.AddSingleton<CustomCommandsHandler>();
            return collection;
        }

        public IServiceCollection AddDeadShowImage()
        {
            collection.AddSingleton<DeadShowImage>();
            return collection;
        }
    }
}
