using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Domain;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Html;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Unzer.Plugin.Payments.Unzer.Models.Api;

namespace Unzer.Plugin.Payments.Unzer.Infrastructure
{
    public class UnzerPaymentRequestBuilder
    {
        private readonly ICountryService _countryService;
        private readonly IStateProvinceService _stateService;
        private readonly IOrderService _orderService;
        private readonly ICurrencyService _currencyService;
        private readonly IStoreService _storeService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly UnzerPaymentSettings _unzerPaymentSettings;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly ICustomerService _customerService;
        private readonly ILanguageService _languageService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IWebHelper _webHelper;
        private readonly ILocalizationService _localizationService;
        private readonly IHtmlFormatter _htmlFormatter;
        private readonly IPictureService _pictureService;
        private readonly StoreInformationSettings _storeInformationSettings;

        private readonly IActionContextAccessor _actionContextAccessor;

        private IUrlHelper _urlHelper;

        public UnzerPaymentRequestBuilder(ICountryService countryService, IStateProvinceService stateService, IOrderService orderService, ICurrencyService currencyService, IStoreService storeService, IStoreContext storeContext, IWorkContext workContext, ShoppingCartSettings shoppingCartSettings, UnzerPaymentSettings unzserPaymentSettings, IPaymentPluginManager paymentPluginManager, ICustomerService customerService, ILanguageService languageService, IUrlHelperFactory urlHelperFactory, IWebHelper webHelper, ILocalizationService localizationService, IHtmlFormatter htmlFormatter, IPictureService pictureService, StoreInformationSettings storeInformationSettings, IActionContextAccessor actionContextAccessor)
        {
            _countryService = countryService;
            _stateService = stateService;
            _orderService = orderService;
            _currencyService = currencyService;
            _storeService = storeService;
            _storeContext = storeContext;
            _workContext = workContext;
            _shoppingCartSettings = shoppingCartSettings;
            _unzerPaymentSettings = unzserPaymentSettings;
            _paymentPluginManager = paymentPluginManager;
            _customerService = customerService;
            _languageService = languageService;
            _urlHelperFactory = urlHelperFactory;
            _webHelper = webHelper;
            _localizationService = localizationService;
            _htmlFormatter = htmlFormatter;
            _pictureService = pictureService;
            _storeInformationSettings = storeInformationSettings;

            _actionContextAccessor = actionContextAccessor;

            if (_actionContextAccessor.ActionContext != null)
                _urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
        }

        public async Task<CreateAuthorizePayPageRequest> BuildAuthorizePayPageRequestAsync(Order order, bool isRecurring, string unzerCustomerId, string basketId)
        {
            _urlHelper = _urlHelper == null ? _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext) : _urlHelper;

            var shopUrl = await GetShopUrlAsync();

            var returnUrl = _urlHelper.RouteUrl(UnzerPaymentDefaults.UnzerPaymentStatusRouteName, new { orderId = order.Id }, _webHelper.GetCurrentRequestProtocol());

            var currentStore = _storeContext.GetCurrentStore();

            var currencyCode = _unzerPaymentSettings.CurrencyCode;
            if (string.IsNullOrEmpty(currencyCode))
            {
                currencyCode = order.CustomerCurrencyCode;
            }

            var orderTotal = _currencyService.ConvertCurrency(order.OrderTotal, order.CurrencyRate);
            if (_shoppingCartSettings.RoundPricesDuringCalculation)
            {
                orderTotal = Math.Round(orderTotal, 2);
            }

            var unzerPaymentType = UnzerPaymentDefaults.ReadUnzerPaymentType(order.PaymentMethodSystemName);
            var selectedPaymentMethod = unzerPaymentType != null ? unzerPaymentType.UnzerName : order.PaymentMethodSystemName;

            var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);
            var excludeTypes = _unzerPaymentSettings.SelectedPaymentTypes.Count > 1 ? _unzerPaymentSettings.AvailablePaymentTypes.Where(t => t != selectedPaymentMethod).ToArray() : new string[0];

