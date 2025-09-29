using CardReaderConsole.Models;
using CardReaderConsole.Services;
using NFCServiceLibrary.Models;
using NFCServiceLibrary.Services;
using NFCServiceLibrary.Services.Interfaces;

namespace CardReaderConsole
{
    internal class Program
    {
        private static INFCReaderService _nfcService;
        private static RabbitService _rabbitService;

        private static readonly ConsoleColor _originalColor = Console.BackgroundColor;

        static async Task Main(string[] args)
        {
            _nfcService = new NFCReaderService();
            _rabbitService = new RabbitService();

            _nfcService.CardDetected += OnCardDetected;
            _nfcService.ErrorOccurred += OnErrorOccurred;
            _rabbitService.ValidationResponseRecieved += OnValidationRecieved;

            await _rabbitService.StartConsumingValidationResponse();

            if (!_nfcService.StartMonitoring())
            {
                Console.WriteLine("Failed to start the NFC monitoring.");
            }

            Console.ReadLine();

            _nfcService.StopMonitoring();
            _nfcService.Dispose();
        }

        private static void OnCardDetected(object sender, CardDetectedEventArgs e)
        {
            _rabbitService.PublishMessageAsync("card.validate", e);
        }

        private static void OnErrorOccurred(object sender, string errorMessage)
        {
            Console.WriteLine($"Error: {errorMessage}");
        }

        private static void OnValidationRecieved(object sender, ValidationModel validation)
        {
            ConsoleColor eventColor = validation.ValidationStatus switch
            {
                "Valid" => ConsoleColor.Green,
                "Invalid" => ConsoleColor.Red,
                _ => _originalColor
            };

            Console.BackgroundColor = eventColor;
            Console.Clear();
            System.Threading.Thread.Sleep(1000);
            Console.BackgroundColor = _originalColor;
            Console.Clear();
        }
    }
}
