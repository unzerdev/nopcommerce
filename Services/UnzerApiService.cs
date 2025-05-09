using Nop.Core.Domain.Orders;
using Unzer.Plugin.Payments.Unzer.Models;
using Unzer.Plugin.Payments.Unzer.Models.Api;
using Unzer.Plugin.Payments.Unzer.Infrastructure;
using Nop.Services.Logging;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Common;
using Microsoft.CodeAnalysis;

namespace Unzer.Plugin.Payments.Unzer.Services
{
    public class UnzerApiService : IUnzerApiService
    {
        private readonly UnzerApiHttpClient _unzerApiHttpClient;
        private readonly UnzerPaymentRequestBuilder _unzerPayRequestBuilder;
        private readonly ILogger _logger;

        public UnzerApiService(UnzerApiHttpClient httpClient, UnzerPaymentRequestBuilder unzerPayRequestBuilder, ILogger logger)
        {
            _unzerApiHttpClient = httpClient;  
            _unzerPayRequestBuilder = unzerPayRequestBuilder;
            _logger = logger;
        }

        public UnzerPaymentSettings UnzerPaymentSettings
        {
            set
            {
                _unzerApiHttpClient.UnzerPaymentSettings = value;
            }
        }

        public async Task<CreatePaymentResponse> CreateAuthPayment(Order order, bool isRecurring, string unzerCustomerId, string basketId)
        {
            var status = new CreatePaymentResponse { Success = false, StatusMessage = string.Empty};
            var authPayPageReq = await _unzerPayRequestBuilder.BuildV2AuthorizePayPageRequestAsync(order, isRecurring, unzerCustomerId, basketId);

            var response = await _unzerApiHttpClient.RequestAsync<CreatePayPageRequest, PayPageResponse>(authPayPageReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse != null && response.ErrorResponse.FieldErrors.Any() ? string.Join(",", response.ErrorResponse.FieldErrors.Select(e => $"{e.Field} {e.Message}")) : string.Empty;
                await _logger.ErrorAsync($"UnzerApiService.CreateAuthPayment(V2): Failed with call to Unzer API Client with {errMsg}");
                status.StatusMessage = errMsg;
                status.Success = response.IsSuccess;
                return status;
            }

            status.Success = response.IsSuccess;
            status.StatusMessage = "Payment created successfull";
            status.RedirectUrl = response.redirectUrl;
            //status.PaymentId = response.resources.paymentId;
            return status;
        }

        public bool IsConfigured(UnzerPaymentSettings unzerSettings)
        {
            return !string.IsNullOrEmpty(unzerSettings?.UnzerApiKey) && !string.IsNullOrEmpty(unzerSettings?.UnzerApiKey) && unzerSettings.UnzerApiKey != UnzerPaymentDefaults.DefaultApiKeySetting;
        }


        public async Task<CreatePaymentResponse> CreateCapturePayment(Order order, bool isRecurring, string unzerCustomerId, string basketId)
        {
            var status = new CreatePaymentResponse { Success = false, StatusMessage = string.Empty };
            var authPayPageReq = await _unzerPayRequestBuilder.BuildV2CapturePayPageRequestAsync(order, isRecurring, unzerCustomerId, basketId);

            var response = await _unzerApiHttpClient.RequestAsync<CreatePayPageRequest, PayPageResponse>(authPayPageReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse != null && response.ErrorResponse.FieldErrors.Any() ? string.Join(",", response.ErrorResponse.FieldErrors.Select(e => $"{e.Field} {e.Message}")) : string.Empty;
                await _logger.ErrorAsync($"UnzerApiService.CreateCapturePayment: Failed with call to Unzer API Client with {errMsg}");
                status.StatusMessage = errMsg;
                status.Success = response.IsSuccess;
                return status;
            }

            status.Success = response.IsSuccess;
            status.StatusMessage = "Payment created successfull";
            status.RedirectUrl = response.redirectUrl;
            //status.PaymentId = response.resources.paymentId;
            return status;
        }

