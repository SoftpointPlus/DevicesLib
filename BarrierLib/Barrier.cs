using CommonInterface;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BarrierLib
{
    public class Barrier : Device, IBarrier
    {
        private const int repeatComand = 16;
        private enum States { stOpening, stOpen, stClosing, stClose, stNorm, stGetState, stUnknow};
        private States currentState = States.stUnknow;
        private CancellationTokenSource cancelTokenSource;
        private CancellationToken token;

        private byte state { get; set; }
        private Dictionary<byte, string> status = new Dictionary<byte, string>();
        private Queue<Tuple<Func<object, Task<byte>>, object>> command = new Queue<Tuple<Func<object, Task<byte>>, object>>();

        public event EventHandler ChangeStatus;

        public Barrier()
        {
            DelayTymeout = 100;
            status.Add(0x6D, "loopA");
            status.Add(0x68, "loopB");
            status.Add(0x64, "Кнопка нажата");
            status.Add(0x6C, "Норма");
            status.Add(0x14, "Закрывается");
            status.Add(0x15, "Закрыт");
            status.Add(0x0A, "Открывается ");
            status.Add(0x0B, "Открыт");
            state = 0;
        }
        public async Task<byte> CloseBarrier(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0x02);
            dataBuf.Add(0x00);
            dataBuf.Add(0x0A);
            dataBuf.Add(0x31);
            dataBuf.Add(0x30);
            dataBuf.Add(0x42);
            dataBuf.Add(0x43);
            dataBuf.Add(0x48);
            dataBuf.Add(0x20);
            dataBuf.Add(0x09);
            dataBuf.Add(0x00);
            dataBuf.Add(0x00);
            dataBuf.Add(0x00);
            dataBuf.Add(0x03);
            dataBuf.Add(0x6A);

            ResponseLength = 15; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(100);
            byte[] reciveBuf = recievedData.ToArray();
            OnGetStatus(new BarrierEventArgs(0x14, "Закрывается"));
            currentState = States.stClosing;
            return 0x14;

        }

        public void SetCloseBarrier()
        {
            for (int i = 0; i < repeatComand; i++)
            {
                command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(CloseBarrier, 0));
            }

        }

        public async Task<byte> OpenBarrier(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0x02);
            dataBuf.Add(0x00);
            dataBuf.Add(0x0A);
            dataBuf.Add(0x31);
            dataBuf.Add(0x30);
            dataBuf.Add(0x42);
            dataBuf.Add(0x43);
            dataBuf.Add(0x48);
            dataBuf.Add(0x20);
            dataBuf.Add(0x0A);
            dataBuf.Add(0x00);
            dataBuf.Add(0x00);
            dataBuf.Add(0x00);
            dataBuf.Add(0x03);
            dataBuf.Add(0x69);

            ResponseLength = 15; //Response lenght

            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(100);
            byte[] reciveBuf = recievedData.ToArray();
            OnGetStatus(new BarrierEventArgs(0x0A, "Открывается"));
            currentState = States.stOpening;
            return 0x0A;
        }

        public void SetOpenBarrier()
        {
            for (int i = 0; i < repeatComand; i++)
            {
                command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(OpenBarrier, 0));
            }


        }

        public void SetConfiguration(string port, int baudrate, Parity parity, int databits, StopBits stopbits)
        {
            Port = new SerialPort(port, baudrate, parity, databits, stopbits);
        }

        public void SetConfiguration(SerialPort port)
        {
            Port = port;
        }

        public async Task StartCommunication()
        {
            cancelTokenSource = new CancellationTokenSource();
            token = cancelTokenSource.Token;
            await Task.Run(async () => await StartProccess(token));
        }

        private async Task StartProccess(CancellationToken token)
        {
            byte resultStatus;
            string statusString;


            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(InitBarrier1, 0));
            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(InitBarrier2, 0));
            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(CloseBarrier, 0));
            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(StatusRequest, 0));


            while (!token.IsCancellationRequested)
            {
                while (command.Count > 0)
                {
                    var cmdInfo = command.Dequeue();
                    resultStatus = await cmdInfo.Item1(cmdInfo.Item2);
                    //отправляем статус
                    if (status.TryGetValue(resultStatus, out statusString))
                    {
                        if (resultStatus != state)
                        {
                            state = resultStatus;
                            OnGetStatus(new BarrierEventArgs(resultStatus, statusString));
                        }
                    }
                }
                await Task.Delay(DelayTymeout);
                command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(StatusRequest, 0));
            }
        }

        private void OnGetStatus(BarrierEventArgs e)
        {
            ChangeStatus?.Invoke(this, e);
        }

        void IDevice.Close()
        {
            Thread.Sleep(1000);
            cancelTokenSource.Cancel();
            Thread.Sleep(500);
            Port.Close();
        }

        void IDevice.Open()
        {
            base.Open();
        }
        private async Task<byte> StatusRequest(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0x02);
            dataBuf.Add(0x00);
            dataBuf.Add(0x0A);
            dataBuf.Add(0x31);
            dataBuf.Add(0x30);
            dataBuf.Add(0x42);
            dataBuf.Add(0x43);
            dataBuf.Add(0x48);
            dataBuf.Add(0x20);
            dataBuf.Add(0x08);
            dataBuf.Add(0x00);
            dataBuf.Add(0x00);
            dataBuf.Add(0x00);
            dataBuf.Add(0x03);
            dataBuf.Add(0x6B);

            ResponseLength = 15; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);
            byte[] reciveBuf = recievedData.ToArray();

            if (reciveBuf.Length < 15)
                return 0; // ошибка
            else
            {


                switch (currentState)
                {
                    case States.stOpening:
                        {
                            currentState = States.stOpen;
                            OnGetStatus(new BarrierEventArgs(0x0B, "Открыто"));
                            break;
                        }
                    case States.stClosing:
                        {
                            currentState = States.stClose;
                            OnGetStatus(new BarrierEventArgs(0x15, "Закрыто"));
                            break;
                        }
                    default:
                        {
                            currentState = States.stGetState;
                            break;
                        }
                }
                return reciveBuf[reciveBuf.Length - 1]; //возвращаем crc т.к не знаем протокол и оринтируемся на неё
            }
           
                    
        }

        public async Task<byte> InitBarrier1(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0x02);
            dataBuf.Add(0x00);
            dataBuf.Add(0x02);
            dataBuf.Add(0x32);
            dataBuf.Add(0x30);
            dataBuf.Add(0x03);
            dataBuf.Add(0x01);

            ResponseLength = 18; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            return 0;
        }

        public async Task<byte> InitBarrier2(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0x02);
            dataBuf.Add(0x00);
            dataBuf.Add(0x0A);
            dataBuf.Add(0x31);
            dataBuf.Add(0x30);
            dataBuf.Add(0x42);
            dataBuf.Add(0x43);
            dataBuf.Add(0x48);
            dataBuf.Add(0x20);
            dataBuf.Add(0x05);
            dataBuf.Add(0x00);
            dataBuf.Add(0x00);
            dataBuf.Add(0x00);
            dataBuf.Add(0x03);
            dataBuf.Add(0x66);

            ResponseLength = 18; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            return 0;
           
        }
    }
}
