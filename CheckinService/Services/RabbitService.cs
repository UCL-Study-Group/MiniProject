using RabbitMQ.Client;

namespace CheckinServiceo.Services;

public class RabbitService
{
    private IConnection? _connection;
    private IChannel? _channel;
    
    public async Task InitializeAsync()
    {
        if (_connection is not null && _connection.IsOpen)
            return;

        var factory = new ConnectionFactory()
        {
            HostName = "localhost"
        };

        _connection = await factory.CreateConnectionAsync();
    }
}