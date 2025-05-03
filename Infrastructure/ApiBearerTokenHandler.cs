using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Nop.Services.Logging;
using Unzer.Plugin.Payments.Unzer.Models.Api;

namespace Unzer.Plugin.Payments.Unzer.Infrastructure;
public class ApiBearerTokenHandler : DelegatingHandler
{
    private readonly UnzerPaymentSettings _unzerPaymentSettings;
    private readonly ILogger _logger;
    
    private JwtSecurityToken? _accessToken;

    public ApiBearerTokenHandler(UnzerPaymentSettings unzerPaymentSettings, ILogger logger)
    {
        _unzerPaymentSettings = unzerPaymentSettings;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_unzerPaymentSettings.UnzerApiKey))
            throw new ArgumentNullException(nameof(_unzerPaymentSettings.UnzerApiKey));

        if (!UnzerPaymentDefaults.UseBearerTokenUrls.Contains(request.RequestUri.AbsolutePath))
            return await base.SendAsync(request, cancellationToken);

        if (_accessToken == null || _accessToken.ValidTo < DateTime.Now)
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format("{0}:", _unzerPaymentSettings.UnzerApiKey)));
            HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Post, new Uri($"{UnzerPaymentDefaults.UnzerTokenUrl}/v1/auth/token"));
            msg.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await base.SendAsync(msg, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await _logger.WarningAsync($"ApiBearerTokenHandler: Requsesting new access token failed with {response.ReasonPhrase}");
                throw new ApplicationException($"Requsesting new access token failed with {response.ReasonPhrase}"); 
            }
            
            var authResult = await response.Content.ReadFromJsonAsync<AuthenticationTokenResponse>();
            if (authResult?.accessToken == null)
            {
                await _logger.WarningAsync("ApiBearerTokenHandler: Getting a token failed with no acceestoken response");
                throw new ApplicationException("Authenticated with success status code, but body was unexpected");
            }

            _accessToken = new JwtSecurityToken(authResult.accessToken);
        }

        // set the bearer token to the outgoing request
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken.RawData);

        // Proceed calling the inner handler, that will actually send the request
        // to our protected api
        return await base.SendAsync(request, cancellationToken);
    }
}
