using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommonInterface
{
    public abstract class Device
    {
        private SerialPort _port;
        protected int DelayTymeout = 1000;
        protected Queue<byte> recievedData = new Queue<byte>();
        protected int ResponseLength { get; set; }


        protected List<byte> dataBuf = new List<byte>();
        public Device()
        {

        }
        public SerialPort Port
        {
            get { return _port; }
            set
            {
                _port = value;
                _port.DataReceived += DataReceived;
            }
        }


        protected virtual void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int bytes = _port.BytesToRead;
            byte[] data = new byte[bytes];
            _port.Read(data, 0, bytes);

            data.ToList().ForEach(b => recievedData.Enqueue(b));
        }
        protected void Open()
        {
            Port.Open();
        }

        protected void Close()
        {
            Port.Close();
        }
    }
}
