using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Unzer.Plugin.Payments.Unzer.Infrastructure;
using Unzer.Plugin.Payments.Unzer.Models;
using Unzer.Plugin.Payments.Unzer.Services;

namespace Unzer.Plugin.Payments.Unzer.Controllers
{
    [Area(AreaNames.ADMIN)]
    [AutoValidateAntiforgeryToken]
    [ValidateIpAddress]
    [AuthorizeAdmin]
    public class UnzerPaymentController : BasePaymentController
    {
        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IUnzerApiService _unzerApiService;
        private readonly ICountryService _countryService;
        private readonly ILogger _logger;

        public UnzerPaymentController(ILocalizationService localizationService,
            INotificationService notificationService,
            ISettingService settingService,
            IStoreContext storeContext,
            IUnzerApiService unzerApiService,
            ICountryService countryService,
            ILogger logger)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _settingService = settingService;
            _storeContext = storeContext;
            _unzerApiService = unzerApiService;
            _countryService = countryService;
            _logger = logger;
        }

        [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
        public async Task<IActionResult> Configure()
        {
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var unzerPaymentSettings = await _settingService.LoadSettingAsync<UnzerPaymentSettings>(storeId);

            var model = new ConfigurationModel
            {
                UnzerApiBaseUrl = unzerPaymentSettings.UnzerApiBaseUrl,
                UnzerTokenUrl = unzerPaymentSettings.UnzerTokenUrl,
                UnzerPaypageApiUrl = unzerPaymentSettings.UnzerPaypageApiUrl,
                UnzerApiKey = unzerPaymentSettings.UnzerApiKey,
                ShopUrl = unzerPaymentSettings.ShopUrl,
                LogoImage = unzerPaymentSettings.LogoImage,
                ShopDescription = unzerPaymentSettings.ShopDescription,
                TagLine = unzerPaymentSettings.TagLine,
                SelectedPaymentTypes= unzerPaymentSettings.SelectedPaymentTypes,
                CurrencyCode = unzerPaymentSettings.CurrencyCode,
                EnforceCountryRestriction = unzerPaymentSettings.EnforceCountryRestriction,
                EnforceCurrencyRestriction = unzerPaymentSettings.EnforceCurrencyRestriction,
                AdditionalFeePercentage = unzerPaymentSettings.AdditionalFeePercentage,
                AutoCapture = unzerPaymentSettings.AutoCapture,
                AutoCaptureOptions = await unzerPaymentSettings.AutoCapture.ToSelectListAsync(),
                LogCallbackPostData = unzerPaymentSettings.LogCallbackPostData,
                SkipPaymentInfo = unzerPaymentSettings.SkipPaymentInfo,
                ActiveStoreScopeConfiguration = storeId,
                SendOrderConfirmOnAuthorized = unzerPaymentSettings.SendOrderConfirmOnAuthorized,
            };

            if (storeId > 0)
            {
                model.UnzerApiKey_OverrideForStore = await _settingService.SettingExistsAsync(unzerPaymentSettings, setting => setting.UnzerApiKey, storeId);
                model.ShopUrl_OverrideForStore = await _settingService.SettingExistsAsync(unzerPaymentSettings, setting => setting.ShopUrl, storeId);                
                model.LogoImage_OverrideForStore = await _settingService.SettingExistsAsync(unzerPaymentSettings, setting => setting.LogoImage, storeId);
                model.ShopDescription_OverrideForStore = await _settingService.SettingExistsAsync(unzerPaymentSettings, setting => setting.ShopDescription, storeId);
                model.TagLine_OverrideForStore = await _settingService.SettingExistsAsync(unzerPaymentSettings, setting => setting.TagLine, storeId);
                model.PaymentMethods_OverrideForStore = await _settingService.SettingExistsAsync(unzerPaymentSettings, setting => setting.SelectedPaymentTypes, storeId);
                model.CurrencyCode_OverrideForStore = await _settingService.SettingExistsAsync(unzerPaymentSettings, setting => setting.CurrencyCode, storeId);
                model.EnforceCountryRestriction_OverrideForStore = await _settingService.SettingExistsAsync(unzerPaymentSettings, setting => setting.EnforceCountryRestriction, storeId);
                model.EnforceCurrencyRestriction_OverrideForStore = await _settingService.SettingExistsAsync(unzerPaymentSettings, setting => setting.EnforceCurrencyRestriction, storeId);
                model.LogCallbackPostData_OverrideForStore = await _settingService.SettingExistsAsync(unzerPaymentSettings, setting => setting.LogCallbackPostData, storeId);
                model.SkipPaymentInfo_OverrideForStore = await _settingService.SettingExistsAsync(unzerPaymentSettings, setting => setting.SkipPaymentInfo, storeId);
                model.AutoCapture_OverrideForStore = await _settingService.SettingExistsAsync(unzerPaymentSettings, setting => setting.AutoCapture, storeId);
                model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(unzerPaymentSettings, setting => setting.AdditionalFeePercentage, storeId);
                model.SendOrderConfirmOnAuthorized_OverrideForStore = await _settingService.SettingExistsAsync(unzerPaymentSettings, setting => setting.SendOrderConfirmOnAuthorized, storeId);
            }

            if (unzerPaymentSettings.AvailablePaymentTypes == null || !unzerPaymentSettings.AvailablePaymentTypes.Any())
            {
                unzerPaymentSettings.AvailablePaymentTypes = new List<string>();                
            }

            unzerPaymentSettings.AvailablePaymentTypes.Insert(0, "All");

            model.AvailablePaymentMethods = new SelectList(unzerPaymentSettings.AvailablePaymentTypes.Select(p => new { ID = p, Name = UnzerPaymentDefaults.MapPaymentTypeName(p) }), "ID", "Name");

            return View("~/Plugins/Payments.Unzer/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return await Configure();

            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<UnzerPaymentSettings>(storeId);

            var selectedPayments = model.SelectedPaymentTypes != null && model.SelectedPaymentTypes.Count > 1 ? model.SelectedPaymentTypes.Where(p => p != "All").ToList() : new List<string> { "All" };

            var apiKeyHasChanged = model.UnzerApiKey != null && model.UnzerApiKey != settings.UnzerApiKey;
            var countryRestrictionHasChanged = model.EnforceCountryRestriction != settings.EnforceCountryRestriction;

            settings.UnzerApiBaseUrl = model.UnzerApiBaseUrl;
            settings.UnzerTokenUrl = model.UnzerTokenUrl;
            settings.UnzerPaypageApiUrl = model.UnzerPaypageApiUrl;
            settings.UnzerApiKey = !string.IsNullOrEmpty(model.UnzerApiKey) ? model.UnzerApiKey : settings.UnzerApiKey;
            settings.ShopUrl = model.ShopUrl;
            settings.LogoImage = model.LogoImage;
            settings.ShopDescription = model.ShopDescription;
            settings.TagLine = model.TagLine;
            settings.SelectedPaymentTypes = selectedPayments;
            settings.CurrencyCode = model.CurrencyCode;
            settings.EnforceCountryRestriction = model.EnforceCountryRestriction;
            settings.EnforceCurrencyRestriction = model.EnforceCurrencyRestriction;
            settings.LogCallbackPostData = model.LogCallbackPostData;
            settings.SkipPaymentInfo = model.SkipPaymentInfo;            
            settings.AutoCapture = (AutoCapture)model.AutoCapture;
            settings.AdditionalFeePercentage = model.AdditionalFeePercentage;
            settings.SendOrderConfirmOnAuthorized = model.SendOrderConfirmOnAuthorized;

            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.UnzerApiBaseUrl, true, 0, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.UnzerTokenUrl, true, 0, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.UnzerPaypageApiUrl, true, 0, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.UnzerApiKey, model.UnzerApiKey_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.ShopUrl, model.ShopUrl_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.LogoImage, model.LogoImage_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.ShopDescription, model.ShopDescription_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.TagLine, model.TagLine_OverrideForStore, storeId, false);            
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.SelectedPaymentTypes, model.PaymentMethods_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.CurrencyCode, model.CurrencyCode_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.EnforceCountryRestriction, model.EnforceCountryRestriction_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.EnforceCurrencyRestriction, model.EnforceCurrencyRestriction_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.LogCallbackPostData, model.LogCallbackPostData_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.SkipPaymentInfo, model.SkipPaymentInfo_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.AutoCapture, model.AutoCapture_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.SendOrderConfirmOnAuthorized, model.SendOrderConfirmOnAuthorized_OverrideForStore, storeId, false);

            var placeOrderDelayKey = $"{UnzerPaymentDefaults.SystemName}.PlaceOrderDelay";
            if (model.SendOrderConfirmOnAuthorized_OverrideForStore || storeId == 0)
                await _settingService.SetSettingAsync(placeOrderDelayKey, settings.SendOrderConfirmOnAuthorized);
            else if (storeId > 0)
                await _settingService.SetSettingAsync(placeOrderDelayKey, false, storeId);

            if (apiKeyHasChanged || !settings.UnzerWebHooksSet)
            {
                _unzerApiService.UnzerPaymentSettings = settings;
                await ManageUnzerSettings(settings, apiKeyHasChanged);                
            }

            if(countryRestrictionHasChanged)
            {
                await HandleContryRestrictions(settings);
            }

            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        private async Task ManageUnzerSettings(UnzerPaymentSettings unzerPaymentSettings, bool apiKeyUpdated)
        {
            var store1 = await _storeContext.GetCurrentStoreAsync();
            var webHookUrl1 = $"{store1.Url.TrimEnd('/')}{Url.RouteUrl(UnzerPaymentDefaults.CallBackUrlRouteName)}".ToLowerInvariant();

            if (_unzerApiService.IsConfigured(unzerPaymentSettings))
            {
                try
                {
                    if (!unzerPaymentSettings.UnzerWebHooksSet || apiKeyUpdated)
                    {
                        var store = await _storeContext.GetCurrentStoreAsync();
                        var webHookUrl = $"{store.Url.TrimEnd('/')}{Url.RouteUrl(UnzerPaymentDefaults.CallBackUrlRouteName)}".ToLowerInvariant();

                        var curWebHooks = await _unzerApiService.GetWebHookEventsAsync();
                        if (curWebHooks.IsError)
                        {
                            await _logger.WarningAsync($"UnzerPaymentController.Config: Reading WebHooks unsuccessfull with {curWebHooks.ErrorResponse}");
                        }

                        //ensure the webhook is created
                        if (webHookUrl.Contains("localhost"))
                            webHookUrl = UnzerPaymentDefaults.DevCallbackUrl;

                        var eventList = new List<string>();

                        foreach (var eventType in UnzerPaymentDefaults.CallbackEvents)
                        {
                            if (!curWebHooks.Events.Any(e => e.Event == eventType.ToString() && e.Url == webHookUrl))
                            {
                                eventList.Add(eventType.ToString());
                            }
                        }

                        if (eventList.Any())
                        {
                            var result = await _unzerApiService.SetWebHookEventAsync(webHookUrl, eventList);
                            if (result.IsError)
                            {
                                var warning = await _localizationService.GetResourceAsync("Plugins.Payments.Unzer.Configuration.Webhook.Warning");
                                await _logger.WarningAsync($"UnzerPaymentController.Config: Updating WebHooks failed with {result.ErrorResponse}");
                                _notificationService.WarningNotification(warning, false);
                            }
                            else
                            {
                                unzerPaymentSettings.UnzerWebHooksSet = true;
                            }
                        }
                        else
                        {
                            unzerPaymentSettings.UnzerWebHooksSet = curWebHooks.Events.Count >= UnzerPaymentDefaults.CallbackEvents.Count();
                        }
                    }
                }
                catch (Exception ex)
                {
                    var warning = $"UnzerPaymentController.Config: Failed configuring Unzer Webhook with {ex.Message}";
                    _notificationService.WarningNotification(warning, false);
                    await _logger.ErrorAsync(warning, ex);
                }

                try
                {
                    if (string.IsNullOrEmpty(unzerPaymentSettings.UnzerMetadataId) || apiKeyUpdated)
                    {
                        unzerPaymentSettings.UnzerMetadataId = await SetUnzerMetadataAsync();
                    }
                }
                catch (Exception ex)
                {
                    var warning = $"UnzerPaymentController.Config: Failed configuring Unzer Metadata with {ex.Message}";
                    _notificationService.WarningNotification(warning, false);
                    await _logger.ErrorAsync(warning, ex);
                }

                try
                {
                    var allNoneActiveTypes = UnzerPaymentDefaults.AllNoneActivePaymentTypeByUnzerName();

                    var keyPairResult = await _unzerApiService.GetKeyPairAsync();
                    if (keyPairResult.IsError)
                    {
                        var warning = await _localizationService.GetResourceAsync("Plugins.Payments.Unzer.Configuration.KeyPair.Warning");
                        _notificationService.WarningNotification(warning, false);
                    }
                    unzerPaymentSettings.UnzerPublicApiKey = keyPairResult.publicKey;

                    unzerPaymentSettings.AvailablePaymentTypes = keyPairResult.availablePaymentTypes.Except(allNoneActiveTypes).ToList();
                }
                catch (Exception ex)
                {
                    var warning = $"UnzerPaymentController.Config: Failed getting Unzer KeyPair with {ex.Message}";
                    _notificationService.WarningNotification(warning, false);
                    await _logger.ErrorAsync(warning, ex);
                }

                await _settingService.SaveSettingAsync(unzerPaymentSettings);

                unzerPaymentSettings.AvailablePaymentTypes.Insert(0, "All");
            }
            else
            {
                if (unzerPaymentSettings.AvailablePaymentTypes == null || !unzerPaymentSettings.AvailablePaymentTypes.Any())
                {
                    unzerPaymentSettings.AvailablePaymentTypes = new List<string>();
                    unzerPaymentSettings.AvailablePaymentTypes.Insert(0, "All");
                }
            }

        }

        private async Task<string> SetUnzerMetadataAsync()
        {
            var metadatId = string.Empty;
            var metadataResult = await _unzerApiService.CreateMetadata();
            if (metadataResult.Success)
                metadatId = metadataResult.ResponseId;

            return metadatId;
        }

        private async Task HandleContryRestrictions(UnzerPaymentSettings unzerPaymentSettings)
        {
            var paymentMethods = unzerPaymentSettings.SelectedPaymentTypes.Select(p => UnzerPaymentDefaults.ReadPaymentTypeByUnzerName(p)).ToList();

            foreach (var method in paymentMethods.Where(p => p.CountryRestrictions.Any()))
            {
                var settingKey = string.Format(NopPaymentDefaults.RestrictedCountriesSettingName, method.SystemName);
                var unzerCountryRestricttions = await _settingService.GetSettingByKeyAsync<List<int>>(settingKey) ?? new List<int>();

                if (!unzerPaymentSettings.EnforceCountryRestriction && unzerCountryRestricttions.Any())
                {
                    await _settingService.SetSettingAsync<List<int>>(settingKey, new List<int>());
                    continue;
                }

                if (method.CountryRestrictions.Length != unzerCountryRestricttions.Count())
                {
                    foreach (var countryCode in method.CountryRestrictions)
                    {
                        var country = await _countryService.GetCountryByTwoLetterIsoCodeAsync(countryCode);
                        if (country == null)
                            continue;
                        if (unzerCountryRestricttions.Contains(country.Id))
                            continue;

                        unzerCountryRestricttions.Add(country.Id);
                    }

                    await _settingService.SetSettingAsync<List<int>>(settingKey, unzerCountryRestricttions);
                }
            }
        }
    }
}
