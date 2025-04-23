using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Unzer.Plugin.Payments.Unzer.Infrastructure;

namespace Unzer.Plugin.Payments.Unzer.Models.Api
{
    public class CreatePayPageRequest : UnzerApiRequest
    {
        public PayPageMode mode { get; set; }
        public decimal amount { get; set; }
        public string currency { get; set; }
        public PayPageType type { get; set; }
        public PayPageRecurrenceType recurrenceType { get; set; }
        public string orderId { get; set; }
        public bool card3ds { get; set; }
        public string invoiceId { get; set; }
        public string shopName { get; set; }
        public Resources resources { get; set; }
        public string paymentReference { get; set; }
        public PayPageCheckoutType checkoutType { get; set; }
        public Urls urls { get; set; }
        public Style style { get; set; }
        public Paymentmethodsconfigs[] paymentMethodsConfigs { get; set; }
        public Riskdata risk { get; set; }
        public Customersettings customerSettings { get; set; }
        public string alias { get; set; }
        public bool multiUse { get; set; }
        public DateTime expiresAt { get; set; }
        public AmountSettings amountSettings { get; set; }

        [JsonIgnore]
        public override string BaseUrl => UnzerPaymentDefaults.UnzerPaypageApiUrl;

        [JsonIgnore]
        public override string Path => "v2/merchant/paypage";

        [JsonIgnore]
        public override string Method => HttpMethods.Post;
    }

    public class Urls
    {
        public string termsAndCondition { get; set; }
        public string privacyPolicy { get; set; }
        public string imprint { get; set; }
        public string help { get; set; }
        public string contact { get; set; }
        public string returnSuccess { get; set; }
        public string returnPending { get; set; }
        public string returnFailure { get; set; }
        public string returnCancel { get; set; }
    }

    public class Style
    {
        public string logoImage { get; set; }
        public string backgroundColor { get; set; }
        public string footerColor { get; set; }
        public string headerColor { get; set; }
        public string linkColor { get; set; }
        public string textColor { get; set; }
        public string brandColor { get; set; }
        public string cornerRadius { get; set; }
        public string hideUnzerLogo { get; set; }
        public string backgroundImage { get; set; }
        public string font { get; set; }
        public string shadows { get; set; }
        public string favicon { get; set; }
    }

    public class Paymentmethodsconfigs
    {
        public string name{ get; set; }
        public bool enabled { get; set; }
        public int order { get; set; }
    }

    public class Customersettings
    {
        public CustomerType type { get; set; }
        public string[] allowedCountries { get; set; }
    }

    public class AmountSettings
    {
        public string minimum { get; set; }
        public string maximum { get; set; }
    }

}
