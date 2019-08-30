using ByteExtensionMethods;
using CommonInterface;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RFIDLib
{
    public class DispenserMT166 : Device, IDispenser, IDevice
    {
        public event EventHandler ChangeStatus;
        private Dictionary<byte, string> statuses = new Dictionary<byte, string>();

        public DispenserMT166()
        {
            DelayTymeout = 1000;
            statuses.Add(0x10, "Card box empty");
            statuses.Add(0x11, "Card box not empty");
            statuses.Add(0x12, "Card is at dispense position");
            statuses.Add(0x13, "Card isn't at dispense position");
            statuses.Add(0x14, "Card at pre send position");
            statuses.Add(0x15, "Card isn't at pre send position");
            statuses.Add(0x16, "Card shortage");
            statuses.Add(0x17, "Normal");
            statuses.Add(0x18, "Dispensing card");
            statuses.Add(0x19, "Ready");
            statuses.Add(0x20, "Card accepting");
            statuses.Add(0x21, "Ready");
            statuses.Add(0x22, "Dispensing card error");
            statuses.Add(0x23, "Normal");
            statuses.Add(0x24, "Card time out and recycled");
            statuses.Add(0x25, "No card time out");
        }

        public new void Open()
        {
            base.Open();
        }
        public new void Close()
        {
            Port.Close();
        }

        public async Task SendCardToReadPosition()
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0x02);
            dataBuf.Add(0x00);
            dataBuf.Add(0x02);
            dataBuf.Add(0x31);
            dataBuf.Add(0x30);
            dataBuf.Add(0x03);
            dataBuf.Add(Crc(dataBuf.ToArray())); //BCC

            ResponseLength = 8; //Response lenght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] buffer = recievedData.ToArray();

            if (CheckCrc(buffer))
            {
                switch (buffer[5])
                {
                    case 0x59:
                        {
                            MessageBox.Show("Send card command: success");
                            break;
                        };
                    case 0x4E:
                        {
                            MessageBox.Show("Send card command: failed");
                            break;
                        }
                }
            }
            else
                MessageBox.Show("Read command: Check summ failed");
        }

        protected byte Crc(byte[] data)
        {
            byte resultCrc = data[0];
            for (int i = 1; i < data.Length; i++)
            {
                resultCrc ^= data[i];
            }
            return resultCrc;
        }

        protected bool CheckCrc(byte[] data)
        {
            if (data.Length <= 0)
            {
                return false;
            }
            byte resultCrc = data[0];

            for (int i = 1; i < data.Length - 1; i++)
            {
                resultCrc ^= data[i];
            }
            return resultCrc == data[data.Length - 1];
        }

        public void SetConfiguration(string port, int baudrate, Parity parity, int databits, StopBits stopbits)
        {
            Port = new SerialPort(port, baudrate, parity, databits, stopbits);
            Port.ReadTimeout= DelayTymeout;
            Port.WriteTimeout = DelayTymeout;
        }
        public void SetConfiguration(SerialPort port)
        {
            Port = port;
        }

        public async Task DispenseCard()
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0x02);
            dataBuf.Add(0x00);
            dataBuf.Add(0x02);
            dataBuf.Add(0x31);
            dataBuf.Add(0x31);
            dataBuf.Add(0x03);
            dataBuf.Add(Crc(dataBuf.ToArray())); //BCC

            ResponseLength = 8; //Response lenght
            try
            {
                Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
                await Task.Delay(DelayTymeout);

                byte[] buffer = recievedData.ToArray();

                if (CheckCrc(buffer))
                {
                    switch (buffer[5])
                    {
                        case 0x59:
                            {
                                MessageBox.Show("Dispense card command: success");
                                break;
                            };
                        case 0x4E:
                            {
                                MessageBox.Show("Dispense card command: failed");
                                break;
                            }
                    }
                }
                else
                    MessageBox.Show("Dispense card: Check summ failed");
            }catch(TimeoutException e)
            {
                OnGetStatus(new DispenserEventArgs(0xF1, "Com port timeout error"));
            }
        }

        public async Task RecycleCard()
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0x02);
            dataBuf.Add(0x00);
            dataBuf.Add(0x02);
            dataBuf.Add(0x33);
            dataBuf.Add(0x30);
            dataBuf.Add(0x03);
            dataBuf.Add(Crc(dataBuf.ToArray())); //BCC

            ResponseLength = 8; //Response lenght
            try
            {
                Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
                await Task.Delay(DelayTymeout);

                byte[] buffer = recievedData.ToArray();

                if (CheckCrc(buffer))
                {
                    switch (buffer[5])
                    {
                        case 0x59:
                            {
                                MessageBox.Show("Recycle card command: success");
                                break;
                            };
                        case 0x4E:
                            {
                                MessageBox.Show("Recycle card command: failed");
                                break;
                            }
                    }
                }
                else
                    MessageBox.Show("Recycle card: Check summ failed");
            }catch(TimeoutException e)
            {
                OnGetStatus(new DispenserEventArgs(0xF1, "Com port timeout error"));
            }
        }

        public async Task GetStatus()
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0x02);
            dataBuf.Add(0x00);
            dataBuf.Add(0x02);
            dataBuf.Add(0x32);
            dataBuf.Add(0x30);
            dataBuf.Add(0x03);
            dataBuf.Add(Crc(dataBuf.ToArray())); //BCC

            ResponseLength = 8; //Response lenght
            try
            {
                Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
                await Task.Delay(DelayTymeout);

                byte[] buffer = recievedData.ToArray();
                int pos = 0;
                if (CheckCrc(buffer))
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (buffer[5].IsBitSet(i))
                            OnGetStatus(new DispenserEventArgs(statuses.ElementAt(pos).Key, statuses.ElementAt(pos).Value));
                        else
                            OnGetStatus(new DispenserEventArgs(statuses.ElementAt(pos + 1).Key, statuses.ElementAt(pos + 1).Value));
                        pos += 2;
                    }

                }
                else
                    OnGetStatus(new DispenserEventArgs(0xF0, "Recycle card: Check summ failed"));
            }
            catch (TimeoutException)
            {
                OnGetStatus(new DispenserEventArgs(0xF1, "Com port timeout error"));
            }
        }
        private void OnGetStatus(DispenserEventArgs e)
        {
            ChangeStatus?.Invoke(this, e);
        }
    }
}