            var authReq = new CreateAuthorizePayPageRequest
            {
                currency = currencyCode,
                amount = orderTotal,
                orderId = order.Id.ToString("D6"),
                returnUrl = returnUrl,
                card3ds = true,
                logoImage = _unzerPaymentSettings.LogoImage,
                shopName = currentStore.Name,
                shopDescription = _unzerPaymentSettings.ShopDescription,
                tagLine = _unzerPaymentSettings.TagLine,
                additionalAttributes = isRecurring ? new AdditionalAttributes { recurrenceType = "scheduled" } : null,
                excludeTypes = excludeTypes,
                resources = new Resources
                {
                    customerId = !string.IsNullOrEmpty(unzerCustomerId) ? customer.CustomerGuid.ToString() : null,
                    metadataId = _unzerPaymentSettings.UnzerMetadataId,
                    basketId = !string.IsNullOrEmpty(basketId) ? basketId : null
                }
            };

            return authReq;
        }

        public async Task<CreatePayPageRequest> BuildV2AuthorizePayPageRequestAsync(Order order, bool isRecurring, string unzerCustomerId, string basketId)
        {
            _urlHelper = _urlHelper == null ? _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext) : _urlHelper;

            var shopUrl = await GetShopUrlAsync();
            var returnUrl = _urlHelper.RouteUrl(UnzerPaymentDefaults.UnzerPaymentStatusRouteName, new { orderId = order.Id }, _webHelper.GetCurrentRequestProtocol());
            var cancelUrl = _urlHelper.RouteUrl(UnzerPaymentDefaults.UnzerCancelPaymentRouteName, null, _webHelper.GetCurrentRequestProtocol());
            var pendingUrl = _urlHelper.RouteUrl("OrderDetails", new { orderId = order.Id }, _webHelper.GetCurrentRequestProtocol());

            var storeLogoPict = await _pictureService.GetPictureByIdAsync(_storeInformationSettings.LogoPictureId);
            var storeLogoUrl = (await _pictureService.GetPictureUrlAsync(storeLogoPict)).Url;

            var currentStore = _storeContext.GetCurrentStore();

            var currencyCode = _unzerPaymentSettings.CurrencyCode;
            if (string.IsNullOrEmpty(currencyCode))
            {
                currencyCode = order.CustomerCurrencyCode;
            }

            var orderTotal = _currencyService.ConvertCurrency(order.OrderTotal, order.CurrencyRate);
            if (_shoppingCartSettings.RoundPricesDuringCalculation)
            {
                orderTotal = Math.Round(orderTotal, 2);
            }

            var unzerPaymentType = UnzerPaymentDefaults.ReadUnzerPaymentType(order.PaymentMethodSystemName);
            var selectedPaymentMethod = unzerPaymentType != null ? unzerPaymentType.UnzerName : order.PaymentMethodSystemName;

            var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);

            var authReq = new CreatePayPageRequest
            {
                mode = PayPageMode.authorize,
                type = PayPageType.hosted,
                checkoutType = PayPageCheckoutType.payment_only,
                currency = currencyCode,
                amount = orderTotal,
                orderId = order.Id.ToString("D6"),
                paymentMethodsConfigs = await BuildV2PaymentMethodsConfig(selectedPaymentMethod),
                urls = new Urls
                {
                    returnCancel = cancelUrl,
                    returnFailure = cancelUrl,
                    returnPending = pendingUrl,
                    returnSuccess = returnUrl
                },
                style = new Style
                {
                    logoImage = !string.IsNullOrEmpty(_unzerPaymentSettings.LogoImage) ? _unzerPaymentSettings.LogoImage : storeLogoUrl,
                },
                shopName = currentStore.Name,
                recurrenceType = isRecurring ? PayPageRecurrenceType.scheduled : PayPageRecurrenceType.unscheduled,
                resources = new V2Resources
                {
                    customerId = !string.IsNullOrEmpty(unzerCustomerId) ? customer.CustomerGuid.ToString() : null,
                    metadataId = _unzerPaymentSettings.UnzerMetadataId,
                    basketId = !string.IsNullOrEmpty(basketId) ? basketId : null
                },
                customerSettings = string.IsNullOrEmpty(unzerCustomerId) ? new Customersettings
                {
                    type = CustomerType.B2C
                } : null,
            };

            return authReq;
        }

        public async Task<CreateCapturePayPageRequest> BuildCapturePayPageRequestAsync(Order order, bool isRecurring, string unzerCustomerId, string basketId)
        {
            _urlHelper = _urlHelper == null ? _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext) : _urlHelper;

            var shopUrl = await GetShopUrlAsync();

            var returnUrl = _urlHelper.RouteUrl(UnzerPaymentDefaults.UnzerPaymentStatusRouteName, new { orderId = order.Id }, _webHelper.GetCurrentRequestProtocol());

            var currentStore = _storeContext.GetCurrentStore();

            var currencyCode = _unzerPaymentSettings.CurrencyCode;
            if (string.IsNullOrEmpty(currencyCode))
            {
                currencyCode = order.CustomerCurrencyCode;
            }

            var orderTotal = _currencyService.ConvertCurrency(order.OrderTotal, order.CurrencyRate);
            if (_shoppingCartSettings.RoundPricesDuringCalculation)
            {
                orderTotal = Math.Round(orderTotal, 2);
            }

            var unzerPaymentType = UnzerPaymentDefaults.ReadUnzerPaymentType(order.PaymentMethodSystemName);
            var selectedPaymentMethod = unzerPaymentType != null ? unzerPaymentType.UnzerName : order.PaymentMethodSystemName;

            var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);
            var excludeTypes = _unzerPaymentSettings.SelectedPaymentTypes.Count > 1 ? _unzerPaymentSettings.AvailablePaymentTypes.Where(t => t != selectedPaymentMethod).ToArray() : new string[0];

            var captReq = new CreateCapturePayPageRequest
            {
                currency = currencyCode,
                amount = orderTotal,
                orderId = order.Id.ToString("D6"),
                returnUrl = returnUrl,
                card3ds = true,
                logoImage = _unzerPaymentSettings.LogoImage,
                shopName = currentStore.Name,
                shopDescription = _unzerPaymentSettings.ShopDescription,
                tagLine = _unzerPaymentSettings.TagLine,
                additionalAttributes = isRecurring ? new AdditionalAttributes { recurrenceType = "scheduled" } : null,
                excludeTypes = excludeTypes,
                resources = new Resources
                {
                    customerId = !string.IsNullOrEmpty(unzerCustomerId) ? customer.CustomerGuid.ToString() : null,
                    metadataId = _unzerPaymentSettings.UnzerMetadataId,
                    basketId = !string.IsNullOrEmpty(basketId) ? basketId : null
                }
            };

            return captReq;
        }

        public async Task<CreatePayPageRequest> BuildV2CapturePayPageRequestAsync(Order order, bool isRecurring, string unzerCustomerId, string basketId)
        {
            _urlHelper = _urlHelper == null ? _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext) : _urlHelper;

            var shopUrl = await GetShopUrlAsync();
            var returnUrl = _urlHelper.RouteUrl(UnzerPaymentDefaults.UnzerPaymentStatusRouteName, new { orderId = order.Id }, _webHelper.GetCurrentRequestProtocol());
            var cancelUrl = _urlHelper.RouteUrl(UnzerPaymentDefaults.UnzerCancelPaymentRouteName, null, _webHelper.GetCurrentRequestProtocol());
            var pendingUrl = _urlHelper.RouteUrl("OrderDetails", new { orderId = order.Id }, _webHelper.GetCurrentRequestProtocol());

            var storeLogoPict = await _pictureService.GetPictureByIdAsync(_storeInformationSettings.LogoPictureId);
            var storeLogoUrl = (await _pictureService.GetPictureUrlAsync(storeLogoPict)).Url;

            var currentStore = _storeContext.GetCurrentStore();

            var currencyCode = _unzerPaymentSettings.CurrencyCode;
            if (string.IsNullOrEmpty(currencyCode))
            {
                currencyCode = order.CustomerCurrencyCode;
            }

            var orderTotal = _currencyService.ConvertCurrency(order.OrderTotal, order.CurrencyRate);
            if (_shoppingCartSettings.RoundPricesDuringCalculation)
            {
                orderTotal = Math.Round(orderTotal, 2);
            }

            var unzerPaymentType = UnzerPaymentDefaults.ReadUnzerPaymentType(order.PaymentMethodSystemName);
            var selectedPaymentMethod = unzerPaymentType != null ? unzerPaymentType.UnzerName : order.PaymentMethodSystemName;

            var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);

            var authReq = new CreatePayPageRequest
            {
                mode = PayPageMode.charge,
                type = PayPageType.hosted,
                checkoutType = PayPageCheckoutType.payment_only,
                currency = currencyCode,
                amount = orderTotal,
                orderId = order.Id.ToString("D6"),
                paymentMethodsConfigs = await BuildV2PaymentMethodsConfig(selectedPaymentMethod),
                urls = new Urls
                {
                    returnCancel = cancelUrl,
                    returnFailure = cancelUrl,
                    returnPending = pendingUrl,
                    returnSuccess = returnUrl
                },
                style = new Style
                {
                    logoImage = !string.IsNullOrEmpty(_unzerPaymentSettings.LogoImage) ? _unzerPaymentSettings.LogoImage : storeLogoUrl,
                },
                shopName = currentStore.Name,
                recurrenceType = isRecurring ? PayPageRecurrenceType.scheduled : PayPageRecurrenceType.unscheduled,
                resources = new V2Resources
                {
                    customerId = !string.IsNullOrEmpty(unzerCustomerId) ? customer.CustomerGuid.ToString() : null,
                    metadataId = _unzerPaymentSettings.UnzerMetadataId,
                    basketId = !string.IsNullOrEmpty(basketId) ? basketId : null
                },
                customerSettings = string.IsNullOrEmpty(unzerCustomerId) ? new Customersettings
                {
                    type = CustomerType.B2C
                } : null
            };

            return authReq;
        }

        public async Task<CreatePrepaymentChargeRequest> BuildPrepaymentChargeRequestAsync(Order order, string prepaymentTypeId, bool isRecurring, string unzerCustomerId, string basketId)
        {
            var currentStore = _storeContext.GetCurrentStore();

            var currencyCode = _unzerPaymentSettings.CurrencyCode;
            if (string.IsNullOrEmpty(currencyCode))
            {
                currencyCode = order.CustomerCurrencyCode;
            }

            var orderTotal = _currencyService.ConvertCurrency(order.OrderTotal, order.CurrencyRate);
            if (_shoppingCartSettings.RoundPricesDuringCalculation)
            {
                orderTotal = Math.Round(orderTotal, 2);
            }

            var unzerPaymentType = UnzerPaymentDefaults.ReadUnzerPaymentType(order.PaymentMethodSystemName);
            var selectedPaymentMethod = unzerPaymentType != null ? unzerPaymentType.UnzerName : order.PaymentMethodSystemName;

            var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);
            var excludeTypes = _unzerPaymentSettings.SelectedPaymentTypes.Count > 1 ? _unzerPaymentSettings.AvailablePaymentTypes.Where(t => t != selectedPaymentMethod).ToArray() : new string[0];

            var captReq = new CreatePrepaymentChargeRequest
            {
                currency = currencyCode,
                amount = orderTotal,
                orderId = order.Id.ToString("D6"),
                resources = new Resources
                {
                    customerId = !string.IsNullOrEmpty(unzerCustomerId) ? customer.CustomerGuid.ToString() : null,
                    metadataId = _unzerPaymentSettings.UnzerMetadataId,
                    basketId = !string.IsNullOrEmpty(basketId) ? basketId : null,
                    typeId = prepaymentTypeId
                }
            };

            return captReq;
        }

        public async Task<CreateCaptureRequest> BuildCaptureRequestAsync(Order order, decimal amount)
        {
            var captureAmount = _currencyService.ConvertCurrency(amount, order.CurrencyRate);
            if (_shoppingCartSettings.RoundPricesDuringCalculation)
            {
                captureAmount = Math.Round(captureAmount, 2);
            }

            var paymentInfo = order.AuthorizationTransactionId.Split('/');
            var paymentId = paymentInfo.Any() ? paymentInfo.First() : order.Id.ToString("D6");
            var capReq = new CreateCaptureRequest
            {
                paymentId = paymentId,
                amount = captureAmount,
                orderId = order.Id.ToString("D6"),
            };

            return await Task.FromResult(capReq);
        }

        public async Task<CreateSubCaptureRequest> BuildSubCaptureRequestAsync(Order order, decimal amount)
        {
            var captureAmount = _currencyService.ConvertCurrency(amount, order.CurrencyRate);
            if (_shoppingCartSettings.RoundPricesDuringCalculation)
            {
                captureAmount = Math.Round(captureAmount, 2);
            }

            var capReq = new CreateSubCaptureRequest
            {
                ChargeId = order.SubscriptionTransactionId,
                orderId = order.Id.ToString("D6"),
                amount = captureAmount,
            };

            return await Task.FromResult(capReq);
        }

        public async Task<CreateRefundRequest> BuildRefundRequestAsync(Order order, decimal amount)
        {
            var refundAmount = _currencyService.ConvertCurrency(amount, order.CurrencyRate);
            if (_shoppingCartSettings.RoundPricesDuringCalculation)
            {
                refundAmount = Math.Round(refundAmount, 2);
            }

            var paymentInfo = order.CaptureTransactionId.Split('/');
            var paymentId = paymentInfo.Any() ? paymentInfo.First() : order.Id.ToString("D6");
            var captureId = paymentInfo.Any() ? paymentInfo.Last() : order.CaptureTransactionId;

            var refReq = new CreateRefundRequest
            {
                chargeId = captureId,
                paymentId = paymentId,
                amount = refundAmount,
            };

            return await Task.FromResult(refReq);
        }

        public async Task<CreateCancelAuthorizedRequest> BuildCancelRequestAsync(Order order, decimal amount)
        {
            var refundAmount = _currencyService.ConvertCurrency(amount, order.CurrencyRate);
            if (_shoppingCartSettings.RoundPricesDuringCalculation)
            {
                refundAmount = Math.Round(refundAmount, 2);
            }

            var paymentInfo = order.AuthorizationTransactionId.Split('/');
            var paymentId = paymentInfo.Any() ? paymentInfo.First() : order.Id.ToString("D6");
            var authId = paymentInfo.Any() ? paymentInfo.Last() : order.AuthorizationTransactionId;

            var canReq = new CreateCancelAuthorizedRequest
            {
                authorizeId = authId,
                paymentId = paymentId,
                amount = refundAmount,
            };

            return await Task.FromResult(canReq);
        }

        public async Task<CreateCancelChargedRequest> BuildCancelChargeRequestAsync(Order order, decimal amount)
        {
            var refundAmount = _currencyService.ConvertCurrency(amount, order.CurrencyRate);
            if (_shoppingCartSettings.RoundPricesDuringCalculation)
            {
                refundAmount = Math.Round(refundAmount, 2);
            }

            var unzerPaymentType = UnzerPaymentDefaults.ReadUnzerPaymentType(order.PaymentMethodSystemName);
            var transactionId = unzerPaymentType.Prepayment ? order.AuthorizationTransactionId : order.CaptureTransactionId;

            var paymentInfo = transactionId.Split('/');
            var paymentId = paymentInfo.Any() ? paymentInfo.First() : order.Id.ToString("D6");
            var authId = paymentInfo.Any() ? paymentInfo.Last() : transactionId;

            var canReq = new CreateCancelChargedRequest
            {
                chargeId = authId,
                paymentId = paymentId,
                amount = refundAmount,
            };

            return await Task.FromResult(canReq);
        }

        public async Task<CreateMetadataRequest> BuildCreateMetadataRequestAsync()
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            var unzerPlugin = await _paymentPluginManager.LoadPluginBySystemNameAsync(UnzerPaymentDefaults.SystemName);

            var metaReq = new CreateMetadataRequest
            {
                pluginType = UnzerPaymentDefaults.MetadataPluginType,
                shopType = UnzerPaymentDefaults.MetadataShopType,
                pluginVersion = unzerPlugin.PluginDescriptor.Version,
                shopVersion = unzerPlugin.PluginDescriptor.SupportedVersions.FirstOrDefault()
            };

            return metaReq;
        }

        public async Task<UpdateMetadataRequest> BuildUpdateMetadataRequestAsync(string metadatId)
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            var unzerPlugin = await _paymentPluginManager.LoadPluginBySystemNameAsync(UnzerPaymentDefaults.SystemName);

            var metaReq = new UpdateMetadataRequest
            {
                metadataId = metadatId,
                pluginType = UnzerPaymentDefaults.MetadataPluginType,
                shopType = UnzerPaymentDefaults.MetadataShopType,
                pluginVersion = unzerPlugin.PluginDescriptor.Version,
                shopVersion = unzerPlugin.PluginDescriptor.SupportedVersions.FirstOrDefault()
            };

            return metaReq;
        }

        public async Task<CreateCustomerRequest> BuildCreateCustomerRequestAsync(Customer customer, Address billingAddress, Address shippingAddress)
        {
            var lang = await _workContext.GetWorkingLanguageAsync();
            var billingCountry = await _countryService.GetCountryByIdAsync(billingAddress.CountryId.Value);
            var billingState = await _stateService.GetStateProvinceByIdAsync(billingAddress.StateProvinceId.Value);
            var shippingCountry = shippingAddress != null ? await _countryService.GetCountryByIdAsync(shippingAddress.CountryId.Value) : null;
            var shippingState = shippingAddress != null ? await _stateService.GetStateProvinceByIdAsync(shippingAddress.StateProvinceId.Value) : null;

            var custIsGuset = await _customerService.IsGuestAsync(customer);

            var isSameAddress = shippingAddress != null ? billingAddress.IsSameAddress(shippingAddress) : false;

            var custReq = new CreateCustomerRequest
            {
                customerId = customer.CustomerGuid.ToString(),
                firstname = !custIsGuset ? customer.FirstName : billingAddress.FirstName,
                lastname = !custIsGuset ? customer.LastName : billingAddress.LastName,
                company = !custIsGuset ? customer.Company : billingAddress.Company,
                email = !custIsGuset ? customer.Email : billingAddress.Email,
                phone = !custIsGuset ? customer.Phone : billingAddress.PhoneNumber,
                mobile = !custIsGuset ? customer.Phone : billingAddress.PhoneNumber,
                language = lang.UniqueSeoCode,
                billingAddress = new Billingaddress
                {
                    name = $"{billingAddress.FirstName} {billingAddress.LastName}",
                    street = billingAddress.Address1,
                    city = billingAddress.City,
                    zip = billingAddress.ZipPostalCode,
                    country = billingCountry?.TwoLetterIsoCode,
                    state = billingState?.Name,
                },
                shippingAddress = shippingAddress == null ? null : new Shippingaddress
                {
                    name = $"{shippingAddress.FirstName} {shippingAddress.LastName}",
                    street = shippingAddress.Address1,
                    city = shippingAddress.City,
                    zip = shippingAddress.ZipPostalCode,
                    country = shippingCountry?.TwoLetterIsoCode,
                    state = shippingState?.Name,
                    shippingType = isSameAddress ? "equals-billing" : "different-address"
                }
            };

            return custReq;
        }

        public async Task<UpdateCustomerRequest> BuildUpdateCustomerRequestAsync(string unzerCustomerId, Customer customer, Address billingAddress, Address shippingAddress)
        {
            var lang = await _workContext.GetWorkingLanguageAsync();
            var billingCountry = await _countryService.GetCountryByIdAsync(billingAddress.CountryId.Value);
            var billingState = await _stateService.GetStateProvinceByIdAsync(billingAddress.StateProvinceId.Value);
            var shippingCountry = shippingAddress != null ? await _countryService.GetCountryByIdAsync(shippingAddress.CountryId.Value) : null;
            var shippingState = shippingAddress != null ? await _stateService.GetStateProvinceByIdAsync(shippingAddress.StateProvinceId.Value) : null;

            var custIsGuset = await _customerService.IsGuestAsync(customer);

            var isSameAddress = shippingAddress != null ? billingAddress.IsSameAddress(shippingAddress) : false;

            var adrComp = shippingAddress == billingAddress;

            var custReq = new UpdateCustomerRequest
            {
                id = unzerCustomerId,
                customerId = customer.CustomerGuid.ToString(),
                firstname = !custIsGuset ? customer.FirstName : billingAddress.FirstName,
                lastname = !custIsGuset ? customer.LastName : billingAddress.LastName,
                company = !custIsGuset ? customer.Company : billingAddress.Company,
                email = !custIsGuset ? customer.Email : billingAddress.Email,
                phone = !custIsGuset ? customer.Phone : billingAddress.PhoneNumber,
                mobile = !custIsGuset ? customer.Phone : billingAddress.PhoneNumber,
                language = lang.UniqueSeoCode,
                billingAddress = new Billingaddress
                {
                    name = $"{billingAddress.FirstName} {billingAddress.LastName}",
                    street = billingAddress.Address1,
                    city = billingAddress.City,
                    zip = billingAddress.ZipPostalCode,
                    country = billingCountry?.TwoLetterIsoCode,
                    state = billingState?.Name,
                },
                shippingAddress = shippingAddress == null ? null : new Shippingaddress
                {
                    name = $"{shippingAddress.FirstName} {shippingAddress.LastName}",
                    street = shippingAddress.Address1,
                    city = shippingAddress.City,
                    zip = shippingAddress.ZipPostalCode,
                    country = shippingCountry?.TwoLetterIsoCode,
                    state = shippingState?.Name,
                    shippingType = isSameAddress ? "equals-billing" : "different-address"
                }
            };

            return custReq;
        }

        public async Task<CreateBasketRequest> BuildCreateBasketRequestAsync(Order order)
        {
            var currencyCode = _unzerPaymentSettings.CurrencyCode;
            if (string.IsNullOrEmpty(currencyCode))
            {
                currencyCode = order.CustomerCurrencyCode;
            }

            var orderItems = await _orderService.GetOrderItemsAsync(order.Id);
            var taxRates = _orderService.ParseTaxRates(order, order.TaxRates);
            var vatRate = taxRates.Any() ? Decimal.ToInt32(taxRates.FirstOrDefault().Key) : 0;

            var basketReq = new CreateBasketRequest
            {
                amountTotalGross = Math.Round(_currencyService.ConvertCurrency(order.OrderSubtotalInclTax, order.CurrencyRate), 2),
                amountTotalDiscount = Math.Round(_currencyService.ConvertCurrency(order.OrderSubTotalDiscountExclTax, order.CurrencyRate), 2),
                amountTotalVat = Math.Round(_currencyService.ConvertCurrency(order.OrderTax, order.CurrencyRate), 2),
                currencyCode = currencyCode,
                orderId = order.Id.ToString("D6"),
                basketItems = await orderItems.SelectAwait(async i => new Basketitem
                {
                    basketItemReferenceId = i.Id.ToString(),
                    quantity = i.Quantity,
                    amountGross = Math.Round(_currencyService.ConvertCurrency(i.PriceInclTax, order.CurrencyRate), 2),
                    amountPerUnit = Math.Round(_currencyService.ConvertCurrency(i.UnitPriceExclTax, order.CurrencyRate), 2),
                    amountDiscount = Math.Round(_currencyService.ConvertCurrency(i.DiscountAmountInclTax, order.CurrencyRate), 2),
                    amountNet = Math.Round(_currencyService.ConvertCurrency(i.PriceExclTax, order.CurrencyRate), 2),
                    amountVat = Math.Round(_currencyService.ConvertCurrency(i.PriceInclTax - i.PriceExclTax, order.CurrencyRate), 2),
                    vat = vatRate,
                    title = (await _orderService.GetProductByOrderItemIdAsync(i.Id)).Name
                }).ToArrayAsync()
            };

            return basketReq;
        }

        public async Task<CreateV2BasketRequest> BuildCreateV2BasketRequestAsync(Order order)
        {
            var lang = await _workContext.GetWorkingLanguageAsync();
            var currencyCode = _unzerPaymentSettings.CurrencyCode;
            if (string.IsNullOrEmpty(currencyCode))
            {
                currencyCode = order.CustomerCurrencyCode;
            }

            var orderItems = await _orderService.GetOrderItemsAsync(order.Id);

            var basketReq = new CreateV2BasketRequest
            {
                totalValueGross = Math.Round(_currencyService.ConvertCurrency(order.OrderSubtotalInclTax, order.CurrencyRate), 2),
                currencyCode = currencyCode,
                orderId = order.Id.ToString("D6"),
                basketItems = await ReadBasketItemsAsync(order, orderItems, lang.Id)
            };

            return basketReq;
        }

        public async Task<GetPaymentAutorizeRequest> BuildPaymentAuthorizeRequestAsync(string paymentId)
        {
            var payAuthReq = new GetPaymentAutorizeRequest
            {
                paymentId = paymentId
            };

            return await Task.FromResult(payAuthReq);
        }

        public async Task<GetPaymentCaptureRequest> BuildPaymentCaptureRequestAsync(string paymentId, string chargeId)
        {
            var payCaptReq = new GetPaymentCaptureRequest
            {
                paymentId = paymentId,
                chargeId = chargeId
            };

            return await Task.FromResult(payCaptReq);
        }

        public async Task<SetWebHookRequest> BuildWebHookRequestAsync(string url, WebHookEventType eventType)
        {
            var setWebHook = new SetWebHookRequest
            {
                Url = url,
                Event = eventType.ToString(),
            };

            return await Task.FromResult(setWebHook);
        }

        public async Task<SetWebHookRequest> BuildWebHookRequestAsync(string url, List<string> eventTypes)
        {
            var setWebHook = new SetWebHookRequest
            {
                Url = url,
                EventList = eventTypes.ToArray()
            };

            return await Task.FromResult(setWebHook);
        }

        private async Task<JsonObject> BuildV2PaymentMethodsConfig(string selectedPaymentMethod)
        {
            var config = new JsonObject();

            var excludeTypes = _unzerPaymentSettings.SelectedPaymentTypes.Count > 1 ? _unzerPaymentSettings.AvailablePaymentTypes.Where(t => t != selectedPaymentMethod).ToArray() : new string[0];
            var defaultSelected = !excludeTypes.Any();
            config = new JsonObject
            {
                ["default"] = new JsonObject
                {
                    ["enabled"] = defaultSelected,
                    ["credentialOnFile"] = defaultSelected
                }
            };

            var commonAttr = new string[] { "enabled", "order" };

            var selectedpayment = UnzerPaymentDefaults.ReadPaymentTypeByUnzerName(selectedPaymentMethod);
            if (selectedpayment.PaypageInfo != null && (selectedpayment.Name != "Unzer" && selectedpayment.UnzerName != "unz"))
            {
                var paymentAttr = new JsonObject
                {
                    ["enabled"] = true,
                    ["order"] = 0,
                };

                foreach (var attr in selectedpayment.PaypageInfo.Attributes)
                {
                    if (!commonAttr.Contains(attr))
                    {
                        if (attr == "credentialOnFile")
                            paymentAttr.Add(attr, true);
                        else if (attr == "exemption")
                            paymentAttr.Add(attr, "lvp");
                        else if (attr == "label")
                            paymentAttr.Add(attr, "");
                    }
                }

                config[selectedpayment.PaypageInfo.Name] = paymentAttr;
            }            

            foreach (var item in excludeTypes)
            {
                var paymentType = UnzerPaymentDefaults.ReadPaymentTypeByUnzerName(item);
                if (paymentType.PaypageInfo == null || (paymentType.Name == "Unzer" && paymentType.UnzerName == "unz"))
                    continue;

                var paymentAttr = new JsonObject
                {
                    ["enabled"] = false,
                    ["order"] = 0,
                };

                foreach (var attr in paymentType.PaypageInfo.Attributes)
                {
                    if (!commonAttr.Contains(attr))
                    {
                        if(attr == "credentialOnFile")
                            paymentAttr.Add(attr, true);
                        else if (attr == "exemption")
                            paymentAttr.Add(attr, "lvp");
                        else if (attr == "label")
                            paymentAttr.Add(attr, "");
                    }
                }

                config[paymentType.PaypageInfo.Name] = paymentAttr;
            }

            return await Task.FromResult(config);
        }

        private async Task<V2Basketitem[]> ReadBasketItemsAsync(Order order, IList<OrderItem> orderItems, int languageId)
        {
            var basketItems = new List<V2Basketitem>();
            var taxRates = _orderService.ParseTaxRates(order, order.TaxRates);
            var vatRate = taxRates.Any() ? Decimal.ToInt32(taxRates.FirstOrDefault().Key) : 0;

            foreach (var item in orderItems)
            {
                var product = await _orderService.GetProductByOrderItemIdAsync(item.Id);

                //get default product picture
                var picture = await _pictureService.GetProductPictureAsync(product, item.AttributesXml);

                var basketItem = new V2Basketitem
                {
                    basketItemReferenceId = item.Id.ToString(),
                    quantity = item.Quantity,
                    amountPerUnitGross = Math.Round(_currencyService.ConvertCurrency(item.UnitPriceInclTax, order.CurrencyRate), 2),
                    amountDiscountPerUnitGross = Math.Round(_currencyService.ConvertCurrency(item.DiscountAmountInclTax, order.CurrencyRate), 2),
                    vat = vatRate,
                    title = await _localizationService.GetLocalizedAsync(product, x => x.Name, languageId),
                    imageUrl = (await _pictureService.GetPictureUrlAsync(picture)).Url
                };

                if (basketItem.imageUrl.Contains("localhost"))
                    basketItem.imageUrl = string.Empty;

                //attributes
                if (!string.IsNullOrEmpty(item.AttributeDescription))
                {
                    var attributes = _htmlFormatter.ConvertHtmlToPlainText(item.AttributeDescription, true, true);
                    basketItem.subTitle = attributes;
                }

                basketItems.Add(basketItem);
            }

            return basketItems.ToArray();
        }

        private async Task<string> GetShopUrlAsync()
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
