using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Stores;
using Unzer.Plugin.Payments.Unzer.Services;
using Unzer.Plugin.Payments.Unzer.Components;
using Unzer.Plugin.Payments.Unzer.Infrastructure;
using Nop.Core.Domain.Catalog;
using Nop.Services.Customers;
using Nop.Services.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Nop.Services.Catalog;
using Unzer.Plugin.Payments.Unzer.Models;

namespace Unzer.Plugin.Payments.Unzer
{
    public class UnzerPaymentMethod : BasePlugin, IPaymentMethod
    {
        private readonly UnzerPaymentSettings _unzerPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IOrderService _orderService;
        private readonly IWebHelper _webHelper;
        private readonly IUnzerApiService _unzerApiService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IStoreContext _storeContext;
        private readonly IStoreService _storeService;
        private readonly UnzerPaymentRequestBuilder _unzerPayRequestBuilder;
        private readonly ICustomerService _customerService;
        private readonly IAddressService _addressService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly ICallEventHandler<CaptureEventHandler> _captEventHandler;

        private IHttpContextAccessor _httpContextAccessor;
        private IUrlHelper _urlHelper;

        public UnzerPaymentMethod(UnzerPaymentSettings unzerPaymentSettings, ISettingService settingService, IOrderTotalCalculationService orderTotalCalculationService, IOrderService orderService, IWebHelper webHelper, IUnzerApiService unzerApiService, ILocalizationService localizationService, ILogger logger, IStoreService storeService, IStoreContext storeContext, UnzerPaymentRequestBuilder unzerPayRequestBuilder, IHttpContextAccessor httpContextAccessor, ICustomerService customerService, IAddressService addressService, IUrlHelperFactory urlHelperFactory, IActionContextAccessor actionContextAccessor, ICallEventHandler<CaptureEventHandler> captEventHandle)
        {
            _unzerPaymentSettings = unzerPaymentSettings;
            _settingService = settingService;  
            _orderTotalCalculationService = orderTotalCalculationService;
            _orderService = orderService;
            _webHelper = webHelper;  
            _unzerApiService = unzerApiService;
            _localizationService = localizationService;
            _logger = logger;
            _storeService = storeService;
            _storeContext = storeContext;
            _unzerPayRequestBuilder = unzerPayRequestBuilder;
            _httpContextAccessor = httpContextAccessor;
            _customerService = customerService;
            _addressService = addressService;
            _urlHelperFactory = urlHelperFactory;
            _actionContextAccessor = actionContextAccessor;
            _captEventHandler = captEventHandle;

            if (_actionContextAccessor.ActionContext != null)
                _urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
        }

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture => true;

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund => true;

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund => true;

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid => true;

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.Manual;

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => _unzerPaymentSettings.SkipPaymentInfo;

        #endregion

        public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            _urlHelper = _urlHelper == null ? _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext) : _urlHelper;

            var order = postProcessPaymentRequest.Order;
            var isRecurring = !string.IsNullOrEmpty(order.SubscriptionTransactionId) && order.SubscriptionTransactionId == order.OrderGuid.ToString();
            var canAutoCapture = await CanAutoCapture(order);
            var unzerPaymentType = UnzerPaymentDefaults.ReadUnzerPaymentType(order.PaymentMethodSystemName);
            var onlySupportsCharge = unzerPaymentType.SupportCharge && !unzerPaymentType.SupportAuthurize;

            var curStore = await _storeContext.GetCurrentStoreAsync();
            var unzerCustomerId = await PrepareCustomerForPaymentAsync(order);
            var unzerBasketID = await PrepareBasketForPaymentAsync(order);

            var paylinkUrl = string.Empty;

            if(unzerPaymentType.Prepayment)
            {
                var languageId = _storeContext.GetCurrentStore().DefaultLanguageId;
                var redirect = _urlHelper.RouteUrl(UnzerPaymentDefaults.UnzerPaymentStatusRouteName, new { orderId = order.Id }, _webHelper.GetCurrentRequestProtocol());

                var prepaymentResponse = await _unzerApiService.CreatePrepayment(order, unzerCustomerId, unzerBasketID);
                if (prepaymentResponse.IsError)
                {
                    var invalidMsg = $"Prepayment for order {order.Id} failed with {prepaymentResponse.message}";
                    throw new NopException(invalidMsg);
                }

                await _captEventHandler.HandleEvent(prepaymentResponse);

                _httpContextAccessor.HttpContext.Response.Redirect(redirect);
            }

