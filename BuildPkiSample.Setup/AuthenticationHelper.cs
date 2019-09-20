using System;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using ConsoleTools;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Rest;

namespace BuildPkiSample.Setup
{
    internal class AuthenticationHelper
    {
        private static readonly string[] Scopes = { "https://management.azure.com/user_impersonation" };

        private readonly string _clientId;
        private readonly string _tenantId;

        public AuthenticationHelper(string clientId, string tenantId)
        {
            _clientId = clientId;
            _tenantId = tenantId;
        }

        public async Task<AcquireTokenResult> AcquireTokenAsync()
        {
            var app = PublicClientApplicationBuilder.Create(_clientId).WithTenantId(_tenantId).WithDefaultRedirectUri().Build();

            var storageCreationProperties = new StorageCreationPropertiesBuilder("tokenCache.dat", ".", _clientId).Build();
            (await MsalCacheHelper.CreateAsync(storageCreationProperties)).RegisterCache(app.UserTokenCache);
            var account = await GetAccountAsync(app);
            AuthenticationResult authenticationResult;
            try
            {
                authenticationResult = await app.AcquireTokenSilent(Scopes, account).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                authenticationResult = await app.AcquireTokenWithDeviceCode(Scopes, deviceCodeResult =>
                {
                    Console.WriteLine(deviceCodeResult.Message);
                    return deviceCodeResult.VerificationUrl == null ? Task.CompletedTask : OpenBrowserAsync(deviceCodeResult.VerificationUrl);
                }).ExecuteAsync();
            }

            return CreateAcquireTokenResult(authenticationResult);
        }

        private AcquireTokenResult CreateAcquireTokenResult(AuthenticationResult authenticationResult)
        {
            var credentials = new AzureCredentials(
                new TokenCredentials(authenticationResult.AccessToken),
                new TokenCredentials(authenticationResult.AccessToken),
                _tenantId,
                AzureEnvironment.AzureGlobalCloud);
            return new AcquireTokenResult(credentials, ExtractObjectId(authenticationResult.IdToken));

            static string ExtractObjectId(string idToken)
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                JwtSecurityToken jwtToken = tokenHandler.ReadJwtToken(idToken);
                return jwtToken.Claims.First(claim => claim.Type == "oid").Value;
            }
        }

        private static async Task<IAccount?> GetAccountAsync(IPublicClientApplication app)
        {
            var accounts = await app.GetAccountsAsync();
            var accountList = accounts.ToList();
            switch (accountList.Count)
            {
                case 0:
                    return null;
                case 1:
                    return accountList[0];
            }

            IAccount? result = null;
            var menu = new ConsoleMenu()
                .AddRange(accountList.Select(currentAccount => new Tuple<string, Action>(currentAccount.Username, () => result = currentAccount)))
                .Configure(config => { config.Title = "Choose an account"; });
            menu.Show();

            return result;
        }

        private static Task OpenBrowserAsync(string url)
        {
            var processStartInfo = new ProcessStartInfo {FileName = url, UseShellExecute = true};
            Process.Start(processStartInfo);
            return Task.CompletedTask;
        }

    }
}