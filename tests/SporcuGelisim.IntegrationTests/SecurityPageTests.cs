using Microsoft.AspNetCore.Mvc.Testing;

namespace SporcuGelisim.IntegrationTests;

public sealed class SecurityPageTests
{
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

    [Theory]
    [InlineData("/coach/words")]
    [InlineData("/coach/athletes")]
    [InlineData("/parent/athletes")]
    [InlineData("/admin/relations")]
    public async Task Anonymous_user_is_redirected_from_role_pages(string path)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        Assert.True((int)response.StatusCode is 302 or 401);
    }
}