            if ((isRecurring || canAutoCapture || onlySupportsCharge) && unzerPaymentType.SupportCharge)
            {
                var payRespons = await _unzerApiService.CreateCapturePayment(order, isRecurring, unzerCustomerId, unzerBasketID);

                if (!payRespons.Success)
                {
                    var invalidMsg = $"Payment for order {order.Id} failed with {payRespons.StatusMessage}";
                    throw new NopException(invalidMsg);
                }

                if (string.IsNullOrEmpty(payRespons.RedirectUrl))
                {
                    var invalidMsg = $"Payment for order {order.Id} failed with empty Redirect Url";
                    throw new NopException(invalidMsg);
                }

                paylinkUrl = payRespons.RedirectUrl;
            }
            else if(unzerPaymentType.SupportAuthurize)
            {
                var payRespons = await _unzerApiService.CreateAuthPayment(order, isRecurring, unzerCustomerId, unzerBasketID);

                if (!payRespons.Success)
                {
                    var invalidMsg = $"Payment for order {order.Id} failed with {payRespons.StatusMessage}";
                    throw new NopException(invalidMsg);
                }

                if (string.IsNullOrEmpty(payRespons.RedirectUrl))
                {
                    var invalidMsg = $"Payment for order {order.Id} failed with empty Redirect Url";                
                    throw new NopException(invalidMsg);
                }

                paylinkUrl = payRespons.RedirectUrl;
            }
            else
            {
                var unsupportedMsg = $"Payment for order {order.Id} failed with unsupported payment method {order.PaymentMethodSystemName}";
                throw new NopException(unsupportedMsg);
            }

