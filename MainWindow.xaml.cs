using AzureContextSwitcher.Models;
using AzureContextSwitcher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace AzureContextSwitcher
{
    public sealed partial class MainWindow : Window
    {
        private readonly AzureSubscriptionService _subscriptionService;

        public MainWindow()
        {
            InitializeComponent();

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32(500, 600));

            _subscriptionService = new AzureSubscriptionService();
            LoadSubscriptionsAsync();
        }

        private async void LoadSubscriptionsAsync()
        {
            try
            {
                LoadingProgressRing.IsActive = true;
                LoadingProgressRing.Visibility = Visibility.Visible;
                ErrorTextBlock.Visibility = Visibility.Collapsed;
                SubscriptionsListView.Visibility = Visibility.Collapsed;

                var subscriptions = await _subscriptionService.GetSubscriptionsAsync();

                SubscriptionsListView.ItemsSource = subscriptions;
                SubscriptionsListView.Visibility = Visibility.Visible;

                LoadingProgressRing.IsActive = false;
                LoadingProgressRing.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LoadingProgressRing.IsActive = false;
                LoadingProgressRing.Visibility = Visibility.Collapsed;
                ErrorTextBlock.Text = $"Error: {ex.Message}";
                ErrorTextBlock.Visibility = Visibility.Visible;
            }
        }

        private async void SubscriptionsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SubscriptionsListView.SelectedItem is SubscriptionInfo selectedSubscription)
            {
                try
                {
                    LoadingProgressRing.IsActive = true;
                    LoadingProgressRing.Visibility = Visibility.Visible;
                    ErrorTextBlock.Visibility = Visibility.Collapsed;

                    await _subscriptionService.SetCurrentSubscriptionAsync(selectedSubscription.SubscriptionId);

                    LoadingProgressRing.IsActive = false;
                    LoadingProgressRing.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    LoadingProgressRing.IsActive = false;
                    LoadingProgressRing.Visibility = Visibility.Collapsed;
                    ErrorTextBlock.Text = $"Error switching subscription: {ex.Message}";
                    ErrorTextBlock.Visibility = Visibility.Visible;
                }
            }
        }
    }
}
