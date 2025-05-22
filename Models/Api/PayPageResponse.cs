namespace Unzer.Plugin.Payments.Unzer.Models.Api;
public class PayPageResponse : UnzerApiResponse
{
    public string paypageId { get; set; }
    public string redirectUrl { get; set; }

    public PayPagePayment Payments { get; set; }
}

public class PayPagePayment
{
    public string PaymentId { get; set; }
    public string SessionId { get; set; }
    public string TransactionStatus { get; set; }
    public DateTime CreationDate { get; set; }
    public Message[] Messages { get; set; }
}

