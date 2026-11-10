using MauiAppIntentsSample.Models;

namespace MauiAppIntentsSample.Services;

/// <summary>Donations are an iOS-only concern in this sample; other platforms register this.</summary>
public sealed class NoOpIntentDonationService : IIntentDonationService
{
    public void DonateCreateTask(TaskItem task) { }
    public void DonateCompleteTask(TaskItem task) { }
    public void DonateOpenTask(TaskItem task) { }
    public void DonateSetDueDate(TaskItem task) { }
    public void DonateSearchTasks(string query) { }
    public void DeleteTaskDonations(string taskId) { }
}
