using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Payments;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Services.Plugins;

namespace Unzer.Plugin.Payments.Unzer.Services;
public class UnzerPaymentPluginManager : PaymentPluginManager
{
    protected readonly PaymentSettings _paymentSettings;
    protected readonly ISettingService _settingService;
    protected readonly IPluginService _pluginService;
    protected readonly ICurrencyService _currencyService;
    protected readonly ILocalizationService _localizationService;
    protected UnzerPaymentSettings _unzerPaymentSettings;

    protected Dictionary<string, IList<IPaymentMethod>> _plugins = new();
    public UnzerPaymentPluginManager(ICustomerService customerService, IPluginService pluginService, ISettingService settingService, PaymentSettings paymentSettings, UnzerPaymentSettings unzerPaymentSettings, ILocalizationService localizationService, ICurrencyService currencyService) : base(customerService, pluginService, settingService, paymentSettings)
    {
        _paymentSettings = paymentSettings;
        _pluginService = pluginService;
        _currencyService = currencyService;
        _settingService = settingService;
        _localizationService = localizationService;
        _unzerPaymentSettings = unzerPaymentSettings;
    }

    public async override Task<IList<IPaymentMethod>> LoadActivePluginsAsync(Customer customer = null, int storeId = 0, int countryId = 0)
    {
        var activePlugins = await LoadActivePluginsAsync(_paymentSettings.ActivePaymentMethodSystemNames, customer, storeId);

        activePlugins = (await BuildUnzerPaymentMethods(activePlugins.ToList(), customer, storeId)).ToList();

        //filter by country
        if (countryId > 0)
            activePlugins = await activePlugins.WhereAwait(async method => !(await GetRestrictedCountryIdsAsync(method)).Contains(countryId)).ToListAsync();

        if(_unzerPaymentSettings.EnforceCurrencyRestriction)
        {
            var currencyId = 0;
            if (!string.IsNullOrEmpty(_unzerPaymentSettings.CurrencyCode))
            {
                var unzerCurrency = _currencyService.GetCurrencyByCodeAsync(_unzerPaymentSettings.CurrencyCode);
                currencyId = unzerCurrency != null ? unzerCurrency.Id : 0;                
            }

            if (currencyId == 0 && customer != null)
            {
                currencyId = customer.CurrencyId.HasValue ? customer.CurrencyId.Value : 0;
            }

            if(currencyId > 0)
            {
                activePlugins = await activePlugins.WhereAwait(async method => !(await GetRestrictedCurrencyIdsAsync(method)).Contains(currencyId)).ToListAsync();
            }
        }

        return activePlugins;
    }

    //public override async Task<IList<IPaymentMethod>> LoadActivePluginsAsync(List<string> systemNames, Customer customer = null, int storeId = 0)
    //{
    //    if (systemNames == null)
    //        return new List<IPaymentMethod>();

    //    //get loaded plugins according to passed system names
    //    return (await LoadAllPluginsAsync(customer, storeId))
    //        .Where(plugin => systemNames.Any(sn => plugin.PluginDescriptor.SystemName.StartsWith(sn, true, System.Globalization.CultureInfo.InvariantCulture)))
    //        .ToList();
    //}

    //public override async Task<IList<IPaymentMethod>> LoadAllPluginsAsync(Customer customer = null, int storeId = 0)
    //{
    //    var allPaymentMethods = await base.LoadAllPluginsAsync(customer, storeId);
    //    var unzerPayemntMethods = new List<IPaymentMethod>();

    //    var key = await GetKeyAsync(customer, storeId);

    //    if (!_plugins.Any())
    //        unzerPayemntMethods = (await BuildUnzerPaymentMethods(allPaymentMethods.ToList(), customer, storeId)).ToList();

    //    return unzerPayemntMethods;
    //}

    public override async Task<IPaymentMethod> LoadPluginBySystemNameAsync(string systemName, Customer customer = null, int storeId = 0)
    {
        if (string.IsNullOrEmpty(systemName))
            return null;

        if (!systemName.Contains(UnzerPaymentDefaults.SystemName))
            return await base.LoadPluginBySystemNameAsync(systemName, customer, storeId);

        //try to get already loaded plugin
        var key = await GetKeyAsync(customer, storeId, systemName);
        if (_plugins.ContainsKey(key))
            return _plugins[key].FirstOrDefault();

        var qpSystemName = UnzerPaymentDefaults.SystemName;

        //or get it from list of all loaded plugins or load it for the first time
        var pluginBySystemName = _plugins.TryGetValue(await GetKeyAsync(customer, storeId), out var plugins)
            && plugins.FirstOrDefault(plugin =>
                plugin.PluginDescriptor.SystemName.Equals(systemName, StringComparison.InvariantCultureIgnoreCase)) is IPaymentMethod loadedPlugin
            ? loadedPlugin
            : (await _pluginService.GetPluginDescriptorBySystemNameAsync<IPaymentMethod>(qpSystemName, customer: customer, storeId: storeId))?.Instance<IPaymentMethod>();

        if (pluginBySystemName.PluginDescriptor.SystemName.Equals(systemName))
        {
            _plugins.Add(key, new List<IPaymentMethod> { pluginBySystemName });
            return pluginBySystemName;
        }

        var curDescriptor = ClonePluginDescriptor(pluginBySystemName.PluginDescriptor);
        var payMeth = systemName.Split('.').Last();
        var friendlyName = UnzerPaymentDefaults.MapPaymentTypeName(payMeth); ;
        curDescriptor.SystemName = systemName;
        curDescriptor.FriendlyName = friendlyName;

        var qpPaymentClone = curDescriptor.Instance<IPaymentMethod>();

        _plugins.Add(key, new List<IPaymentMethod> { qpPaymentClone });

        return qpPaymentClone;
    }

