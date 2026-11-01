using System.Net.Http.Headers;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    App = "Wachat Test",
    Status = "Running"
}));

app.MapGet("/health", () => Results.Ok("Healthy"));

app.MapPost("/send", async (
    IHttpClientFactory factory,
    IConfiguration config) =>
{
    var token = config["WA_TOKEN"];
    var phoneId = config["WA_PHONE_ID"];
    var recipient = config["WA_RECIPIENT"];
    var version = config["WA_API_VERSION"] ?? "v26.0";

    if (string.IsNullOrWhiteSpace(token) ||
        string.IsNullOrWhiteSpace(phoneId) ||
        string.IsNullOrWhiteSpace(recipient))
        return Results.Problem("Missing WhatsApp configuration");

    var client = factory.CreateClient();

    var url = $"https://graph.facebook.com/{version}/{phoneId}/messages";

    using var request = new HttpRequestMessage(HttpMethod.Post, url);

    request.Headers.Authorization =
        new AuthenticationHeaderValue("Bearer", token);

    request.Content = JsonContent.Create(new
    {
        messaging_product = "whatsapp",
        recipient_type = "individual",
        to = recipient,
        type = "text",
        text = new
        {
            body = "مرحباً! تم نجاح الربط الآلي مع منصة Wachat."
        }
    });

    using var response = await client.SendAsync(request);

    var result = await response.Content.ReadAsStringAsync();

    return Results.Content(
        result,
        "application/json",
        statusCode: (int)response.StatusCode);
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            using var scope = app.Services.CreateScope();

            var factory = scope.ServiceProvider
                .GetRequiredService<IHttpClientFactory>();

            var config = scope.ServiceProvider
                .GetRequiredService<IConfiguration>();

            var token = config["WA_TOKEN"];
            var phoneId = config["WA_PHONE_ID"];
            var recipient = config["WA_RECIPIENT"];
            var version = config["WA_API_VERSION"] ?? "v26.0";

            if (string.IsNullOrWhiteSpace(token) ||
                string.IsNullOrWhiteSpace(phoneId) ||
                string.IsNullOrWhiteSpace(recipient))
            {
                app.Logger.LogWarning(
                    "WhatsApp configuration is incomplete.");
                return;
            }

            var client = factory.CreateClient();

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://graph.facebook.com/{version}/{phoneId}/messages");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            request.Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = recipient,
                type = "text",
                text = new
                {
                    body = "مرحباً! تم تشغيل منصة Wachat بنجاح."
                }
            });

            using var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
                app.Logger.LogInformation(
                    "Automatic WhatsApp message accepted.");
            else
                app.Logger.LogError(
                    "WhatsApp error: {Status} {Body}",
                    (int)response.StatusCode,
                    await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex,
                "Automatic WhatsApp message failed.");
        }
    });
});

app.Run();
