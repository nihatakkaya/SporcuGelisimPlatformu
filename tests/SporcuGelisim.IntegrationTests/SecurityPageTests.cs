using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;

namespace SporcuGelisim.IntegrationTests;

public sealed class SecurityPageTests
{
    [Fact]
    public async Task Web_server_starts_even_when_database_is_unavailable()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Server=127.0.0.1,1;Database=Unavailable;User Id=invalid;Password=invalid;Connect Timeout=1;TrustServerCertificate=True"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var response = await client.GetAsync("/privacy", timeout.Token);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_user_is_redirected_from_dashboard()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/dashboard");
        Assert.True((int)response.StatusCode is 302 or 401);
    }

    [Fact]
    public async Task Anonymous_user_cannot_update_athlete_profile()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NationalIdentityNumber"] = "12345678901"
        });

        var response = await client.PostAsync("/athlete/profile/update", content);

        Assert.True((int)response.StatusCode is 302 or 401);
    }

    [Fact]
    public async Task Anonymous_user_cannot_assign_athlete_relation()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RelationType"] = "Coach",
            ["AthleteProfileId"] = Guid.NewGuid().ToString(),
            ["RelatedCoachUserId"] = Guid.NewGuid().ToString()
        });

        var response = await client.PostAsync("/admin/relations/assign", content);

        Assert.True((int)response.StatusCode is 302 or 401);
    }

    [Fact]
    public async Task Anonymous_user_cannot_send_feedback()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CoachUserId"] = Guid.NewGuid().ToString(),
            ["Comment"] = "Deneme mesajı"
        });

        var response = await client.PostAsync("/feedback/send", content);

        Assert.True((int)response.StatusCode is 302 or 401);
    }

    [Fact]
    public async Task Anonymous_user_cannot_send_related_feedback()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Target"] = $"{Guid.NewGuid()}:{Guid.NewGuid()}",
            ["Comment"] = "Deneme mesajı"
        });

        var response = await client.PostAsync("/feedback/send-related", content);

        Assert.True((int)response.StatusCode is 302 or 401);
    }

    [Fact]
    public async Task Anonymous_user_cannot_update_coach_profile()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["PhoneNumber"] = "0555 555 55 55"
        });

        var response = await client.PostAsync("/coach/profile/update", content);

        Assert.True((int)response.StatusCode is 302 or 401);
    }

    [Theory]
    [InlineData("/coach/athletes/add")]
    [InlineData("/coach/athletes/remove")]
    [InlineData("/coach/athletes/assign-parent")]
    public async Task Anonymous_user_cannot_manage_coach_athlete_relations(string path)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["AthleteProfileId"] = Guid.NewGuid().ToString(),
            ["ParentUserId"] = Guid.NewGuid().ToString()
        });

        var response = await client.PostAsync(path, content);

        Assert.True((int)response.StatusCode is 302 or 401);
    }

    [Theory]
    [InlineData("/coach/words")]
    [InlineData("/coach/athletes")]
    [InlineData("/coach/profile")]
    [InlineData("/Account/Register")]
    [InlineData("/parent/athletes")]
    [InlineData("/admin/relations")]
    [InlineData("/admin/coaches")]
    [InlineData("/admin/parents")]
    public async Task Anonymous_user_is_redirected_from_role_pages(string path)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        Assert.True((int)response.StatusCode is 302 or 401);
    }
}
