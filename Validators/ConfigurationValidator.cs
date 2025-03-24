using FluentValidation;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;
using Unzer.Plugin.Payments.Unzer.Models;

namespace Unzer.Plugin.Payments.Unzer.Validators
{
    /// <summary>
    /// Represents configuration model validator
    /// </summary>
    public class ConfigurationValidator : BaseNopValidator<ConfigurationModel>
    {
        #region Ctor

        public ConfigurationValidator(ILocalizationService localizationService)
        {
            RuleFor(model => model.UnzerApiBaseUrl)
                .NotEmpty()
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Unzer.Fields.GateWayURL.Required"));

            RuleFor(model => model.UnzerApiKey)
                .NotEmpty()
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Unzer.Fields.UnzerApiKey.Required"));

            RuleFor(model => model.AutoCapture)
                .IsInEnum()
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Unzer.Fields.AutoCapture.Required"));
        }

        #endregion
    }
}