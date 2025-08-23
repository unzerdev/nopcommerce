using DocumentFormat.OpenXml.Drawing;
using Unzer.Plugin.Payments.Unzer.Infrastructure;

namespace Unzer.Plugin.Payments.Unzer.Models;
public class UnzerPaymentType
{
    public string Name { get; set; }
    public string ShortName { get; set; }
    public string SystemName { get; set; }
    public string UnzerName { get; set; }
    public bool SupportAuthurize { get; set; }
    public bool SupportCharge { get; set; }
    public bool Deprecated { get; set; }
    public bool Prepayment { get; set; }
    public bool IsSupplement { get; set; }
    public string? Supplements { get; set; }
    public PayPageCheckoutType? CheckoutType { get; set; }
    public PaypageInfo PaypageInfo { get; set; }
    public string[] CountryRestrictions { get; set; }
    public string[] CurrencyRestrictions { get; set; }
}

public class PaypageInfo
{
    public PaypageInfo()
    {
        Attributes = Array.Empty<string>();
    }
    public string Name { get; set; }
    public string[] Attributes { get; set; }
}
