using CommonInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Threading;
using System.Windows.Forms;
using System.Collections;

namespace BillAcceptorUBA10_SS
{
    public class BillAceptor : Device, IBillValidator
    {
        private enum Mode { Accepting, PayOut, Idle };
        private bool IsStacked = false;
        private Mode currentMode = Mode.Idle;
        public byte Status { get; set; }
        private bool enableProcess = false;
        private Dictionary<byte, string> status = new Dictionary<byte, string>();
        private Dictionary<byte, Tuple<byte, byte>> denomination = new Dictionary<byte, Tuple<byte, byte>>();
        private Queue<Tuple<Func<object, Task<byte>>, object>> command = new Queue<Tuple<Func<object, Task<byte>>, object>>();
        byte[] billData = new byte[4];
        private CancellationTokenSource cancelTokenSource;

        public event EventHandler ChengeStatus;
        public event EventHandler BillAccept;
        public BillAceptor()
        {
            DelayTymeout = 100;
            status.Add(0x11, "Idle");
            status.Add(0x12, "Accepting");
            status.Add(0x13, "ESCROW");
            status.Add(0x14, "Stacking");
            status.Add(0x15, "Vend Valid");
            status.Add(0x16, "Stacked");
            status.Add(0x17, "Rejecting");
            status.Add(0x18, "Returning");
            status.Add(0x1A, "Disable");
            status.Add(0x1B, "Initialize");

            Status = 0;
        }

        public void SetConfiguration(string port, int baudrate, Parity parity, int databits, StopBits stopbits)
        {
            Port = new SerialPort(port, baudrate, parity, databits, stopbits);
        }
        public void SetConfiguration(SerialPort port)
        {
            Port = port;
        }

        public async Task StartCommunication(CancellationTokenSource tokenSource)
        {
            cancelTokenSource = tokenSource;
            await Task.Run(async () => await StartProccess(tokenSource.Token));
        }

