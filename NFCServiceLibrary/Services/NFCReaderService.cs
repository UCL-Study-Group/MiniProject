using NFCServiceLibrary.Models;
using NFCServiceLibrary.Services.Interfaces;
using PCSC;
using PCSC.Exceptions;
using PCSC.Monitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NFCServiceLibrary.Services
{
    public class NFCReaderService : INFCReaderService, IDisposable
    {
        private ISCardMonitor _monitor;
        private ISCardContext _context;
        private string[] _availableReaders;
        private bool _isMonitoring;

        public event EventHandler<CardDetectedEventArgs> CardDetected;
        public event EventHandler<string> ErrorOccurred;

        public bool IsMonitoring => _isMonitoring;

        public NFCReaderService()
        {
            RefreshReaders();
        }

        public bool StartMonitoring()
        {
            try
            {
                if (_isMonitoring)
                {
                    return true;
                }

                RefreshReaders();

                if (_availableReaders == null || _availableReaders.Length == 0)
                {
                    OnErrorOccured("No NFC readers found. Please check if your reader is connected.");
                    return false;
                }

                _context = ContextFactory.Instance.Establish(SCardScope.System);
                _monitor = MonitorFactory.Instance.Create(SCardScope.System);

                _monitor.CardInserted += OnCardInserted;
                _monitor.CardRemoved += OnCardRemoved;
                _monitor.MonitorException += OnMonitorException;

                _monitor.Start(_availableReaders);
                _isMonitoring = true;

                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccured($"Failed to start monitoring: {ex.Message}");
                return false;
            }
        }

        public void StopMonitoring()
        {
            try
            {
                if (_monitor != null)
                {
                    _monitor.Cancel();
                    _monitor.CardInserted -= OnCardInserted;
                    _monitor.CardRemoved -= OnCardRemoved;
                    _monitor.MonitorException -= OnMonitorException;
                    _monitor.Dispose();
                    _monitor = null;
                }

                _context?.Dispose();
                _context = null;
                _isMonitoring = false;
            }
            catch (Exception ex)
            {
                OnErrorOccured($"Error stopping monitoring: {ex.Message}");
            }
        }

        public string[] GetAvailableReaders()
        {
            RefreshReaders();
            return _availableReaders ?? [];
        }

        public string ReadCardUID(string readerName = null)
        {
            try
            {
                RefreshReaders();

                if (_availableReaders == null || _availableReaders.Length == 0)
                {
                    throw new InvalidOperationException("No NFC readers found. Please check if your reader is connected.");
                }

                string targetReader = readerName ?? _availableReaders[0];

                using (var context = ContextFactory.Instance.Establish(SCardScope.System))
                using (var reader = context.ConnectReader(targetReader, SCardShareMode.Shared, SCardProtocol.Any))
                {
                    var uid = GetCardUID(reader);
                    return BitConverter.ToString(uid).Replace("-", "");
                }
            }
            catch (Exception ex)
            {
                OnErrorOccured($"Failed to read card: {ex.Message}");
                return null;
            }
        }

        private void RefreshReaders()
        {
            try
            {
                using (var context = ContextFactory.Instance.Establish(SCardScope.System))
                {
                    _availableReaders = context.GetReaders();
                }
            }
            catch (Exception ex)
            {
                OnErrorOccured($"Failed to get available readers: {ex.Message}");
                _availableReaders = [];
            }
        }

        private void OnCardInserted(object sender, CardStatusEventArgs e)
        {
            try
            {
                var uid = ReadCardUID(e.ReaderName);
                if (uid != null)
                {
                    CardDetected?.Invoke(this, new CardDetectedEventArgs(e.ReaderName, uid));
                }
            }
            catch (Exception ex)
            {
                OnErrorOccured($"Error processing inserted card: {ex.Message}");
            }
        }

        private void OnCardRemoved(object sender, CardStatusEventArgs e)
        {
            //Here in case we need it later
        }

        private void OnMonitorException(object sender, PCSCException ex)
        {
            OnErrorOccured($"Monitor exception: {ex.Message}");
        }

        private void OnErrorOccured(string errorMessage)
        {
            ErrorOccurred?.Invoke(this, errorMessage);
        }

        private byte[] GetCardUID(ICardReader reader)
        {
            var command = new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 };
            var response = new byte[256];
            var recievedLength = reader.Transmit(command, response);

            if (recievedLength >= 2 && response[recievedLength - 2] == 0x90 && response[recievedLength - 1] == 0x00)
            {
                var uid = new byte[recievedLength - 2];
                Array.Copy(response, 0, uid, 0, recievedLength - 2);
                return uid;
            }

            throw new Exception("Failed to read the card UID");
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}
