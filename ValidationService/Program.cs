using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Registration_Service.Models;
using System;
using System.Text;
using System.Text.Json;
using ValidationService.Models;

namespace ValidationService;

internal class Program
{
  //Queues og exchanges
  private const string RegistrationExchange = "card.exchange"; //hvor du skal consume registrations
  private const string RegistrationQueue = "card.register"; //hvor du skal consume registrations

  private const string ValidationQueue = "validation.response"; //hvor du skal publish valideringen, invalid eller valid

  private const string ValidationExchange = "validation.exchange"; //hvor du skal publish valideringen, invalid eller valid

  private const string InvalidExchange = "invalid.exchange"; //til logging hvis noget går galt

  //Lister
  static new List<string> RegisteredIds = new();
  static new List<string> CheckedIns = new();


  private static async Task Main()
  {
    Console.WriteLine("Validation Service is starting...");

    var factory = new ConnectionFactory() { HostName = "localhost" };

    await using var connection = await factory.CreateConnectionAsync();

    var channel = await connection.CreateChannelAsync();

    //Declare, sørg for at de eksisterer

    await channel.ExchangeDeclareAsync(RegistrationExchange, ExchangeType.Direct, durable: false);
    await channel.ExchangeDeclareAsync(ValidationExchange, ExchangeType.Direct, durable: false);

    //Bind queues 
    await channel.QueueDeclareAsync(RegistrationQueue, false, false, false);
    await channel.QueueBindAsync(RegistrationQueue, RegistrationExchange, "card.register");

    await channel.QueueDeclareAsync(ValidationQueue, durable: false, exclusive: false);
    await channel.QueueBindAsync(ValidationQueue, ValidationExchange, "validation.response");

    //Create consumers

    var registrationConsumer = new AsyncEventingBasicConsumer(channel);

    var validationConsumer = new AsyncEventingBasicConsumer(channel);

        registrationConsumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {

                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                var registration = JsonSerializer.Deserialize<Registration>(body);


                RegisteredIds.Add(registration.Serial);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when receiving registration: {ex.Message}");
                await HandleInvalidAsync(ea, channel, ex);
            }
        };

      validationConsumer.ReceivedAsync += async (model, ea) =>
      {
        try
        {

          var body = Encoding.UTF8.GetString(ea.Body.ToArray());

          //Bruges til at sende det tilbage til den rigtige "ejermand" af response
          var replyProps = new BasicProperties()
          {
            CorrelationId = ea.BasicProperties.CorrelationId,
            ReplyTo = ea.BasicProperties.ReplyTo
          };

          var validationRequet = JsonSerializer.Deserialize<Validation>(body);

          string idToCheck = validationRequet.CardID;

          var validationResponse = new Validation();

          if (!RegisteredIds.Contains(idToCheck))
          {
            Console.WriteLine($"Did not find {idToCheck} in registration.");

            validationResponse.CardID = idToCheck;
            validationResponse.ValidatedTime = DateTime.Now;
            validationResponse.ValidationStatus = "Invalid";

            await PublishResponse(validationResponse, channel, replyProps);

          }
          else
          {
            Console.WriteLine($"Found {idToCheck} in registration. They have now checked in.");

            //Fjerner det så det ikke kan tjekke ind igen
            RegisteredIds.Remove(idToCheck);

            CheckedIns.Add(idToCheck);

            validationResponse.CardID = idToCheck;
            validationResponse.ValidatedTime = DateTime.Now;
            validationResponse.ValidationStatus = "Valid";
            await PublishResponse(validationResponse, channel, replyProps);
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Something went wrong when validating id: {ex.Message}");
          await HandleInvalidAsync(ea, channel, ex);
        }
      };

      await channel.BasicConsumeAsync(ValidationQueue, autoAck: true, validationConsumer);

      await channel.BasicConsumeAsync(RegistrationQueue, autoAck: true, registrationConsumer);

        Console.ReadLine();
    }

  

  private static async Task PublishResponse(Validation validation, IChannel channel, BasicProperties props)
  {
    try
    {
      //Serialiser 
      var json = JsonSerializer.Serialize(validation);
      var body = Encoding.UTF8.GetBytes(json);

      //Publish
      await channel.BasicPublishAsync(
      exchange: "",
      routingKey: props.ReplyTo,
      basicProperties: props,
      body: body,
      mandatory: false);

    }
    catch (Exception ex)
    {
      Console.WriteLine($"Something went wrong when publishing validation response: {ex.Message}");
    }


  }

  /// <summary>
  /// Poster til LoggingService
  /// </summary>
  /// <param name="ea"> beskeden</param>
  /// <param name="channel"> channelen </param>
  /// <param name="ex"> exception </param>
  private static async Task HandleInvalidAsync(BasicDeliverEventArgs ea, IChannel channel, Exception ex)
  {
    var body = new
    {
      TimeStamp = DateTime.Now,
      Service = "Validation Service",
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

