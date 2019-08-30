using CommonInterface;
using System;
using System.IO.Ports;
using System.Reflection;
using System.Windows.Forms;

namespace DevicesTest
{
    public partial class Form1 : Form
    {
        ICardReader rfidReader;
        IDispenser dispenser;
        IBillValidator bill;
        IBarrier barrier;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Type t;
            Assembly asm = Assembly.LoadFrom(@"RFIDLib.dll");
            t = asm.GetType("RFIDLib.RFID", true, true);
            rfidReader = (ICardReader)Activator.CreateInstance(t);
            rfidReader.ChangeStatus += RfidReader_ChangeStatus;

            t = asm.GetType("RFIDLib.DispenserMT166", true, true);
            dispenser = (IDispenser)Activator.CreateInstance(t);
            dispenser.ChangeStatus += Dispense_GetStatus;

            asm = Assembly.LoadFrom(@"BillAcceptorUBA10-SS.dll");
            t = asm.GetType("BillAcceptorUBA10_SS.BillAceptor", true, true);
            bill = (IBillValidator)Activator.CreateInstance(t);

            asm = Assembly.LoadFrom(@"BarrierLib.dll");
            t = asm.GetType("BarrierLib.Barrier", true, true);
            barrier = (IBarrier)Activator.CreateInstance(t);

        }

        private void RfidReader_ChangeStatus(object sender, EventArgs e)
        {
            var eventArg = e as ReaderEventArgs;
            this.Invoke((MethodInvoker)delegate { textBox7.Text += eventArg.StatusString + "\r\n"; });
        }

        private void Dispense_GetStatus(object sender, EventArgs e)
        {
            var eventArg = e as DispenserEventArgs;
            this.Invoke((MethodInvoker)delegate { textBox6.Text += eventArg.StatusString + "\r\n"; });
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            try
            {
                SerialPort port = new SerialPort(tbPort.Text, Int32.Parse(tbBoudRate.Text), (Parity)Enum.Parse(typeof(Parity), tbParity.Text),
                    Int32.Parse(tbBits.Text), (StopBits)Enum.Parse(typeof(StopBits), tbStopBits.Text));

                rfidReader.SetConfiguration(port);
                rfidReader.Open();
                button3.Enabled = true;
                button4.Enabled = true;
                button10.Enabled = true;
                button1.Enabled = false;
            }
            catch
            {
                button3.Enabled = false;
                button4.Enabled = false;
                button10.Enabled = false;
            }
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            try
            {
                rfidReader.Close();
                button3.Enabled = false;
                button4.Enabled = false;
                button10.Enabled = false;
                button1.Enabled = true;
            }
            catch
            {
                button1.Enabled = false;
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            try
            {
                dispenser.SetConfiguration(tbDispPort.Text, Int32.Parse(tbDispBaudRate.Text), (Parity)Enum.Parse(typeof(Parity), tbDispParity.Text),
                    Int32.Parse(tbDispBits.Text), (StopBits)Enum.Parse(typeof(StopBits), tbDispStopBits.Text));
                dispenser.Open();

                button6.Enabled = true;
                button5.Enabled = true;
                button9.Enabled = true;
                button8.Enabled = false;
                button21.Enabled = true;
            }
            catch
            {
                button6.Enabled = false;
                button5.Enabled = false;
                button9.Enabled = false;
                button21.Enabled = false;
            }
        }

        private async void button6_Click(object sender, EventArgs e)
        {
            await dispenser.SendCardToReadPosition();
        }

        private async void button3_Click_1(object sender, EventArgs e)
        {
            byte res = await rfidReader.ReadSectorData(null);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            dispenser.Close();
            button6.Enabled = false;
            button5.Enabled = false;
            button9.Enabled = false;
            button8.Enabled = true;
        }

        private async void button4_Click_1(object sender, EventArgs e)
        {
            await rfidReader.WriteSectorData(ToByteArray(textBox1.Text));
        }

        public static byte[] ToByteArray(String HexString)
        {
            int NumberChars = HexString.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(HexString.Substring(i, 2), 16);
            }
            return bytes;
        }

        private async void button5_Click(object sender, EventArgs e)
        {
            await dispenser.DispenseCard();
        }

