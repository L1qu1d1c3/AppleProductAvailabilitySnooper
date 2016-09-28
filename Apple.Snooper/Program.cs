using System;
using System.Configuration;
using System.Linq;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Apple.Snooper
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger("MyLogger");
        private static readonly IMailer Mailer = new Mailer();
        
        static void Main()
        {
            var emailFromArg = ReadConfig();
            var url = ConfigurationManager.AppSettings["RequestUrl"];
            var productModels = ConfigurationManager.AppSettings["ProductModels"].Split(';');
            var storeCode = ConfigurationManager.AppSettings["Store"];
            var location = ConfigurationManager.AppSettings["Location"];

            var httpclient = new AppleHttpClient(url);
            var finalEmail = "";
            foreach (var model in productModels)
            {
                finalEmail += CheckProductAvailability(httpclient, model, storeCode, location);
            }
            SendEmail(emailFromArg,finalEmail);
        }

        private static string CheckProductAvailability(AppleHttpClient httpclient, string model, string storeCode, string location)
        {
            var jsonString = httpclient.CheckiPhoneAvailability(model, location).Result;
            var json = JsonConvert.DeserializeObject(jsonString) as JObject;

            if (json == null)
            {
                Log.Error("Could not read the response data.");
                Console.WriteLine("Could not read Request data.");
                return "";
            }

            var stores = json["body"]["stores"];
            if (stores == null)
            {
                Log.Error("No stores found in the response json.");
                return "";
            }

            var ourStore = storeCode != string.Empty
                ? stores.FirstOrDefault(x => x["storeNumber"].ToString() == storeCode)
                : stores.First();

            if (ourStore == null)
            {
                Console.WriteLine("Store not found. ");
                return "";
            }

            var modelDescription = ourStore["partsAvailability"][model]["storePickupProductTitle"].ToString();
            var storeName = ourStore["storeName"];
            var partsAvailabilityString = ourStore["partsAvailability"][model]["pickupSearchQuote"].ToString();

            if (partsAvailabilityString != Constants.MensajeNoDisponible)
            {
                modelDescription += " en " + storeName + "\n";
                Console.WriteLine(modelDescription);
                Log.Info(modelDescription);
                return modelDescription;
            }
            else
            {
                // retry later?
                Console.WriteLine($"iPhone {modelDescription} not available");
                Log.Info($"iPhone {modelDescription} not available");
                return "";
            }
        }
        private static void SendEmail(EmailConfig emailFromArg, string modelDescription)
        {
            Mailer.Notify(emailFromArg, modelDescription, "");
        }
        private static EmailConfig ReadConfig()
        {
            var emailServer = ConfigurationManager.AppSettings["SmtpServer"];
            var emailFrom = ConfigurationManager.AppSettings["From"];
            var emailDestinations = ConfigurationManager.AppSettings["Destinations"].Split(';');
            var password = ConfigurationManager.AppSettings["Password"];

            return new EmailConfig(emailServer, emailFrom, password, emailDestinations);
        }
    }
}
