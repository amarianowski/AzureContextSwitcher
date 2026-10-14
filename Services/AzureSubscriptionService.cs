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
            // Try preferred options in order: pwsh (PowerShell Core) -> az CLI -> powershell.exe (Windows PowerShell)
            var attempts = new List<string>();

            // Helper to run an external process with timeout and capture output
            async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessAsync(string fileName, string arguments, int timeoutMs = 30000)
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                try
                {
                    using var process = Process.Start(startInfo);
                    if (process is null)
                        return (-1, string.Empty, $"Failed to start process: {fileName}");

                    var waitTask = process.WaitForExitAsync();
                    var completed = await Task.WhenAny(waitTask, Task.Delay(timeoutMs));
                    if (completed != waitTask)
                    {
                        try { process.Kill(true); } catch { }
                        return (-1, string.Empty, $"Process timed out: {fileName} {arguments}");
                    }

                    var stdOut = await process.StandardOutput.ReadToEndAsync();
                    var stdErr = await process.StandardError.ReadToEndAsync();
                    return (process.ExitCode, stdOut, stdErr);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    return (-1, string.Empty, ex.Message);
                }
            }

            var errors = new List<string>();

            // 1) Try pwsh (PowerShell Core) and explicitly import Az.Accounts
            try
            {
                var pwshArgs = $"-NoProfile -NonInteractive -Command \"Import-Module Az.Accounts -Force; Set-AzContext -SubscriptionId '{subscriptionId}'\"";
                var (code, outText, errText) = await RunProcessAsync("pwsh.exe", pwshArgs);
                if (code == 0)
                    return;

                errors.Add($"pwsh failed: exit={code}, err={errText}");
            }
            catch (Exception ex)
            {
                errors.Add($"pwsh attempt threw: {ex.Message}");
            }

            // 2) Try az CLI
            try
            {
                var azArgs = $"account set --subscription {subscriptionId}";
                var (code, outText, errText) = await RunProcessAsync("az", azArgs);
                if (code == 0)
                    return;

                errors.Add($"az cli failed: exit={code}, err={errText}");
            }
            catch (Exception ex)
            {
                errors.Add($"az attempt threw: {ex.Message}");
            }

            // 3) Fallback to Windows PowerShell (powershell.exe)
            try
            {
                var psArgs = $"-NoProfile -NonInteractive -Command \"Import-Module Az.Accounts -Force; Set-AzContext -SubscriptionId '{subscriptionId}'\"";
                var (code, outText, errText) = await RunProcessAsync("powershell.exe", psArgs);
                if (code == 0)
                    return;

                errors.Add($"powershell failed: exit={code}, err={errText}");
            }
            catch (Exception ex)
            {
                errors.Add($"powershell attempt threw: {ex.Message}");
            }

            throw new InvalidOperationException($"Failed to switch Azure context. Attempts: {string.Join(" | ", errors)}");
        }
    }
}

