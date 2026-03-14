using System.ComponentModel.DataAnnotations.Schema;

namespace subscription_api.Models;

public class Subscription
{
    public int UserId { get; set; }
    public string Plan { get; set; } = "Free";
    public int MonthlyQuota { get; set; }
    public int UsedThisMonth { get; set; }
    public DateTime? SubscriptionEndDate { get; set; }

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }  // Added ? for nullable
}