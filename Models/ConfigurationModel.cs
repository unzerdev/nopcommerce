using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;
using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Mvc;
using Unzer.Plugin.Payments.Unzer.Infrastructure;

namespace Unzer.Plugin.Payments.Unzer.Models
{
    public record ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Unzer.Fields.UnzerApiBaseUrl")]
        public string UnzerApiBaseUrl { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.Unzer.Fields.UnzerApiKey")]
        [NoTrim]
        [DataType(DataType.Password)]        
        public string UnzerApiKey { get; set; }
        public string UnzerPublicApiKey { get; set; }

        public bool UnzerApiKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Unzer.Fields.ShopUrl")]
        public string ShopUrl { get; set; }

        public bool ShopUrl_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Unzer.Fields.LogoImage")]
        public string LogoImage { get; set; }

        public bool LogoImage_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Unzer.Fields.ShopDescription")]
        public string ShopDescription { get; set; }

        public bool ShopDescription_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Unzer.Fields.TagLine")]
        public string TagLine { get; set; }

        public bool TagLine_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Unzer.Fields.PaymentMethods")]
        public IList<string> SelectedPaymentTypes { get; set; }
        public bool PaymentMethods_OverrideForStore { get; set; }

        public SelectList AvailablePaymentMethods { get; set; }
        public IList<string> AvailablePaymentTypes { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Unzer.Fields.LogCallbackPostData")]
        public bool LogCallbackPostData { get; set; }

        public bool LogCallbackPostData_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Unzer.Fields.SkipPaymentInfo")]
        public bool SkipPaymentInfo { get; set; }

        public bool SkipPaymentInfo_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Unzer.Fields.AutoCapture")]
        public AutoCapture AutoCapture { get; set; }

        public bool AutoCapture_OverrideForStore { get; set; }
        public SelectList AutoCaptureOptions { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Unzer.Fields.CurrencyCode")]
        public string CurrencyCode { get; set; }

        public bool CurrencyCode_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Unzer.Fields.AdditionalFeePercentage")]
        public decimal AdditionalFeePercentage { get; set; }

        public bool AdditionalFeePercentage_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Unzer.Fields.SendOrderConfirmOnAuthorized")]
        public bool SendOrderConfirmOnAuthorized { get; set; }
        public bool SendOrderConfirmOnAuthorized_OverrideForStore { get; set; }
    }
}