            if(!string.IsNullOrEmpty(paylinkUrl))
                _httpContextAccessor.HttpContext.Response.Redirect(paylinkUrl);
        }

        public async Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();

            cancelPaymentRequest.Order.SubscriptionTransactionId = null;

            return result;
        }

        public Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //Method is the redirection payment method
            //It also validates whether order is also paid (after redirection) so customers will not be able to pay twice

            //payment status should be Pending
            if (order.PaymentStatus != PaymentStatus.Pending)
                return Task.FromResult(false);

            //let's ensure that at least 1 minute passed after order is placed
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes < 1)
                return Task.FromResult(false);

            return Task.FromResult(true);
        }

        public async Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            var orderTotal = capturePaymentRequest.Order.OrderTotal;

            var captureStatus = await _unzerApiService.CapturePayment(capturePaymentRequest.Order, orderTotal);
            if(!captureStatus.Success)
            {
                result.Errors = new List<string>() { captureStatus.StatusMessage };
            }
            else
            {
                result = new CapturePaymentResult
                {
                    CaptureTransactionId = captureStatus.ResponseId,
                    CaptureTransactionResult = captureStatus.StatusMessage,
                    NewPaymentStatus = PaymentStatus.Paid
                };
            }

            return result;
        }

        public async Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult
            {
                NewPaymentStatus = PaymentStatus.Pending,
                RecurringPaymentFailed = false,
            };

            if (processPaymentRequest.InitialOrder == null)
            {
                result.SubscriptionTransactionId = processPaymentRequest.OrderGuid.ToString();
            }
            else
            {
                var initOrder = processPaymentRequest.InitialOrder;
                if (initOrder != null)
                {
                    var orderTotal = processPaymentRequest.OrderTotal;

                    var autoCapture = await CanAutoCapture(initOrder);
                    var captureResult = await _unzerApiService.CaptureSubPayment(initOrder, orderTotal);
                    if (!captureResult.Success)
                    {
                        result.Errors = new List<string>() { captureResult.StatusMessage };
                    }
                    else
                    {
                        result = new ProcessPaymentResult
                        {
                            CaptureTransactionId = captureResult.ResponseId,
                            CaptureTransactionResult = captureResult.StatusMessage,
                            NewPaymentStatus = PaymentStatus.Paid
                        };
                    }
                }
                else
                {
                    result.AddError("Recurring Capture failed no intial order found");
                }
            }

            return result;
        }

        public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            var result = await _orderTotalCalculationService.CalculatePaymentAdditionalFeeAsync(cart, _unzerPaymentSettings.AdditionalFeePercentage, true);
            return result;
        }

        public override async Task InstallAsync()
        {
            var settings = new UnzerPaymentSettings()
            {
                UnzerApiBaseUrl = "https://api.unzer.com",
                UnzerApiKey = UnzerPaymentDefaults.DefaultApiKeySetting,
                ShopUrl = (await _storeContext.GetCurrentStoreAsync()).Url,
                LogoImage = string.Empty,
                ShopDescription = string.Empty,
                TagLine = string.Empty,
                CurrencyCode = string.Empty,
                SelectedPaymentTypes = new List<string>() { },
                SkipPaymentInfo = false,
                AdditionalFeePercentage = 0,
                LogCallbackPostData = false,
                AutoCapture = AutoCapture.None
            };

            await _settingService.SaveSettingAsync(settings);

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Enums.Unzer.Plugin.Payments.Unzer.AutoCapture.None"] = "None",
                ["Enums.Unzer.Plugin.Payments.Unzer.AutoCapture.OnOrderShipped"] = "Order shipped/picked up",
                ["Enums.Unzer.Plugin.Payments.Unzer.AutoCapture.OnOrderDelivered"] = "Order delivered",
                ["Enums.Unzer.Plugin.Payments.Unzer.AutoCapture.AutoCapture"] = "Auto capture",
                ["Enums.Unzer.Plugin.Payments.Unzer.AutoCapture.OnAuthForDownloadableProduct"] = "Ordee with e-products",
                ["Enums.Unzer.Plugin.Payments.Unzer.AutoCapture.OnAuthForNoneDeliverProduct"] = "Order with none shipable product(s)",

                ["Plugins.Payments.Unzer.PaymentInfoText"] = "Click continue to review order and complete payment.",

                ["Plugins.Payments.Unzer.Instructions"] = "Configure - Unzer Payments",

                ["Plugins.Payments.Unzer.Configuration.Webhook.Warning"] = "Callback webhooks could not be created, please try to reconfigure",
                ["Plugins.Payments.Unzer.Configuration.KeyPair.Warning"] = "Keypair information could not be retrieved from API, please try to reconfigure",

                ["Plugins.Payments.Unzer.Fields.UnzerApiBaseUrl"] = "Unzer API URL",
                ["Plugins.Payments.Unzer.Fields.UnzerApiBaseUrl.Hint"] = "Use the default Url for Unzer API calls, or enter a new one if updated to a new version (v2 ...)",

                ["Plugins.Payments.Unzer.Fields.UnzerApiKey"] = "Unzer API key",
                ["Plugins.Payments.Unzer.Fields.UnzerApiKey.Hint"] = "Use the private key to authorize access to the API. The key will not be shown in UI",
                ["Plugins.Payments.Unzer.Fields.UnzerApiKey.Required"] = "API key must be specififed",

                ["Plugins.Payments.Unzer.Fields.ShopUrl"] = "Shop URL",
                ["Plugins.Payments.Unzer.Fields.ShopUrl.Hint"] = "Override the default shop url, used for callback and return url",

                ["Plugins.Payments.Unzer.Fields.LogoImage"] = "Logo in payment window",
                ["Plugins.Payments.Unzer.Fields.LogoImage.Hint"] = "Override the logo shown in the payment window",

                ["Plugins.Payments.Unzer.Fields.ShopDescription"] = "Shop description",
                ["Plugins.Payments.Unzer.Fields.ShopDescription.Hint"] = "Make a short description of your shop to be shown in payment window",

                ["Plugins.Payments.Unzer.Fields.TagLine"] = "Tag line",
                ["Plugins.Payments.Unzer.Fields.TagLine.Hint"] = "Enter a tag line to be shown in the payment window",

                ["Plugins.Payments.Unzer.Fields.PaymentMethods"] = "Payment methods",
                ["Plugins.Payments.Unzer.Fields.PaymentMethods.Hint"] = "Select supported payment methods. Only these will be shown in the payment window. Leave blank to show all",

                ["Plugins.Payments.Unzer.Fields.LogCallbackPostData"] = "Log callback data",
                ["Plugins.Payments.Unzer.Fields.LogCallbackPostData.Hint"] = "Enable this to log all calback data. This is usefull during debug",

                ["Plugins.Payments.Unzer.Fields.SkipPaymentInfo"] = "Skip payment info",
                ["Plugins.Payments.Unzer.Fields.SkipPaymentInfo.Hint"] = "Enable this to skip the payment info step during check-out",

                ["Plugins.Payments.Unzer.Fields.AutoCapture"] = "Auto capture",
                ["Plugins.Payments.Unzer.Fields.AutoCapture.Hint"] = "Select the wanted auto capture feature",

                ["Plugins.Payments.Unzer.Fields.CurrencyCode"] = "Currency",
                ["Plugins.Payments.Unzer.Fields.CurrencyCode.Hint"] = "Force all payment to use this currency, instead of the currency selected by the shop or customer",

                ["Plugins.Payments.Unzer.Fields.AdditionalFeePercentage"] = "Additional fee (%)",
                ["Plugins.Payments.Unzer.Fields.AdditionalFeePercentage.Hint"] = "Add this additional fee percentage to the payment",

                ["Plugins.Payments.Unzer.Fields.SendOrderConfirmOnAuthorized"] = "Send order receipt on authorize",
                ["Plugins.Payments.Unzer.Fields.SendOrderConfirmOnAuthorized.Hint"] = "Sending order notification when the payment gets authorized",

                ["Plugins.Payments.Unzer.PaymentMethod.Description"] = "Pay by {0}",
                ["Plugins.Payments.Unzer.PaymentMethod.DefaultMethodDescription"] = "Unzer Payments",
                ["Plugins.Payments.Unzer.PaymentMethod.Prepayment.Instructions"] = "Please transfer the amount of {0} to the following account:",
                ["Plugins.Payments.Unzer.PaymentMethod.Prepayment.Reference"] = "Please use only this indentification number as the descriptor:",
            });

            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            if (_unzerPaymentSettings.UnzerWebHooksSet)
            {
                var webHookResponse = await _unzerApiService.DeleteWebHookEventsAsync();
                if (webHookResponse.IsError)
                {
                    await _logger.WarningAsync($"Unistall Unzer Payment: Failed deleting web hooks with: {webHookResponse.ErrorResponse.Errors[0].merchantMessage}");
                }
            }

            if (!string.IsNullOrEmpty(_unzerPaymentSettings.UnzerMetadataId))
            {
                var metadataResponse = await _unzerApiService.DeleteMetadata(_unzerPaymentSettings.UnzerMetadataId);
                if (!metadataResponse.Success)
                {
                    await _logger.WarningAsync($"Unistall Unzer Payment: Failed deleting web hooks with: {metadataResponse.StatusMessage}");
                }
            }

            await _settingService.DeleteSettingAsync<UnzerPaymentSettings>();

            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.Unzer");
            await _localizationService.DeleteLocaleResourcesAsync("Enums.Unzer.Plugin.Payments.Unzer");

            await base.UninstallAsync();
        }

        public override async Task UpdateAsync(string currentVersion, string targetVersion)
        {
            if(currentVersion != targetVersion && !string.IsNullOrEmpty(_unzerPaymentSettings.UnzerMetadataId))
            {
                var updMetaResult = await _unzerApiService.UpdateMetadata(_unzerPaymentSettings.UnzerMetadataId);
                if (!updMetaResult.Success)
                {
                    await _logger.ErrorAsync($"Updating metadata in Unzer Payment faliled with: {updMetaResult.StatusMessage}");
                }
            }

            await base.UpdateAsync(currentVersion, targetVersion);
        }

        public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            return Task.FromResult(new ProcessPaymentRequest());
        }

        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            var paymentDescription = PluginDescriptor.Description;
            if(_unzerPaymentSettings.SelectedPaymentTypes != null && _unzerPaymentSettings.SelectedPaymentTypes.Count() <= 1)
                paymentDescription = await _localizationService.GetResourceAsync("Plugins.Payments.Unzer.PaymentMethod.DefaultMethodDescription");

            return paymentDescription;
        }

        public Type GetPublicViewComponent()
        {
            return typeof(PaymentInfoViewComponent);
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/UnzerPayment/Configure";
        }

        public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            return Task.FromResult(false);
        }

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;
            return Task.FromResult(result);
        }


        public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();

            var amountToRefund = refundPaymentRequest.AmountToRefund;

            var transId = refundPaymentRequest.Order.AuthorizationTransactionId;
            var refundStatus = await _unzerApiService.RefundPayment(refundPaymentRequest.Order, amountToRefund);
            if (!refundStatus.Success)
            {
                result.Errors = new List<string>() { refundStatus.StatusMessage };
            }
            else
            {
                result = new RefundPaymentResult
                {
                    NewPaymentStatus = refundPaymentRequest.IsPartialRefund
                        ? PaymentStatus.PartiallyRefunded
                        : PaymentStatus.Refunded
                };
            }

            return result;
        }

        public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            var valid = new List<string>();
            return Task.FromResult<IList<string>>(valid);
        }

        public async Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();

            var unzerPaymentType = UnzerPaymentDefaults.ReadUnzerPaymentType(voidPaymentRequest.Order.PaymentMethodSystemName);

            var orderTotal = voidPaymentRequest.Order.OrderTotal;

            PaymentApiStatus refundStatus;

            if (unzerPaymentType.Prepayment)
            {
                refundStatus = await _unzerApiService.CancelChargePayment(voidPaymentRequest.Order, orderTotal);
            }
            else
            {
                refundStatus = await _unzerApiService.CancelPayment(voidPaymentRequest.Order, orderTotal);
            }
             
            if (!refundStatus.Success)
            {
                result.Errors = new List<string>() { refundStatus.StatusMessage };
            }
            else
            {
                result = new VoidPaymentResult
                {
                    NewPaymentStatus = PaymentStatus.Voided
                };
            }

            return result;
        }

        private async Task<string> PrepareCustomerForPaymentAsync(Order order)
        {
            var unserCustomerId = string.Empty;
            var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);
            var billingAddress = await _addressService.GetAddressByIdAsync(order.BillingAddressId);
            var shippingAddress = order.ShippingAddressId.HasValue ? await _addressService.GetAddressByIdAsync(order.ShippingAddressId.Value) : null;

            var custFoundResult = await _unzerApiService.GetCustomer(customer.CustomerGuid.ToString());
            if(!custFoundResult.Success)
            {
                var custCreate = await _unzerApiService.CreateCustomer(customer, billingAddress, shippingAddress);
                if (!custCreate.Success)
                {
                    await _logger.ErrorAsync($"Customer creation in Unzer Payment faliled with: {custCreate.StatusMessage}");
                    return unserCustomerId;
                }

                unserCustomerId = custCreate.ResponseId;
            }
            else if(custFoundResult.Success && !string.IsNullOrEmpty(custFoundResult.ResponseId))
            {
                var updCreate = await _unzerApiService.UpdateCustomer(custFoundResult.ResponseId, customer, billingAddress, shippingAddress);
                if (!updCreate.Success)
                {
                    await _logger.ErrorAsync($"Customer update in Unzer Payment faliled with: {updCreate.StatusMessage}");
                    return unserCustomerId;
                }

                unserCustomerId = updCreate.ResponseId;
            }

            return unserCustomerId;
        }

        private async Task<string> PrepareBasketForPaymentAsync(Order order)
        {
            var unzerBasketId = string.Empty;

            var basketCreate = await _unzerApiService.CreateV2Basket(order);
            if (!basketCreate.Success)
            {
                await _logger.ErrorAsync($"Basket creation in Unzer Payment faliled with: {basketCreate.StatusMessage}");
                return unzerBasketId;
            }

            unzerBasketId = basketCreate.ResponseId;

            return unzerBasketId;
        }

        private async Task<bool> CanAutoCapture(Order order)
        {
            var onlyDownloadableProduct = true;
            var onlyNoneDeliverProduct = false;

            var itemsData = await _orderService.GetOrderItemsAsync(order.Id);
            foreach (var item in itemsData)
            {
                var prod = await _orderService.GetProductByOrderItemIdAsync(item.Id);
                if (!prod.IsDownload || (prod.IsDownload && prod.DownloadActivationType != DownloadActivationType.WhenOrderIsPaid))
                {
                    onlyDownloadableProduct = false;
                    break;
                }
            }

            itemsData = await _orderService.GetOrderItemsAsync(order.Id, null, true);
            onlyNoneDeliverProduct = !itemsData.Any();

            if (_unzerPaymentSettings.AutoCapture == AutoCapture.AutoCapture && (onlyDownloadableProduct || onlyNoneDeliverProduct))
                return true;

            if (_unzerPaymentSettings.AutoCapture == AutoCapture.OnAuthForDownloadableProduct)
                return onlyDownloadableProduct;

            if (_unzerPaymentSettings.AutoCapture == AutoCapture.OnAuthForNoneDeliverProduct)
                return onlyNoneDeliverProduct;

            return false;
        }

        private async Task<string> GetShopUrl()
        {
            string shopUrl = _unzerPaymentSettings.ShopUrl;
            var curStore = await _storeContext.GetCurrentStoreAsync();

            if ((await _storeService.GetAllStoresAsync()).Count > 1 || string.IsNullOrEmpty(shopUrl))
            {
                shopUrl = curStore.Url;
            }

            if (!curStore.SslEnabled && !shopUrl.StartsWith("http://"))
            {
                shopUrl = string.Format("http://{0}", shopUrl);
            }
            else if (curStore.SslEnabled && !shopUrl.StartsWith("https://"))
            {
                if (shopUrl.StartsWith("http://"))
                {
                    shopUrl = shopUrl.Replace("http://", "https://");
                }
                else
                {
                    shopUrl = string.Format("https://{0}", shopUrl);
                }
            }

            return shopUrl.TrimEnd('/');
        }
    }
}