        public async Task<PaymentApiStatus> CapturePayment(Order order, decimal capturreAmount)
        {
            var status = new PaymentApiStatus { Success = false, StatusMessage = string.Empty };
            var authPayPageReq = await _unzerPayRequestBuilder.BuildCaptureRequestAsync(order, capturreAmount);

            var response = await _unzerApiHttpClient.RequestAsync<CreateCaptureRequest, PaymentCaptureResponse>(authPayPageReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.CapturePayment: Failed with call to Unzer API Client with {errMsg}");
                status.StatusMessage = errMsg;
                status.Success = response.IsSuccess;
                return status;
            }

            status.Success = response.IsSuccess;
            status.StatusMessage = "Payment captured successfull";
            status.ResponseId = $"{response.resources.paymentId}/{response.Id}";
            return status;
        }

        public async Task<PaymentApiStatus> CaptureSubPayment(Order order, decimal capturreAmount)
        {
            var status = new PaymentApiStatus { Success = false, StatusMessage = string.Empty };
            var authPayPageReq = await _unzerPayRequestBuilder.BuildSubCaptureRequestAsync(order, capturreAmount);

            var response = await _unzerApiHttpClient.RequestAsync<CreateSubCaptureRequest, PaymentCaptureResponse>(authPayPageReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.CaptureSubPayment: Failed with call to Unzer API Client with {errMsg}");
                status.StatusMessage = errMsg;
                status.Success = response.IsSuccess;
                return status;
            }

            status.Success = response.IsSuccess;
            status.StatusMessage = "Sub. Payment captured successfull";
            status.ResponseId = $"{response.resources.paymentId}/{response.Id}";
            return status;
        }

        public async Task<PaymentApiStatus> RefundPayment(Order order, decimal refundAmount)
        {
            var status = new PaymentApiStatus { Success = false, StatusMessage = string.Empty };
            var refundPayPageReq = await _unzerPayRequestBuilder.BuildRefundRequestAsync(order, refundAmount);

            var response = await _unzerApiHttpClient.RequestAsync<CreateRefundRequest, PaymentRefundResponse>(refundPayPageReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.RefundPayment: Failed with call to Unzer API Client with {errMsg}");
                status.StatusMessage = errMsg;
                status.Success = response.IsSuccess;
                return status;
            }

            status.Success = response.IsSuccess;
            status.StatusMessage = "Payment refund successfull";
            status.ResponseId = $"{response.resources.paymentId}/{response.Id}";
            return status;;
        }

        public async Task<PaymentApiStatus> CancelPayment(Order order, decimal cancelAmount)
        {
            var status = new PaymentApiStatus { Success = false, StatusMessage = string.Empty };
            var cancelPayPageReq = await _unzerPayRequestBuilder.BuildCancelRequestAsync(order, cancelAmount);

            var response = await _unzerApiHttpClient.RequestAsync<CreateCancelAuthorizedRequest, PaymentCancelResponse>(cancelPayPageReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.CancelPayment: Failed with call to Unzer API Client with {errMsg}");
                status.StatusMessage = errMsg;
                status.Success = response.IsSuccess;
                return status;
            }

            status.Success = response.IsSuccess;
            status.StatusMessage = "Payment cancel successfull";
            status.ResponseId = $"{response.resources.paymentId}/{response.Id}";
            return status;
        }

        public async Task<PaymentApiStatus> CancelChargePayment(Order order, decimal cancelAmount)
        {
            var status = new PaymentApiStatus { Success = false, StatusMessage = string.Empty };
            var cancelPayPageReq = await _unzerPayRequestBuilder.BuildCancelChargeRequestAsync(order, cancelAmount);

            var response = await _unzerApiHttpClient.RequestAsync<CreateCancelChargedRequest, PaymentCancelResponse>(cancelPayPageReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.CancelChargePayment: Failed with call to Unzer API Client with {errMsg}");
                status.StatusMessage = errMsg;
                status.Success = response.IsSuccess;
                return status;
            }

            status.Success = response.IsSuccess;
            status.StatusMessage = "Payment cancel successfull";
            status.ResponseId = $"{response.resources.paymentId}/{response.Id}";
            return status;
        }

