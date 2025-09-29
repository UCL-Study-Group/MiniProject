using NFCServiceLibrary.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NFCServiceLibrary.Services.Interfaces
{
    public interface INFCReaderService : IDisposable
    {
        event EventHandler<CardDetectedEventArgs> CardDetected;
        event EventHandler<string> ErrorOccurred;
        bool IsMonitoring { get; }
        bool StartMonitoring();
        void StopMonitoring();
        string[] GetAvailableReaders();
        string ReadCardUID(string readerName = null);
    }
}
