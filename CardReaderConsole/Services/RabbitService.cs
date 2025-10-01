using CardReaderConsole.Models;
using FluentResults;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CardReaderConsole.Services
{
    public class RabbitService : IAsyncDisposable
    {
        private IConnection? _connection;
        private IChannel? _channel;
        private AsyncEventingBasicConsumer? _consumer;

        public event EventHandler<ValidationModel> ValidationResponseRecieved;

        private const string ValidationExchange = "validation.exchange";
        private string? replyQueueName = null;

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
            }
            catch (Exception ex)
            {
                Console.WriteLine("Countered an exception while trying to establish a connection to RabbitMQ");
            }
        }

        private async Task SetupValidationExchange()
        {
            await _channel.ExchangeDeclareAsync(ValidationExchange, ExchangeType.Direct, durable: false);
        }

        private async Task SetupValidationResponseQueue()
        {
            if (replyQueueName is null)
            {
                var queueDeclareResult = await _channel.QueueDeclareAsync(durable: false, exclusive: false);
                replyQueueName = queueDeclareResult.QueueName;
            }
            else
            {
                await _channel.QueueDeclareAsync(replyQueueName, durable: false);
            }
        }

        public async Task<Result> PublishMessageAsync(string route, object body)
        {
            try
            {
                await InitializeAsync();
                await SetupValidationExchange();

                if (_connection is null || _channel is null || !_channel.IsOpen)
                    return Result.Fail("Couldn't initialize RabbitMQ");

                var encodedBody = EncodeMessage(body);

                BasicProperties props = new();

                props.CorrelationId = Guid.NewGuid().ToString();
                props.ReplyTo = replyQueueName;

                await _channel.BasicPublishAsync(
                    exchange: ValidationExchange, 
                    routingKey: route, 
                    body: encodedBody, 
                    basicProperties: props,
                    mandatory: false);

                return Result.Ok();
            }
            catch (Exception)
            {
                return Result.Fail("Failed publish message");
            }
        }

        public async Task<Result> StartConsumingValidationResponse()
        {
            await InitializeAsync();
            await SetupValidationResponseQueue();

            if (_consumer is not null && !_consumer.IsRunning)
                return Result.Fail("Consumer is already running");

            if (_consumer is null)
                _consumer = new AsyncEventingBasicConsumer(_channel);

            _consumer.ReceivedAsync += OnValidationResponse;

            await _channel.BasicConsumeAsync(replyQueueName, autoAck: true, _consumer);
            return Result.Ok();
        }

        private async Task OnValidationResponse(object sender, BasicDeliverEventArgs @event)
        {
            try
            {
                var correlationId = @event.BasicProperties.CorrelationId;
                var message = Encoding.UTF8.GetString(@event.Body.ToArray());
                var validation = JsonSerializer.Deserialize<ValidationModel>(message);

                if (validation is not null)
                {
                    ValidationResponseRecieved.Invoke(this, validation);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating inserted card: {ex.Message}");
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
}