        public async Task<PaymentApiStatus> CreateCustomer(Customer customer, Address billingAddress, Address shippingAddress)
        {
            var status = new PaymentApiStatus { Success = false, StatusMessage = string.Empty };
            var createCustReq = await _unzerPayRequestBuilder.BuildCreateCustomerRequestAsync(customer, billingAddress, shippingAddress);

            var response = await _unzerApiHttpClient.RequestAsync<CreateCustomerRequest, CreateCustomerResponse>(createCustReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.CreateCustomer: Failed with call to Unzer API Client with {errMsg}");
                status.StatusMessage = errMsg;
                status.Success = response.IsSuccess;
                return status;
            }

            status.Success = response.IsSuccess;
            status.StatusMessage = "Customer creation successfull";
            status.ResponseId = response.Id;
            return status;
        }

        public async Task<PaymentApiStatus> UpdateCustomer(string unserCustomerId, Customer customer, Address billingAddress, Address shippingAddress)
        {
            var status = new PaymentApiStatus { Success = false, StatusMessage = string.Empty };
            var updateCustReq = await _unzerPayRequestBuilder.BuildUpdateCustomerRequestAsync(unserCustomerId, customer, billingAddress, shippingAddress);

            var response = await _unzerApiHttpClient.RequestAsync<UpdateCustomerRequest, CreateCustomerResponse>(updateCustReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.UpdateCustomer: Failed with call to Unzer API Client with {errMsg}");
                status.StatusMessage = errMsg;
                status.Success = response.IsSuccess;
                return status;
            }

            status.Success = response.IsSuccess;
            status.StatusMessage = "Customer updated successfull";
            status.ResponseId = response.Id;
            return status;
        }

        public async Task<PaymentApiStatus> GetCustomer(string customerId)
        {
            var status = new PaymentApiStatus { Success = false, StatusMessage = string.Empty };
            var getCustReq = new GetCustomerRequest
            {
                customerId = customerId
            };

            var response = await _unzerApiHttpClient.RequestAsync<GetCustomerRequest, CreateCustomerResponse>(getCustReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.GetCustomer: Failed with call to Unzer API Client with {errMsg}");
                status.StatusMessage = errMsg;
                return status;
            }

            status.ResponseId = response.Id;
            status.StatusMessage = "Customer found";
            status.Success = true;

            return status;
        }

        public async Task<PaymentCaptureResponse> PaymentAuthorizedResponse(string paymentId)
        {
            var getPayAuthReq = await _unzerPayRequestBuilder.BuildPaymentAuthorizeRequestAsync(paymentId);

            var response = await _unzerApiHttpClient.RequestAsync<GetPaymentAutorizeRequest, PaymentCaptureResponse>(getPayAuthReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.PaymentAuthorizedResponse: Failed with call to Unzer API Client with {errMsg}");
            }

            return response;
        }

        public async Task<PaymentCaptureResponse> PaymentCaptureResponse(string paymentId, string chargeId)
        {
            var getPayCaptReq = await _unzerPayRequestBuilder.BuildPaymentCaptureRequestAsync(paymentId, chargeId);

            var response = await _unzerApiHttpClient.RequestAsync<GetPaymentCaptureRequest, PaymentCaptureResponse>(getPayCaptReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.PaymentCaptureResponse: Failed with call to Unzer API Client with {errMsg}");
            }

            return response;
        }

