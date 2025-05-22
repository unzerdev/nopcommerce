using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Unzer.Plugin.Payments.Unzer.Infrastructure;
using Unzer.Plugin.Payments.Unzer.Models;
using Unzer.Plugin.Payments.Unzer.Models.Api;

namespace Unzer.Plugin.Payments.Unzer.Services
{
    public interface IUnzerApiService
    {
        UnzerPaymentSettings UnzerPaymentSettings { set; }
        Task<CreatePaymentResponse> CreateAuthPayment(Order order, bool isRecurring, string unzerCustomerId, string basketId);
        Task<CreatePaymentResponse> CreateCapturePayment(Order order, bool isRecurring, string unzerCustomerId, string basketId);
        Task<PaymentCaptureResponse> CreatePrepayment(Order order, string unzerCustomerId, string basketId);
        Task<PaymentApiStatus> CapturePayment(Order order, decimal capturreAmount);
        Task<PaymentApiStatus> CaptureSubPayment(Order order, decimal capturreAmount);
        Task<PaymentApiStatus> RefundPayment(Order order, decimal refundAmount);
        Task<PaymentApiStatus> CancelPayment(Order order, decimal refundAmount);
        Task<PaymentApiStatus> CancelChargePayment(Order order, decimal cancelAmount);
        Task<PayPageResponse> GetPayPage(string payPageId);

        Task<PaymentApiStatus> GetCustomer(string customerId);
        Task<PaymentApiStatus> CreateCustomer(Customer customer, Address billingAddress, Address shippingAddress);
        Task<PaymentApiStatus> UpdateCustomer(string unserCustomerId, Customer customer, Address billingAddress, Address shippingAddress);

        Task<PaymentApiStatus> CreateBasket(Order order);
        Task<PaymentApiStatus> CreateV2Basket(Order order);

        Task<PaymentApiStatus> CreateMetadata();
        Task<PaymentApiStatus> UpdateMetadata(string metadataId);
        Task<PaymentApiStatus> DeleteMetadata(string metadataId);

        Task<SetWebHooksResponse> SetWebHookEventAsync(string callbackUrl, List<string> eventTypes);
        Task<SetWebHookResponse> SetWebHookEvenAsync(string callbackUrl, WebHookEventType eventType);
        Task<SetWebHooksResponse> GetWebHookEventsAsync();
        Task<SetWebHooksResponse> DeleteWebHookEventsAsync();

        Task<GetKeyPairResponse> GetKeyPairAsync();

        Task<PaymentCaptureResponse> PaymentAuthorizedResponse(string paymentId);
        Task<PaymentCaptureResponse> PaymentCaptureResponse(string paymentId, string chargeId);

        bool IsConfigured(UnzerPaymentSettings unzerSettings);
    }
}
