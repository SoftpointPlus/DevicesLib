using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace CommonInterface
{
    public interface ICardReader : IDevice
    {
        event EventHandler ChangeStatus;
        Task<byte> ReadSectorData(object param);
        Task<byte> WriteSectorData(byte[] data);
        Task<byte> GetRFCard(object param);
        Task<byte> VerifyPassword(object param);

        string ReadData { get; }
    }
    public interface IDispenser : IDevice
    {
        event EventHandler ChangeStatus;
        Task GetStatus();
        Task SendCardToReadPosition();
        Task DispenseCard();
        Task RecycleCard();

    }

    public interface IBillValidator : IDevice
    {
        Task StartCommunication(CancellationTokenSource tokenSource);
        void DispenceBill(int bill1, int space1, int bill2, int space2);
        void SetInhibit(bool inhibit);
        event EventHandler ChengeStatus;
        event EventHandler BillAccept;
    }

    public interface IBarrier : IDevice
    {
        Task StartCommunication();

        void SetOpenBarrier();
        void SetCloseBarrier();

        event EventHandler ChangeStatus;

    }
    public interface IDevice
    {
        void SetConfiguration(string port, int baudrate, Parity parity, int databits, StopBits stopbits);
        void SetConfiguration(SerialPort port);
        void Open();
        void Close();
    }

    public class BillEventArgs : EventArgs
    {
        public byte StatusCode { get; set; }
        public string StatusString { get; set; }
        public double BillDenomination { get; set; }
        public BillEventArgs(byte statusCode, string statusString, double denomination)
        {
            StatusCode = statusCode;
            StatusString = statusString;
            BillDenomination = denomination;
        }
    }

    public class BarrierEventArgs : EventArgs
    {
        public byte StatusCode { get; set; }
        public string StatusString { get; set; }
        public BarrierEventArgs(byte statusCode, string statusString)
        {
            StatusCode = statusCode;
            StatusString = statusString;
        }
    }

    public class DispenserEventArgs : EventArgs
    {
        public byte StatusCode { get; set; }
        public string StatusString { get; set; }
        public DispenserEventArgs(byte statusCode, string statusString)
        {
            StatusCode = statusCode;
            StatusString = statusString;
        }
    }

    public class ReaderEventArgs : EventArgs
    {
        public byte StatusCode { get; set; }
        public string StatusString { get; set; }
        public ReaderEventArgs(byte statusCode, string statusString)
        {
            StatusCode = statusCode;
            StatusString = statusString;
        }
    }
}