        public async Task<SetWebHooksResponse> GetWebHookEventsAsync()
        {
            var getWebHoolEvents = new GetWebHooksRequest();

            var response = await _unzerApiHttpClient.RequestAsync<GetWebHooksRequest, SetWebHooksResponse>(getWebHoolEvents);
            if (response.IsError)
            {
                response.Events = new List<WebHookEvent>();
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.GetWebHookEventsAsync: Failed with call to Unzer API Client with {errMsg}");
            }

            return response;
        }

        public async Task<SetWebHooksResponse> DeleteWebHookEventsAsync()
        {
            var curWebHooks = await GetWebHookEventsAsync();

            if (curWebHooks.IsError)
                return curWebHooks;

            foreach (var webHook in curWebHooks.Events)
            {
                var deleteWebHoolEvents = new DeleteWebHooksRequest
                {
                    webHookId = webHook.Id
                };

                var response = await _unzerApiHttpClient.RequestAsync<DeleteWebHooksRequest, SetWebHooksResponse>(deleteWebHoolEvents);
                if (response.IsError)
                {
                    response.Events = new List<WebHookEvent>();
                    var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                    await _logger.ErrorAsync($"UnzerApiService.DeleteWebHookEventsAsync: Failed with call to Unzer API Client with {errMsg}");
                }

                return response;
            }

            return curWebHooks;
        }

        public async Task<SetWebHookResponse> SetWebHookEvenAsync(string callbackUrl, WebHookEventType eventType)
        {
            var setWebHookReq = await _unzerPayRequestBuilder.BuildWebHookRequestAsync(callbackUrl, eventType);

            var response = await _unzerApiHttpClient.RequestAsync<SetWebHookRequest, SetWebHookResponse>(setWebHookReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.SetWebHookEvent: Failed with call to Unzer API Client with {errMsg}");
            }
         
            return response;
        }

        public async Task<SetWebHooksResponse> SetWebHookEventAsync(string callbackUrl, List<string> eventTypes)
        {
            var setWebHookReq = await _unzerPayRequestBuilder.BuildWebHookRequestAsync(callbackUrl, eventTypes);

            var response = await _unzerApiHttpClient.RequestAsync<SetWebHookRequest, SetWebHooksResponse>(setWebHookReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.SetWebHookEvent: Failed with call to Unzer API Client with {errMsg}");
            }

            return response;
        }

        public async Task<PaymentApiStatus> CreateMetadata()
        {
            var status = new PaymentApiStatus { Success = false, StatusMessage = string.Empty };
            var metaReq = await _unzerPayRequestBuilder.BuildCreateMetadataRequestAsync();

            var response = await _unzerApiHttpClient.RequestAsync<CreateMetadataRequest, UnzerApiResponse>(metaReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.CreateMetadata: Failed with call to Unzer API Client with {errMsg}");
                status.StatusMessage = errMsg;
                return status;
            }

            status.ResponseId = response.Id;
            status.StatusMessage = "Metadata created successfull";
            status.Success = true;

            return status;
        }

        public async Task<PaymentApiStatus> UpdateMetadata(string metadataId)
        {
            var status = new PaymentApiStatus { Success = false, StatusMessage = string.Empty };
            var metaReq = await _unzerPayRequestBuilder.BuildUpdateMetadataRequestAsync(metadataId);

            var response = await _unzerApiHttpClient.RequestAsync<UpdateMetadataRequest, UnzerApiResponse>(metaReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.UpdateMetadata: Failed with call to Unzer API Client with {errMsg}");
                status.StatusMessage = errMsg;
                return status;
            }

            status.ResponseId = response.Id;
            status.StatusMessage = "Metadata updated successfull";
            status.Success = true;

            return status;
        }

