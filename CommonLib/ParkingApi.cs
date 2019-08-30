using System;
using System.Net;
using System.Text;
using System.Configuration;
using System.IO;
using Newtonsoft.Json;

namespace CommonLib
{
    public class ApiAnswer
    {
        public string status { get; set; }
        public string text { get; set; }

        public override string ToString()
        {
            return $"{status}: contributions";
        }
    }

    public class ParkingApi
    {
        public static void SetStatus(int TerminalId, int StatusId) //записываем статус терминала
        {
            ApiAnswer Answer = GetApi("api/set_status/" + TerminalId.ToString() + "/" + StatusId.ToString());
        }

        public static Boolean CheckCard(String Number) //проверяем карту на наличие в базе
        {
            ApiAnswer Answer = GetApi("api/check_card_id/" + Number);
            if (Answer.status.Equals("success")) return true;
            return false;
        }

        public static String Get_Enter_Type()
        {
            ApiAnswer Answer = GetApi("api/get_enter_type/");  //проверяем тип вьезда и выезда
            return Answer.text;
        }

        public static Boolean Enter_Car(String id)
        {
            ApiAnswer Answer = GetApi("api/enter_car/", "id=" + id);  //ввозим машину по карте и проверяем есть ли карта можно ли по ней вьехать
            if (Answer.status.Equals("OK")) return true;
            return false;
        }

        public static Boolean Outer_Car(String id)
        {
            ApiAnswer Answer = GetApi("api/outer_car/", "id=" + id);  //вывозим машину по карте и проверяем есть ли карта можно ли по ней вьехать
            if (Answer.status.Equals("OK")) return true;
            return false;
        }

        public static double Get_Price(String id)
        {
            ApiAnswer Answer = GetApi("api/get_price/", "id=" + id);  //получаем стоимость по карте
            if (Answer.status.Equals("success")) return Convert.ToDouble(Answer.text);
            return -1; //значит ошибка

        }

        public static Boolean Payment(String id, double price, double change)
        {
            ApiAnswer Answer = GetApi("api/payment/", "id=" + id + "&price=" + price.ToString() + "&change=" + change.ToString());  //получаем стоимость по карте
            if (Answer.status.Equals("success")) return true;
            return false; //значит ошибка
        }

        public static String GetNewCard()
        {
            ApiAnswer Answer = GetApi("api/generate_card");
            return Answer.text;
        }
        public static void AddLog(String text)
        {
            ApiAnswer Answer = GetApi("api/addlog", "log=" + text);
        }

        public static ApiAnswer GetApi(String get, String requestBody = "")
        {
            Configuration configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            ApiAnswer Answer = new ApiAnswer();


            string url = configFile.AppSettings.Settings["ApiHost"].Value;
            try
            {

                var request = (HttpWebRequest)WebRequest.Create(new Uri(url + get));
                if (request == null) return Answer;
                if (requestBody.Length > 0) requestBody = requestBody + "&token=" + configFile.AppSettings.Settings["ApiToken"].Value;
                else requestBody = "token=" + configFile.AppSettings.Settings["ApiToken"].Value;
                var data = Encoding.UTF8.GetBytes(requestBody);

                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = data.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                var response = (HttpWebResponse)request.GetResponse();

                var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                File.WriteAllText("answer.txt", responseString);
                string _byteOrderMarkUtf8 =  Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());
                if (responseString.StartsWith(_byteOrderMarkUtf8, StringComparison.Ordinal))
                {
                    responseString = responseString.Remove(0, _byteOrderMarkUtf8.Length);
                }
                Answer = JsonConvert.DeserializeObject<ApiAnswer>(responseString);
                return Answer;
            }
            catch (Exception ex)
            {
                File.WriteAllText("error.txt", $"internet_error{ex.ToString()}");
                Answer.status = $"internet_error{ex.ToString()}";
                Answer.text = "";
                return Answer;
            }
        }

    }
}