        private async void button9_Click(object sender, EventArgs e)
        {
            await dispenser.RecycleCard();
        }


        private async void button11_Click(object sender, EventArgs e)
        {
            bill.ChengeStatus += Bill_GetStatus;
            bill.BillAccept += Bill_BillAccept;
            //await bill.StartCommunication();
        }

        private void Bill_BillAccept(object sender, EventArgs e)
        {
            var eventArg = e as BillEventArgs;
            this.Invoke((MethodInvoker)delegate { textBox5.Text = eventArg.BillDenomination.ToString(); });
        }

        private void Bill_GetStatus(object sender, EventArgs e)
        {
            var eventArg = e as BillEventArgs;
            this.Invoke((MethodInvoker)delegate { textBox3.Text = eventArg.StatusString; });
            
        }

        private void button13_Click(object sender, EventArgs e)
        {

            bill.SetConfiguration(tbBillPort.Text, Int32.Parse(tbBillBaudrate.Text), (Parity)Enum.Parse(typeof(Parity), tbBillParity.Text),
                    Int32.Parse(tbBillBits.Text), (StopBits)Enum.Parse(typeof(StopBits), tbBillStopBits.Text));
            bill.Open();
            
        }

        private void button12_Click(object sender, EventArgs e)
        {
            bill.Close();
        }

        private void button14_Click(object sender, EventArgs e)
        {
            bill.SetInhibit(true);
        }

        private void button15_Click(object sender, EventArgs e)
        {
             bill.SetInhibit(false);
        }

        private async void button10_Click_1(object sender, EventArgs e)
        {
            await rfidReader.VerifyPassword(null);
        }

        private void button17_Click(object sender, EventArgs e)
        {
            try
            {
                SerialPort port = new SerialPort(tbBarrierPort.Text, Int32.Parse(tbBarrierBaudRate.Text), (Parity)Enum.Parse(typeof(Parity), tbBarrierParity.Text),
                    Int32.Parse(tbBarrierBits.Text), (StopBits)Enum.Parse(typeof(StopBits), tbBarrierStopBits.Text));

                barrier.SetConfiguration(port);
                barrier.Open();
                button16.Enabled = true;
                button17.Enabled = false;

            }
            catch
            {
                button16.Enabled = false;
                button17.Enabled = true;

            }
        }

        private void button16_Click(object sender, EventArgs e)
        {
            try
            {
                barrier.Close();
                button17.Enabled = true;
                button16.Enabled = false;

            }
            catch
            {
                button17.Enabled = false;
            }
        }

        private async void button18_Click(object sender, EventArgs e)
        {
            barrier.ChangeStatus += Barrier_GetStatus;
            await barrier.StartCommunication();
        }

        private void Barrier_GetStatus(object sender, EventArgs e)
        {
            var eventArg = e as BarrierEventArgs;
            this.Invoke((MethodInvoker)delegate {
                switch (eventArg.StatusCode)
                {
                    case 0x6D:
                        {
                            tbLoopA.Text = "занята";
                            break;
                        }
                    case 0x68:
                        {
                            tbLoopB.Text = "занята";
                            break;
                        }
                    case 0x64:
                        {
                            tbButton.Text = "нажата";
                            break;
                        }
                    case 0x6C:
                        {
                            tbLoopA.Text = "свободна";
                            tbLoopB.Text = "свободна";
                            tbButton.Text = "не нажата";
                            break;
                        }
                    case 0x0A:
                        {
                            tbBarrier.Text = "открывается";
                            break;
                        }
                    case 0x0B:
                        {
                            tbBarrier.Text = "открыт";
                            break;
                        }
                    case 0x14:
                        {
                            tbBarrier.Text = "закрывается";
                            break;
                        }
                    case 0x15:
                        {
                            tbBarrier.Text = "закрыт";
                            break;
                        }

                }
            });
        }

        private void button19_Click(object sender, EventArgs e)
        {
            barrier.SetOpenBarrier();
        }

        private void button20_Click(object sender, EventArgs e)
        {
            barrier.SetCloseBarrier();
        }

        private async void button21_Click(object sender, EventArgs e)
        {
            await dispenser.GetStatus();
        }

        private async void button22_Click(object sender, EventArgs e)
        {
            await rfidReader.GetRFCard(null);
        }
    }
}