        public async Task<PaymentApiStatus> DeleteMetadata(string metadataId)
        {
            var status = new PaymentApiStatus { Success = false, StatusMessage = string.Empty };
            var metaReq = await _unzerPayRequestBuilder.BuildUpdateMetadataRequestAsync(metadataId);

            metaReq.pluginVersion = null;
            metaReq.shopVersion = null;
            metaReq.pluginType = null;
            metaReq.shopType = null;

            var response = await _unzerApiHttpClient.RequestAsync<UpdateMetadataRequest, UnzerApiResponse>(metaReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.DeleteMetadata: Failed with call to Unzer API Client with {errMsg}");
                status.StatusMessage = errMsg;
                return status;
            }

            status.ResponseId = response.Id;
            status.StatusMessage = "Metadata updated successfull";
            status.Success = true;

            return status;
        }

        public async Task<PaymentApiStatus> CreateBasket(Order order)
        {
            var status = new PaymentApiStatus { Success = false, StatusMessage = string.Empty };
            var basketReq = await _unzerPayRequestBuilder.BuildCreateBasketRequestAsync(order);

            var response = await _unzerApiHttpClient.RequestAsync<CreateBasketRequest, UnzerApiResponse>(basketReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.CreateBasket: Failed with call to Unzer API Client with {errMsg}");
                status.StatusMessage = errMsg;
                return status;
            }

            status.ResponseId = response.Id;
            status.StatusMessage = "Basket created successfull";
            status.Success = true;

            return status;
        }

        public async Task<PaymentApiStatus> CreateV2Basket(Order order)
        {
            var status = new PaymentApiStatus { Success = false, StatusMessage = string.Empty };
            var basketReq = await _unzerPayRequestBuilder.BuildCreateV2BasketRequestAsync(order);

            var response = await _unzerApiHttpClient.RequestAsync<CreateV2BasketRequest, UnzerApiResponse>(basketReq);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.CreateV2Basket: Failed with call to Unzer API Client with {errMsg}");
                status.StatusMessage = errMsg;
                return status;
            }

            status.ResponseId = response.Id;
            status.StatusMessage = "Basket created successfull";
            status.Success = true;

            return status;
        }


        public async Task<GetKeyPairResponse> GetKeyPairAsync()
        {
            var getKeyPairEvents = new GetKeyPairRequest();

            var response = await _unzerApiHttpClient.RequestAsync<GetKeyPairRequest, GetKeyPairResponse>(getKeyPairEvents);
            if (response.IsError)
            {
                var errMsg = response.ErrorResponse.Errors.Any() ? string.Join(",", response?.ErrorResponse .Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.GetKeyPairAsync: Failed with call to Unzer API Client with {errMsg}");
            }

            return response;
        }

        public async Task<PaymentCaptureResponse> CreatePrepayment(Order order, string unzerCustomerId, string basketId)
        {
            var prePaymentTypeReq = new CreatePrepaymentTypeRequest();
            var prePaymentTypeRersponse = await _unzerApiHttpClient.RequestAsync<CreatePrepaymentTypeRequest, CreatePrepaymentTypeResponse>(prePaymentTypeReq);
            if (prePaymentTypeRersponse.IsError)
            {
                var errMsg = prePaymentTypeRersponse.ErrorResponse.Errors.Any() ? string.Join(",", prePaymentTypeRersponse?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.CreatePrepayment: Failed with call to Unzer API Client with {errMsg}");
            }

            var prepaymentChargePayment = await _unzerPayRequestBuilder.BuildPrepaymentChargeRequestAsync(order, prePaymentTypeRersponse.Id, false, unzerCustomerId, basketId);
            var prePaymentChargeRersponse = await _unzerApiHttpClient.RequestAsync<CreatePrepaymentChargeRequest, PaymentCaptureResponse>(prepaymentChargePayment);
            if (prePaymentChargeRersponse.IsError)
            {
                var errMsg = prePaymentChargeRersponse.ErrorResponse.Errors.Any() ? string.Join(",", prePaymentTypeRersponse?.ErrorResponse.Errors.Select(e => e.merchantMessage)) : "";
                await _logger.ErrorAsync($"UnzerApiService.CreatePrepayment: Failed with call to Unzer API Client with {errMsg}");
            }

            return prePaymentChargeRersponse;
        }
    }
}