        private async Task StartProccess(CancellationToken token)
        {
            byte resultStatus;
            string statusString;
            bool isDispensing = false;
            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(Reset, 0));
            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(SetSequrity, 0));
            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(Enable, false));
            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(OptionalFunction, false));
            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(VersionInformation, 0));
            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(Currrency, 0));
            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(C6, 0));
            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(C7, 0));
            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(CommunicationMode, 0));
            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(StatusRequest, 0));

            enableProcess = true;
            while (!token.IsCancellationRequested)
            {
                Tuple<byte, byte> denominationValue = null;
                while (command.Count > 0)
                {
                    var cmdInfo = command.Dequeue();
                    resultStatus = await cmdInfo.Item1(cmdInfo.Item2);
                    if (currentMode == Mode.Accepting)
                    {
                        switch (resultStatus)
                        {
                            case 0x12:
                                {
                                    command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(StatusRequest, 0));
                                    IsStacked = false;
                                    break;
                                };
                            case 0x13: //ESCROW
                                {
                                    //отправляем номинал купюры клиенту
                                    byte[] reciveBuf = recievedData.ToArray();
                                    denominationValue = null;
                                    denomination.TryGetValue(reciveBuf[3], out denominationValue);
                                    command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(Hold, 0));
                                    command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(Stack2, 0));
                                    break;
                                };
                            case 0x14: //STACKING
                                {
                                    command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(StatusRequest, 0));
                                    break;
                                };
                            case 0x15:// VEND_VALID
                                {
                                    command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(ACK, 0));
                                    break;
                                };
                            case 0x16:// STACKED
                                {
                                    command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(StatusRequest, 0));
                                    if (!IsStacked && denominationValue != null)
                                    {
                                        IsStacked = true;
                                        OnBillAccept(new BillEventArgs(Status, "", denominationValue.Item1 * Math.Truncate(Math.Pow(10, denominationValue.Item2))));
                                    }

                                    break;
                                };
                            case 0x50:// ACK
                                {
                                    command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(StatusRequest, 0));

                                    break;
                                };
                            default:
                                {
                                    command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(StatusRequest, 0));
                                    break;
                                }
                        }
                    }
                    //Выдача сдачи
                    if (currentMode == Mode.PayOut)
                    {
                        switch (resultStatus)
                        {
                            case 0x1A:
                                {
                                    if (!isDispensing)
                                    {
                                        command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(Dispense, billData));
                                        isDispensing = true;
                                    }
                                    else
                                        currentMode = Mode.Idle; 
                                    break;
                                }
                            case 0x50:// ACK
                                {
                                    command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(StatusRequest, 0));
                                    break;
                                };
                            case 0x23:// PAY VALID
                                {
                                    command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(ACK, 0));
                                    break;
                                };

                                //command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(StatusRequest, 0));
                        }
                    }


                    switch (resultStatus)
                    {
                        case 0x11:
                            {
                                currentMode = Mode.Idle; //
                                break;
                            };
                        case 0x12:
                            {
                                currentMode = Mode.Accepting; //Прием денег 
                                break;
                            };



                    }
                    //отправляем статус
                    if (status.TryGetValue(resultStatus, out statusString))
                    {
                        if (resultStatus != Status)
                        {
                            Status = resultStatus;
                            OnGetStatus(new BillEventArgs(resultStatus, statusString, -1));
                        }
                    }
                }
                await Task.Delay(DelayTymeout);
                command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(StatusRequest, 0));
            }
            enableProcess = false;
        }

        public void SetInhibit(bool inhibit)
        {
            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(Inhibit, inhibit));
        }

        private async Task<byte> Inhibit(object param)
        {
            var inhibit = Convert.ToBoolean(param);
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x06);
            dataBuf.Add(0xC3);
            if (inhibit)
                dataBuf.Add(0x01);
            else
                dataBuf.Add(0x00);
            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 7; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);
            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
                return 0;

        }

        private async Task<byte> CommunicationMode(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x06);
            dataBuf.Add(0xC2);
            dataBuf.Add(0x00);
            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 6; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
                return 0;

        }

        private async Task<byte> C6(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x07);
            dataBuf.Add(0xC6);
            dataBuf.Add(0x01);
            dataBuf.Add(0x12);
            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 7; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
                return 0;

        }

        private async Task<byte> C7(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x06);
            dataBuf.Add(0xC7);
            dataBuf.Add(0xFC);
            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 6; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
                return 0;

        }

        private async Task<byte> StatusRequest(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x05);
            dataBuf.Add(0x11);
            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 5; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
            {
                return 0;
            }
        }

        private async Task<byte> StatusRequestExtAsync(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x07);
            dataBuf.Add(0xF0);
            dataBuf.Add(0x20);
            dataBuf.Add(0x1A);
            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 5; //Response lenght?
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
            {
                return 0;
            }
        }

        private async Task<byte> PayOutAsync(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x08);
            dataBuf.Add(0xF0);
            dataBuf.Add(0x20);
            dataBuf.Add(0x4A);
            try
            {
                dataBuf.Add(Convert.ToByte(param));
            }
            catch (Exception)
            {
                dataBuf.Add(0X00);
            }

            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 5; //Response lenght?
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
            {
                return 0;
            }
        }

        private async Task<byte> Hold(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x06);
            dataBuf.Add(0x44);
            dataBuf.Add(0xFF);
            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 5; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
            {
                return 0;
            }


        }

        private async Task<byte> Currrency(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x05);
            dataBuf.Add(0x8A);
            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 50; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                denomination.Clear();
                int i = 1;
                var tmpBuf = reciveBuf.Skip(3).ToArray();
                while (3 + (4 * i) < reciveBuf.Length)
                {
                    denomination.Add(tmpBuf[0], new Tuple<byte, byte>(tmpBuf[2], tmpBuf[3]));
                    tmpBuf = reciveBuf.Skip(3 + (4 * i)).ToArray();
                    i++;
                }
                return reciveBuf[2];
            }
            else
            {
                return 0;
            }
        }
        private async Task<byte> VersionInformation(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x05);
            dataBuf.Add(0x88);
            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 50; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
                return 0;
        }
        private async Task<byte> OptionalFunction(object param)
        {
            var enable = Convert.ToBoolean(param);
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x07);
            dataBuf.Add(0xC5);
            dataBuf.Add(0x03);
            if (enable)
                dataBuf.Add(0x00);
            else
                dataBuf.Add(0x01);
            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 7; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
                return 0;

        }
        private async Task<byte> Enable(object param)
        {
            var enable = Convert.ToBoolean(param);
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x07);
            dataBuf.Add(0xC0);
            dataBuf.Add(0x00);
            if (enable)
                dataBuf.Add(0x00);
            else
                dataBuf.Add(0x01);

            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 7; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
                return 0;
        }

        private async Task<byte> Reset(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x05);
            dataBuf.Add(0x40);
            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 5; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
                return 0;

        }

        private async Task<byte> SetSequrity(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x07);
            dataBuf.Add(0xC1);
            dataBuf.Add(0x00);
            dataBuf.Add(0x00);
            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 5; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
                return 0;
        }

        private async Task<byte> ACK(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x05);
            dataBuf.Add(0x50);
            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 5; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
                return 0;
        }
        private async Task<byte> Stack2(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(0x05);
            dataBuf.Add(0x42);
            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 5; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
                return 0;
        }



        protected bool CheckCrc(byte[] data)
        {
            if (data.Length > 2)
            {
                byte[] buf = new byte[data.Length - 2];
                Array.Copy(data, 0, buf, 0, data.Length - 2);
                byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(buf);
                return (crcBuffer[0] == data[data.Length - 2] && crcBuffer[1] == data[data.Length - 1] ? true : false);
            }
            else
                return false;
        }

        public new void Open()
        {
            base.Open();
        }
        public new void Close()
        {

            SetInhibit(true);
            Thread.Sleep(1000);
            cancelTokenSource.Cancel();
            Task.Run(async () =>
            {
                while (!enableProcess)
                {
                    await Task.Delay(500);
                }
            });
            Port.Close();
        }

        protected void OnGetStatus(BillEventArgs e)
        {
            ChengeStatus?.Invoke(this, e);
        }

        protected void OnBillAccept(BillEventArgs e)
        {
            BillAccept?.Invoke(this, e);
        }

        public async Task<byte> Dispense(object param)
        {
            var billsInfo = CommonFunctions.ObjectToByteArray(param);
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0xFC);
            dataBuf.Add(CommonFunctions.StringToByteArray((billsInfo.Length + 7).ToString())[0]);
            dataBuf.Add(0xF0);
            dataBuf.Add(0x20);
            dataBuf.Add(0x4A);
            foreach(var val in billsInfo)
            {
                dataBuf.Add(val);
            }
            byte[] crcBuffer = Crc16CcittKermit.ComputeChecksumBytes(dataBuf.ToArray());
            dataBuf.Add(crcBuffer[0]);
            dataBuf.Add(crcBuffer[1]);
            ResponseLength = 5; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] reciveBuf = recievedData.ToArray();
            if (CheckCrc(reciveBuf))
            {
                return reciveBuf[2];
            }
            else
                return 0;
        }

        public void DispenceBill(int bill1, int space1, int bill2, int space2)
        {
            billData[0] = CommonFunctions.StringToByteArray(bill1.ToString())[0];
            billData[1] = CommonFunctions.StringToByteArray(space1.ToString())[0];
            billData[2] = CommonFunctions.StringToByteArray(bill2.ToString())[0];
            billData[3] = CommonFunctions.StringToByteArray(space2.ToString())[0];
            currentMode = Mode.PayOut;
            command.Enqueue(new Tuple<Func<object, Task<byte>>, object>(Inhibit, true));
        }
    }
}
