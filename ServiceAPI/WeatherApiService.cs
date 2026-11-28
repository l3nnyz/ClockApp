using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ClockApp.ServiceAPI
{
    public sealed record CurrentWeather(double Temperature, int Code, bool IsDay);

    public static class WeatherApiService
    {
        private static readonly HttpClient Http = new()
        {
            BaseAddress = new Uri("https://api.open-meteo.com/"),
            Timeout = TimeSpan.FromSeconds(10),
        };

        private sealed record Current(
            [property: JsonPropertyName("temperature_2m")] double Temperature,
            [property: JsonPropertyName("weather_code")] int WeatherCode,
            [property: JsonPropertyName("is_day")] int IsDay);

        private sealed record Response([property: JsonPropertyName("current")] Current? Current);

        public static async Task<CurrentWeather?> GetAsync(double latitude, double longitude, CancellationToken ct = default)
        {
            try
            {
                var lat = latitude.ToString(CultureInfo.InvariantCulture);
                var lng = longitude.ToString(CultureInfo.InvariantCulture);
                var resp = await Http.GetFromJsonAsync<Response>(
                    $"v1/forecast?latitude={lat}&longitude={lng}&current=temperature_2m,weather_code,is_day", ct);
                if (resp?.Current is not { } c) return null;
                return new CurrentWeather(c.Temperature, c.WeatherCode, c.IsDay == 1);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                Debug.WriteLine($"open-meteo.com ({latitude}, {longitude}) : {ex.Message}");
                return null;
            }
        }
    }
}
