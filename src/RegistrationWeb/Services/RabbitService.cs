using System.Text;
using System.Text.Json;
using FluentResults;
using RabbitMQ.Client;

namespace Registration_Blazor.Services;

public class RabbitService : IAsyncDisposable
{
    private IConnection? _connection;
    private IChannel? _channel;

    private const string CardExchange = "card.exchange";

    private async Task InitializeAsync()
    {
        if (_connection is not null && _connection.IsOpen)
            return;

        try
        {
            var factory = new ConnectionFactory()
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest",
                Port = 5672,
            };

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            await _channel.ExchangeDeclareAsync(CardExchange, ExchangeType.Direct, durable: false);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Countered an exception while trying to establish a connection to RabbitMQ");
        }
    }

    public async Task<Result> PublishMessageAsync(string route, object body)
    {
        try
        {
            await InitializeAsync();

            if (_connection is null || _channel is null)
                return Result.Fail("Couldn't initialize RabbitMQ");

            var encodedBody = EncodeMessage(body);

            await _channel.BasicPublishAsync(CardExchange, route, encodedBody);

            return Result.Ok();
        }
        catch (Exception)
        {
            return Result.Fail("Failed publish message");
        }
    }

    private static byte[] EncodeMessage(object messageToEncode)
    {
        var json = JsonSerializer.Serialize(messageToEncode);
        
        var encoded = Encoding.UTF8.GetBytes(json);
        
        return encoded;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null && _channel is not null)
        {
            await _connection.DisposeAsync();
            await _channel.DisposeAsync();
        }
        
        GC.SuppressFinalize(this);
    }
}