    public override bool IsPluginActive(IPaymentMethod paymentMethod)
    {
        if (paymentMethod.PluginDescriptor.SystemName.Contains(UnzerPaymentDefaults.SystemName))
        {
            return _paymentSettings.ActivePaymentMethodSystemNames
                ?.Any(systemName => UnzerPaymentDefaults.SystemName.Equals(systemName, StringComparison.InvariantCultureIgnoreCase))
                ?? false;
        }

        return base.IsPluginActive(paymentMethod);
    }

    public override async Task<bool> IsPluginActiveAsync(string systemName, Customer customer = null, int storeId = 0)
    {
        var paymentMethod = await LoadPluginBySystemNameAsync(systemName, customer, storeId);
        return IsPluginActive(paymentMethod);

    }

    public override async Task<IList<int>> GetRestrictedCountryIdsAsync(IPaymentMethod paymentMethod)
    {
        ArgumentNullException.ThrowIfNull(paymentMethod);

        if (paymentMethod.PluginDescriptor.SystemName.Contains(UnzerPaymentDefaults.SystemName))
        {
            var settingKey = string.Format(NopPaymentDefaults.RestrictedCountriesSettingName, UnzerPaymentDefaults.SystemName);

            return await _settingService.GetSettingByKeyAsync<List<int>>(settingKey) ?? new List<int>();
        }

        return await base.GetRestrictedCountryIdsAsync(paymentMethod);
    }

    private async Task<IList<int>> GetRestrictedCurrencyIdsAsync(IPaymentMethod paymentMethod)
    {
        ArgumentNullException.ThrowIfNull(paymentMethod);

        var restrictList = new List<int>();

        if (paymentMethod.PluginDescriptor.SystemName.Contains(UnzerPaymentDefaults.SystemName))
        {
            var settingKey = string.Format(UnzerPaymentDefaults.RestrictedCurrencySettingName, UnzerPaymentDefaults.SystemName);

            return await _settingService.GetSettingByKeyAsync<List<int>>(settingKey) ?? new List<int>();
        }

        return new List<int>();
    }

    public async override Task<string> GetPluginLogoUrlAsync(IPaymentMethod meth)
    {
        var logoUrl = await base.GetPluginLogoUrlAsync(meth);

        if (!meth.PluginDescriptor.SystemName.Contains(UnzerPaymentDefaults.SystemName))
            return logoUrl;

        var methName = meth.PluginDescriptor.SystemName.Split(".");
        if (methName.Count() > 2)
        {
            var subName = methName.Last();
            logoUrl = logoUrl.Replace("logo.jpg", $"{subName}.png");
        }

        return logoUrl;
    }

    private async Task<IList<IPaymentMethod>> BuildUnzerPaymentMethods(List<IPaymentMethod> activePlugins, Customer customer = null, int storeId = 0)
    {
        var allPaymentPlugins = new List<IPaymentMethod>(activePlugins);

        var unzerPlugin = allPaymentPlugins.SingleOrDefault(p => p.PluginDescriptor.SystemName.Contains(UnzerPaymentDefaults.SystemName));
        if (unzerPlugin != null)
        {
            var allSuppTypes = UnzerPaymentDefaults.AllSupplementPaymentTypeByUnzerName();
            if (_unzerPaymentSettings.SelectedPaymentTypes.Except(allSuppTypes).Count() > 1)
            {
                var systemName = unzerPlugin.PluginDescriptor.SystemName;
                allPaymentPlugins.Remove(unzerPlugin);

                foreach (var paymentMethod in _unzerPaymentSettings.SelectedPaymentTypes)
                {
                    if (allPaymentPlugins.Any(p => p.PluginDescriptor.SystemName.Contains(paymentMethod)))
                        continue;

                    var friendlyName = UnzerPaymentDefaults.MapPaymentTypeName(paymentMethod);

                    var curDescriptor = ClonePluginDescriptor(unzerPlugin.PluginDescriptor);

                    curDescriptor.SystemName = $"{systemName}.{paymentMethod}";
                    curDescriptor.FriendlyName = friendlyName;

                    var methDesc = await _localizationService.GetResourceAsync("Plugins.Payments.Unzer.PaymentMethod.Description");
                    curDescriptor.Description = string.Format(methDesc, friendlyName);

                    var qpPaymentClone = curDescriptor.Instance<IPaymentMethod>();

                    allPaymentPlugins.Add(qpPaymentClone);

                    var key = await GetKeyAsync(customer, storeId);
                    if (_plugins.ContainsKey(key))
                    {
                        _plugins[key].Add(qpPaymentClone);
                    }
                }
            }
        }

        return allPaymentPlugins;
    }

    private PluginDescriptor ClonePluginDescriptor(PluginDescriptor toClone)
    {
        return new PluginDescriptor
        {
            AssemblyFileName = toClone.AssemblyFileName,
            Author = toClone.Author,
            DependsOn = toClone.DependsOn,
            Description = toClone.Description,
            DisplayOrder = toClone.DisplayOrder,
            FriendlyName = toClone.FriendlyName,
            Group = toClone.Group,
            Installed = toClone.Installed,
            LimitedToCustomerRoles = toClone.LimitedToCustomerRoles,
            SupportedVersions = toClone.SupportedVersions,
            OriginalAssemblyFile = toClone.OriginalAssemblyFile,
            LimitedToStores = toClone.LimitedToStores,
            PluginFiles = toClone.PluginFiles,
            PluginType = toClone.PluginType,
            ReferencedAssembly = toClone.ReferencedAssembly,
            ShowInPluginsList = toClone.ShowInPluginsList,
            SystemName = toClone.SystemName,
            Version = toClone.Version
        };
    }
}
