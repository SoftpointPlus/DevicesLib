using CommonInterface;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace RFIDLib
{
    public class RFID : Device, ICardReader
    {
        private string data = "";
        public event EventHandler ChangeStatus;
        public RFID()
        {
            DelayTymeout = 1000;
        }
        public new void Open()
        {
            base.Open();
        }
        public new void Close()
        {
            Port.Close();
        }

        public async Task<byte> ReadSectorData(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0x02);
            dataBuf.Add(0x00);
            dataBuf.Add(0x04);
            dataBuf.Add(0x34);
            dataBuf.Add(0x33);
            dataBuf.Add(0x05); //Sector
            dataBuf.Add(0x01); //Block
            dataBuf.Add(0x03);
            dataBuf.Add(Crc(dataBuf.ToArray())); //BCC

            ResponseLength = 26; //Response linght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] buffer = recievedData.ToArray();
            if (CheckCrc(buffer))
            {

                switch (buffer[7])
                {
                    case 0x59:
                        {
                            byte[] dt = new byte[16];
                            Array.Copy(buffer, 8, dt, 0, 16);
                            data = CommonFunctions.ByteArrayToString(dt);
                            return 0x59;
                        };
                    case 0x31: return 0x31;
                    case 0x34: return 0x34;
                    default: return 0xFA;
                }
            }
            else
                return 0xFF;

        }
        public async Task<byte> WriteSectorData(byte[] data)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0x02);
            dataBuf.Add(0x00);
            dataBuf.Add(0x14);
            dataBuf.Add(0x34);
            dataBuf.Add(0x34);
            dataBuf.Add(0x05); //Sector
            dataBuf.Add(0x01); //Block
            foreach (byte i in data)
            {
                dataBuf.Add(i);
            }
            dataBuf.Add(0x03);
            dataBuf.Add(Crc(dataBuf.ToArray())); //BCC

            ResponseLength = 26; //Response linght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] buffer = recievedData.ToArray();

            if (CheckCrc(buffer))
            {
                return buffer[7];
            }
            else
                return 0xFF;
        }
        public void SetConfiguration(string port, int baudrate, Parity parity, int databits, StopBits stopbits)
        {
            Port = new SerialPort(port, baudrate, parity, databits, stopbits);
            Port.ReadTimeout = DelayTymeout;
            Port.WriteTimeout = DelayTymeout;
        }
        public void SetConfiguration(SerialPort port)
        {
            Port = port;
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
        public async Task<byte> VerifyPassword(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0x02);
            dataBuf.Add(0x00);
            dataBuf.Add(0x09);
            dataBuf.Add(0x34);
            dataBuf.Add(0x32);
            dataBuf.Add(0x05); //Sector
            dataBuf.Add(0xFF);
            dataBuf.Add(0xFF);
            dataBuf.Add(0xFF);
            dataBuf.Add(0xFF);
            dataBuf.Add(0xFF);
            dataBuf.Add(0xFF);
            dataBuf.Add(0x03);

            dataBuf.Add(Crc(dataBuf.ToArray())); //BCC

            ResponseLength = 9; //Response linght
            Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
            await Task.Delay(DelayTymeout);

            byte[] buffer = recievedData.ToArray();


            if (CheckCrc(buffer))
                return buffer[6];
            else
                return 0xFF;
        }
        public async Task<byte> GetRFCard(object param)
        {
            recievedData.Clear();
            dataBuf.Clear();
            dataBuf.Add(0x02);
            dataBuf.Add(0x00);
            dataBuf.Add(0x02);
            dataBuf.Add(0x34);
            dataBuf.Add(0x30);
            dataBuf.Add(0x03);
            dataBuf.Add(Crc(dataBuf.ToArray())); //BCC

            ResponseLength = 8; //Response linght
            try
            {
                Port.Write(dataBuf.ToArray(), 0, dataBuf.Count);
                await Task.Delay(DelayTymeout);

                byte[] buffer = recievedData.ToArray();

                if (CheckCrc(buffer))
                    return buffer[5];
                else
                    return 0xFF;
            }
            catch (TimeoutException e)
            {
                return 0xFE;
            }

        }
        private void OnGetStatus(ReaderEventArgs e)
        {
            ChangeStatus?.Invoke(this, e);
        }

        public string ReadData { get { return data; } }
    }
}
