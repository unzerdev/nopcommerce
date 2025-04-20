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
    private readonly HttpClient _tokenClient;
    private readonly UnzerPaymentSettings _unzerPaymentSettings;
    private readonly ILogger _logger;
    
    private JwtSecurityToken? _accessToken;

    public ApiBearerTokenHandler(HttpClient tokenClient, UnzerPaymentSettings unzerPaymentSettings, ILogger logger)
    {
        _tokenClient = tokenClient
            ?? throw new ArgumentNullException(nameof(tokenClient));

        _unzerPaymentSettings = unzerPaymentSettings;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        if (!UnzerPaymentDefaults.UseBearerTokenUrls.Contains(request.RequestUri.AbsolutePath))
            return await base.SendAsync(request, cancellationToken);

        if (_accessToken == null || _accessToken.ValidTo < DateTime.Now)
        {
            if (!string.IsNullOrEmpty(_unzerPaymentSettings.UnzerApiKey))
            {
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format("{0}:", _unzerPaymentSettings.UnzerApiKey)));
                _tokenClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", string.Format("Basic {0}", credentials));
            }

            HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/token");
            var response = await _tokenClient.SendAsync(msg, cancellationToken);
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
