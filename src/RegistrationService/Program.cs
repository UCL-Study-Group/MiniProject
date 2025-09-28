using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Registration_Service.Models;

namespace Registration_Service;

internal class Program
{
    private const string RegistrationExchange = "card.exchange";
    private const string RegistrationQueue = "card.register";
    
    private const string InvalidExchange = "invalid.exchange";
    
    private static async Task Main()
    {
        Console.WriteLine("Registration Service is starting...");
        
        var factory = new ConnectionFactory() { HostName = "localhost" };
        
        await using var connection = await factory.CreateConnectionAsync();
        
        var channel = await SetupChannelAsync(connection);
        
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                Console.WriteLine("Received registration");

                HandleEvent(ea);
            }
            catch (Exception ex)
            {
                await HandleInvalidAsync(ea, channel, ex);
                
                Console.WriteLine("Received invalid registration, message logged");
            }
        };
        
        await channel.BasicConsumeAsync(RegistrationQueue, true, consumer);

        while (true)
        {
            var command = Console.ReadLine();
            
            if (!string.IsNullOrEmpty(command) && command == "exit")
                break;

            Console.Read();
        }
    }

    private static async Task<IChannel> SetupChannelAsync(IConnection connection)
    {
        var options = new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true,
            outstandingPublisherConfirmationsRateLimiter: new ThrottlingRateLimiter(50));
        
        var channel = await connection.CreateChannelAsync(options);
        
        await channel.ExchangeDeclareAsync(RegistrationExchange, ExchangeType.Direct, durable: false);
        await channel.ExchangeDeclareAsync(InvalidExchange, ExchangeType.Direct, durable: false);
        
        await SetupQueuesAsync(channel);
        
        return channel;
    }

    private static async Task SetupQueuesAsync(IChannel channel)
    {
        await channel.QueueDeclareAsync(RegistrationQueue, false, false, false);
        
        await channel.QueueBindAsync(RegistrationQueue, RegistrationExchange, "card.register");
    }

    private static void HandleEvent(BasicDeliverEventArgs ea)
    {
        var body = Encoding.UTF8.GetString(ea.Body.ToArray());

        var parsed = JsonSerializer.Deserialize<Registration>(body);

        var bytes = parsed?.GetParsedSerial;
    }

    private static async Task HandleInvalidAsync(BasicDeliverEventArgs ea, IChannel channel, Exception ex)
    {
        var body = new
        {
            TimeStamp = DateTime.Now,
            Service = "Registration Service",
            Reason = ex.Message,
            Body = Encoding.UTF8.GetString(ea.Body.ToArray())
        };
        
        var json = JsonSerializer.Serialize(body);

        await channel.BasicPublishAsync(
            exchange: InvalidExchange, 
            routingKey: "invalid.message", 
            body: Encoding.UTF8.GetBytes(json), 
            mandatory: false);
    }
}