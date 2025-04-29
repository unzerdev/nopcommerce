namespace Unzer.Plugin.Payments.Unzer.Models
{
    public class CreatePaymentResponse
    {
        public string PaymentId { get; set; }
        public string PaypageId { get; set; }
        public string RedirectUrl { get; set; }

        public bool Success { get; set; }
        public string StatusMessage { get; set; }        
        public string RawContent { get; set; }
    }
}