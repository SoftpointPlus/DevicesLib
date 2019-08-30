using CommonInterface;
using DevExpress.XtraTab;
using System;
using System.IO.Ports;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

using CommonLib;
using System.Configuration;

namespace BarrierControl
{
    public partial class MainForm : DevExpress.XtraEditors.XtraForm
    {

        private ICardReader rfidReader;
        private IDispenser dispenser;
        private IBillValidator billValidator;
        private CancellationTokenSource cancelBillTokenSource = new CancellationTokenSource();

        private CancellationTokenSource cancelBillAcceptSource;

        private System.Threading.Timer timerBillWait;
        private double inSumm = 0;
        private double price = 0;
        Tuple<string, byte> cardReadInfo = null;
        private const int tryCount = 3;
        private bool cardDetected = false;
        private bool cardReaded = false;
        private bool cardValid = false;
        public MainForm()
        {
            InitializeComponent();
           // Console.WriteLine(CommonFunctions.StringToByteArray(11.ToString())[0]);
        }
        private async Task LoadConfiguration(string fileName)
        {
            await Task.Run(async () =>
            {
                try
                {
                    Type t;
                    Assembly asm;
                    XmlDocument config = new XmlDocument();
                    config.Load(fileName);
                    XmlNodeList deviceConfig;

                    deviceConfig = config.SelectNodes(@"//device[@devicename='RFIDLib.RFID']");
                    asm = Assembly.LoadFrom(deviceConfig[0].Attributes["libpath"].Value);
                    t = asm.GetType(deviceConfig[0].Attributes["devicename"].Value, true, true);
                    rfidReader = (ICardReader)Activator.CreateInstance(t);


                    rfidReader.SetConfiguration(deviceConfig[0].Attributes["port"].Value,
                                                Int32.Parse(deviceConfig[0].Attributes["boudrate"].Value),
                                                (Parity)Enum.Parse(typeof(Parity), deviceConfig[0].Attributes["parity"].Value),
                                                Int32.Parse(deviceConfig[0].Attributes["bits"].Value),
                                                (StopBits)Enum.Parse(typeof(StopBits), deviceConfig[0].Attributes["stopbits"].Value)
                                                );
                    rfidReader.Open();

                    deviceConfig = config.SelectNodes(@"//device[@devicename='RFIDLib.DispenserMT166']");
                    asm = Assembly.LoadFrom(deviceConfig[0].Attributes["libpath"].Value);
                    t = asm.GetType(deviceConfig[0].Attributes["devicename"].Value, true, true);
                    dispenser = (IDispenser)Activator.CreateInstance(t);
                    dispenser.SetConfiguration(deviceConfig[0].Attributes["port"].Value,
                                                Int32.Parse(deviceConfig[0].Attributes["boudrate"].Value),
                                                (Parity)Enum.Parse(typeof(Parity), deviceConfig[0].Attributes["parity"].Value),
                                                Int32.Parse(deviceConfig[0].Attributes["bits"].Value),
                                                (StopBits)Enum.Parse(typeof(StopBits), deviceConfig[0].Attributes["stopbits"].Value)
                                                );

                    //  dispenser.Open();

                    deviceConfig = config.SelectNodes(@"//device[@devicename='BillAcceptorUBA10_SS.BillAceptor']");
                    asm = Assembly.LoadFrom(deviceConfig[0].Attributes["libpath"].Value);
                    t = asm.GetType(deviceConfig[0].Attributes["devicename"].Value, true, true);
                    billValidator = (IBillValidator)Activator.CreateInstance(t);
                    billValidator.SetConfiguration(deviceConfig[0].Attributes["port"].Value,
                                                Int32.Parse(deviceConfig[0].Attributes["boudrate"].Value),
                                                (Parity)Enum.Parse(typeof(Parity), deviceConfig[0].Attributes["parity"].Value),
                                                Int32.Parse(deviceConfig[0].Attributes["bits"].Value),
                                                (StopBits)Enum.Parse(typeof(StopBits), deviceConfig[0].Attributes["stopbits"].Value)
                                                );
                    billValidator.Open();
                    billValidator.BillAccept += Bill_BillAccept;
                    await billValidator.StartCommunication(cancelBillTokenSource);
                    ShowCurrentScreen(tpIdle);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    ShowInfo("АППАРАТ ВРЕМЕННО НЕ РАБОТАЕТ");
                }
            });
        }

        private void RfidReader_ChangeStatus(object sender, EventArgs e)
        {
            var eventArg = e as ReaderEventArgs;
            switch (eventArg.StatusCode)
            {
                //карта в картридере
                case 0x59:
                    {

                        break;
                    };
                case 0x4E:
                    {

                        break;
                    };
                default:
                    {
                        break;
                    };
            }


        }

        private void ShowCurrentScreen(XtraTabPage screen)
        {
            this.Invoke((MethodInvoker)delegate
            {
                tcMain.SelectedTabPage = screen;
            });
        }
        private void ShowInfo(string message)
        {
            this.Invoke((MethodInvoker)delegate
            {
                lbInfo.Text = message.ToUpper();
                tcMain.SelectedTabPage = tpInfo;
            });
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            LoadConfiguration(@"config.xml");
        }

