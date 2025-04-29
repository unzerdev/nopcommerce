namespace Unzer.Plugin.Payments.Unzer.Models.Api;
public class PayPageResponse : UnzerApiResponse
{
    public string paypageId { get; set; }
    public string redirectUrl { get; set; }
}
