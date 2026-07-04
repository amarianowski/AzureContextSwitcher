namespace AzureContextSwitcher.Models
{
    public class SubscriptionInfo
    {
        public required string SubscriptionId { get; set; }
        public required string DisplayName { get; set; }
        public string? State { get; set; }

        public override string ToString()
        {
            return $"{DisplayName} ({SubscriptionId})";
        }
    }
}