        private async Task StartGetRFCard()
        {
            CancellationTokenSource cancelTokenSource = new CancellationTokenSource(); ;
            CancellationToken token = cancelTokenSource.Token;
   
            await Task.Run(async () =>
            {

                TimerCallback tm = new TimerCallback(TimeOutOperation);
                System.Threading.Timer timer = new System.Threading.Timer(tm, cancelTokenSource, 10000, 10000);
                byte res;
                while (!cancelTokenSource.Token.IsCancellationRequested)
                {
                    res = await rfidReader.GetRFCard(null);
                    if (res == 0x59)
                    {
                        timer.Change(Timeout.Infinite, Timeout.Infinite);
                        cardDetected = true;
                        ShowInfo("КАТРА ОБНАРУЖЕНА");
                        break;
                    }
                }
                if (!cardDetected) return;
                //Несколько попыток чтения
                for (int i = 0; i < tryCount; i++)
                {
                    cardReadInfo = await GetId();
                    if (cardReadInfo.Item2 == 0x59)
                    {
                        ShowInfo("Подожите...");
                        await Task.Delay(3000);
                        cardReaded = true;
                        break;
                    }
                }
                if (!cardReaded) return; //карта не прочитана
                for (int i = 0; i < tryCount; i++)
                {
                    if (ParkingApi.CheckCard(cardReadInfo.Item1))
                    {
                        cardValid = true;
                        ShowInfo("КАТРА ЕСТЬ БАЗЕ");
                        await Task.Delay(1000);
                        break;
                    }
                }
                if (!cardValid)
                {
                    ShowInfo("КАТРЫ нет БАЗЕ");
                    await Task.Delay(1000);
                    return;
                };

                for (int i = 0; i < tryCount; i++)
                {
                    price = ParkingApi.Get_Price(cardReadInfo.Item1);
                    ShowInfo($"стоимость {price}");
                    await Task.Delay(1000);
                    inSumm = 0;
                    await Task.Delay(1000);
                    if (price > 0)
                    {
                        ShowInfo($"Внесите {price} руб.\n Внесено {inSumm} руб.");
                        billValidator.SetInhibit(false);

                        //токен отмены для жидания внесения денег
                        //cancelBillTokenSource = new CancellationTokenSource();
                        // таймер для окончания ожидания приема денег. не вечно же ждать
                        cancelBillAcceptSource = new CancellationTokenSource();
                        TimerCallback tmBillWait = new TimerCallback(TimeOutBillWait);
                        timerBillWait = new System.Threading.Timer(tmBillWait, cancelBillAcceptSource, 30000, 30000);
                        //запуск цикла приема денег

                        await WaitBills(cancelBillAcceptSource.Token);
                        if ((inSumm - price) >= 0)
                        {
                            var payment = ParkingApi.Payment(cardReadInfo.Item1, price, inSumm - price);
                            if (payment)
                            {
                                ShowInfo($"Платеж принят {payment.ToString()}");
                                await Task.Delay(1000);
                            }
                            else
                            {
                                ShowInfo($"Платеж не принят {payment.ToString()}");
                                await Task.Delay(1000);
                            }
                            //сдача
                            ShowInfo($"Сдача  {inSumm - price}");
                            if ( (inSumm - price) > 0)
                                billValidator.DispenceBill(1, 1, 1, 2);
                            await Task.Delay(5000);
                        }
                        else
                        {
                            //если денег не достаточно и мы вывылились по таймауту то возвращаем деньги
                            //сдача
                            ShowInfo($"Сдача  {inSumm}");
                        }
                        await Task.Delay(1000);
                        break;
                    }
                }
                timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
                );
        }
        private async Task  WaitBills(CancellationToken cancel)
        {
            await Task.Run(async () =>
            {
                while ((inSumm < price) && !cancel.IsCancellationRequested)
                {
                    await Task.Delay(100);
                }
            });
        }
        private void TimeOutBillWait(object state)
        {
            billValidator.SetInhibit(true);
            (state as CancellationTokenSource).Cancel();
        }

        private void Bill_BillAccept(object sender, EventArgs e)
        {
            var eventArg = e as BillEventArgs;
            this.Invoke((MethodInvoker)delegate
            {
                timerBillWait.Change(30000, 30000);
                inSumm += eventArg.BillDenomination;
                ShowInfo($"Внесите {price} руб.\n Внесено {inSumm} руб.");
                if (inSumm >= price)
                {
                    timerBillWait.Change(Timeout.Infinite, Timeout.Infinite);
                    billValidator.SetInhibit(true);
                   // cancelBillTokenSource.Cancel();
                }
            });
        }

        private async Task<Tuple<string, byte>> GetId()
        {
            return await Task.Run(async () =>
            {
                byte res;
                res = await rfidReader.VerifyPassword(null);
                res = await rfidReader.ReadSectorData(null);
                return new Tuple<string, byte>(rfidReader.ReadData, res);
            });
        }

        private void TimeOutOperation(object state)
        {
            (state as CancellationTokenSource).Cancel();
        }

        private async void btGetPay_Click(object sender, EventArgs e)
        {
            ShowInfo("ВСТАВЬТЕ КАРТУ");
            await StartGetRFCard();
            ShowCurrentScreen(tpIdle);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {

        }

        private void timer1_Tick_1(object sender, EventArgs e)
        {
  
        }
    }
}


