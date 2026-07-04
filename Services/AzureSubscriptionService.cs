using Azure.Identity;
using Azure.ResourceManager;
using AzureContextSwitcher.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AzureContextSwitcher.Services
{
    public class AzureSubscriptionService
    {
        private readonly ArmClient _armClient;

        public AzureSubscriptionService()
        {
            var credential = new DefaultAzureCredential();
            _armClient = new ArmClient(credential);
        }

        public async Task<List<SubscriptionInfo>> GetSubscriptionsAsync()
        {
            var subscriptions = new List<SubscriptionInfo>();

            try
            {
                await foreach (var subscription in _armClient.GetSubscriptions())
                {
                    subscriptions.Add(new SubscriptionInfo
                    {
                        SubscriptionId = subscription.Data.SubscriptionId,
                        DisplayName = subscription.Data.DisplayName,
                        State = subscription.Data.State?.ToString()
                    });
                }
            }
            catch (Azure.Identity.AuthenticationFailedException ex)
            {
                throw new InvalidOperationException(
                    "Failed to authenticate with Azure. Please run 'az login' or 'Connect-AzAccount' first.", 
                    ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve subscriptions: {ex.Message}", ex);
            }

            return subscriptions;
        }

        public async Task SetCurrentSubscriptionAsync(string subscriptionId)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"Set-AzContext -SubscriptionId '{subscriptionId}'\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Failed to switch Azure context: {error}");
            }
        }
    }
}

