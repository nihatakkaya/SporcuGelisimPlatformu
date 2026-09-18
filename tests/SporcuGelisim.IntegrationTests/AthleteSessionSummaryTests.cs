using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SporcuGelisim.Web.Components.Shared;

namespace SporcuGelisim.IntegrationTests;

public sealed class AthleteSessionSummaryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(50)]
    public async Task Summary_keeps_constant_markup_and_links_to_athletes_history(int count)
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var athleteId = Guid.NewGuid();
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<AthleteSessionSummary>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(AthleteSessionSummary.AthleteId)] = athleteId,
                [nameof(AthleteSessionSummary.Count)] = count,
                [nameof(AthleteSessionSummary.LastSessionDate)] = count == 0 ? null : new DateTimeOffset(2026, 9, 18, 0, 0, 0, TimeSpan.FromHours(3))
            }));
            return System.Net.WebUtility.HtmlDecode(component.ToHtmlString());
        });
        Assert.Contains($"Toplam {count} oturum", html);
        Assert.Contains($"/coach/athletes/{athleteId}/sessions", html);
        Assert.Contains("Oturumları Gör", html);
        Assert.Contains(count == 0 ? "Henüz oturum yok" : "18.09.2026", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<p\\b"));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "<a\\b").Count);
        Assert.DoesNotContain("<li", html);
    }
}
