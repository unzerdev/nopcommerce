using System.Text;
using System.Text.Json;
using Nop.Core.Infrastructure;
using Unzer.Plugin.Payments.Unzer.Infrastructure;
using Unzer.Plugin.Payments.Unzer.Models;

namespace Unzer.Plugin.Payments.Unzer
{
    public class UnzerPaymentDefaults
    {
        private static INopFileProvider _fileProvider = EngineContext.Current.Resolve<INopFileProvider>();
        private static UnzerPaymentSettings _unzerPaymentSettings = EngineContext.Current.Resolve<UnzerPaymentSettings>();
        private static readonly string _unzerPaymentTypesFile = _fileProvider.MapPath("~/Plugins/Payments.Unzer/UnzerPaymentTypes.json");
        public static List<UnzerPaymentType> UnzerPaymentTypes = JsonSerializer.Deserialize<List<UnzerPaymentType>>(_fileProvider.ReadAllText(_unzerPaymentTypesFile, Encoding.UTF8));

        public static string SystemName => "Payments.Unzer";

        public static string MetadataPluginType = "unzer/nopcommerce";
        public static string MetadataShopType = "NopCommerce";

        public static string UnzerApiUrl => _unzerPaymentSettings.UnzerApiBaseUrl ?? "https://api.unzer.com/";
        public static string UnzerPaypageApiUrl => _unzerPaymentSettings.UnzerPaypageApiUrl ?? "https://paypage.test.unzer.com/";
        public static string UnzerTokenUrl => _unzerPaymentSettings.UnzerTokenUrl ?? "https://token.upcgw.com/v1/auth/token";
        
        public static string[] AllowedUrls = new string[] { "api.unzer.com", "api.heidelpay.com" };
        public static string ConfigurationRouteName => "Plugin.Payments.Unzer.Configure";
        public static string DefaultApiKeySetting = "<Unzer Private Key>";

        public static string PaymentPluginManagerOverride = "ObjectaData.Plugin.Payments.Unzer.Services.UnzerPaymentPluginManager";

        public static string CallBackUrlRouteName = "Plugin.Payments.Unzer.CallbackHandler";
        public static string UnzerPaymentStatusRouteName = "Plugin.Payments.Unzer.UnzerPaymentCompleted";
        public static string UnzerPrePaymentComplteRouteName = "Plugin.Payments.Unzer.UnzerPrePaymentCompleted";
        public static string UnzerCancelOrderRouteName = "Plugin.Payments.Unzer.UnzerCancelOrder";

        public static string DevCallbackUrl = "https://webhook-test.com/25e2a12f616f335f7eaf8843a3de5e57";
        public static WebHookEventType[] CallbackEvents = new WebHookEventType[] { WebHookEventType.authorize, WebHookEventType.charge };
        public static string[] IgnoreCallbackEvents = new string[] { "authorize.failed", "authorize.pending", "authorize.canceled", "charge.failed", "charge.canceled" };

        public static string[] UseBearerTokenUrls = new string[] { "v2/merchant/paypage" };

        public static string PrePaymentInstructionAttribute => "UnzerPrePaymentsInstruction";

        public static string MapPaymentType(string paymentTypes)
        {
            string mappedType = paymentTypes;

            var anyType = UnzerPaymentTypes.FirstOrDefault(t => paymentTypes.Contains(t.ShortName));
            if (anyType == null)
                return mappedType;

            mappedType = anyType.Name;

            return mappedType;
        }

        public static string MapPaymentTypeName(string paymentType)
        {
            string mappedTypeName = paymentType;

            var anyType = UnzerPaymentTypes.SingleOrDefault(t => t.UnzerName == paymentType);
            if (anyType == null)
                return mappedTypeName;

            mappedTypeName = anyType.Name;

            return mappedTypeName;
        }

        public static UnzerPaymentType ReadUnzerPaymentType(string paymentSystemName)
        {
            var unzerPaymentType = UnzerPaymentTypes.SingleOrDefault(t => t.SystemName == paymentSystemName);
            if (unzerPaymentType == null)
            {
                return new UnzerPaymentType
                {
                    Name = "Unzer",
                    ShortName = "unz",
                    SystemName = paymentSystemName,
                    SupportAuthurize = true,
                    SupportCharge = true,
                    Deprecated = false
                };
            }                

            return unzerPaymentType;
        }
    }
}
