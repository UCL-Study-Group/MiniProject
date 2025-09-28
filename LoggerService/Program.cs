using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LoggerService;

internal class Program
{
    private const string InvalidExchange = "invalid.exchange";
    private const string InvalidQueue = "invalid.queue";
    
    private static List<object> _log = [];
    
    private static async Task Main()
    {
        Console.WriteLine("LoggerService is starting...");
        
        ConnectionFactory factory = new() { HostName = "localhost" };
        await using var connection = await factory.CreateConnectionAsync();
        
        var channel = await SetupChannelAsync(connection);
        
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (model, ea) =>
        {
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
            var routingKey = ea.RoutingKey;

            var deserialized = JsonSerializer.Deserialize<object>(message);
            
            Console.WriteLine($"Received Invalid Message");
            
            if (deserialized is not null)
                _log.Add(deserialized);
    
            Console.WriteLine("---------------------------");
            
            return Task.CompletedTask;
        };
        
        await channel.BasicConsumeAsync(InvalidQueue, autoAck: true, consumer);
        
        Console.WriteLine("Listening for messages...");
        
        Console.ReadLine();
    }
    
    private static async Task<IChannel> SetupChannelAsync(IConnection connection)
    {
        var channel = await connection.CreateChannelAsync();
        
        await channel.ExchangeDeclareAsync(InvalidExchange, ExchangeType.Direct, durable: false);
        
        await SetupQueuesAsync(channel);
        
        return channel;
    }

    private static async Task SetupQueuesAsync(IChannel channel)
    {
        await channel.QueueDeclareAsync(InvalidQueue, false, false, false);
        
        await channel.QueueBindAsync(InvalidQueue, InvalidExchange, "invalid.message");
    }
}