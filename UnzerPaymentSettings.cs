using Nop.Core.Configuration;
using Unzer.Plugin.Payments.Unzer.Infrastructure;

namespace Unzer.Plugin.Payments.Unzer
{
    public class UnzerPaymentSettings : ISettings
    {
        public string UnzerApiBaseUrl { get; set; }
        public string UnzerTokenUrl { get; set; }
        public string UnzerPaypageApiUrl { get; set; }        
        public string UnzerApiKey { get; set; }
        public string UnzerPublicApiKey { get; set; }
        public bool LogCallbackPostData { get; set; }
        public string ShopUrl { get; set; }
        public string LogoImage { get; set; }
        public string ShopDescription { get; set; }
        public string TagLine { get; set; }
        public List<string> SelectedPaymentTypes { get; set; }
        public List<string> AvailablePaymentTypes { get; set; }
        public bool SkipPaymentInfo { get; set; }
        public bool EnforceCountryRestriction { get; set; }
        public bool EnforceCurrencyRestriction { get; set; }
        public string CurrencyCode { get; set; }
        public decimal AdditionalFeePercentage { get; set; }
        public AutoCapture AutoCapture { get; set; }
        public bool SendOrderConfirmOnAuthorized { get; set; }

        public bool UnzerWebHooksSet { get; set; }
        public string UnzerMetadataId { get; set; }
    }
}
