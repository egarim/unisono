using MauiAppIntentsSample.Models;
using MauiAppIntentsSample.Services;

namespace MauiAppIntentsSample.Platforms.iOS;

/// <summary>
/// Donations are paused in M1: the per-intent Swift donation bridge was removed with the per-app binding.
/// M2's generated Swift will expose a fixed-name donate(intent, parametersJSON) entry point instead.
/// </summary>
public class IntentDonationService : IIntentDonationService
{
    public void DonateCreateTask(TaskItem task) { }
    public void DonateCompleteTask(TaskItem task) { }
    public void DonateOpenTask(TaskItem task) { }
    public void DonateSetDueDate(TaskItem task) { }
    public void DonateSearchTasks(string query) { }
    public void DeleteTaskDonations(string taskId) { }
}
