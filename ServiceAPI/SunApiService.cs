using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ClockApp.ServiceAPI
{
    public static class SunApiService
    {
        private static readonly HttpClient Http = new()
        {
            BaseAddress = new Uri("https://api.sunrise-sunset.org/"),
            Timeout = TimeSpan.FromSeconds(10),
        };

        private sealed record SunResults(
            [property: JsonPropertyName("sunrise")] string Sunrise,
            [property: JsonPropertyName("sunset")] string Sunset,
            [property: JsonPropertyName("day_length")] long DayLength);

        private sealed record SunResponse(
            [property: JsonPropertyName("results")] SunResults? Results,
            [property: JsonPropertyName("status")] string Status);

        public static async Task<(bool Ok, (DateTime Rise, DateTime Set)? Sun)> GetAsync(
            DateOnly date, double latitude, double longitude, CancellationToken ct = default)
        {
            try
            {
                var lat = latitude.ToString(CultureInfo.InvariantCulture);
                var lng = longitude.ToString(CultureInfo.InvariantCulture);
                var resp = await Http.GetFromJsonAsync<SunResponse>(
                    $"json?lat={lat}&lng={lng}&date={date:yyyy-MM-dd}&formatted=0", ct);
                if (resp is not { Status: "OK", Results: { } r }) return (false, null);

                var rise = DateTime.Parse(r.Sunrise, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
                var set = DateTime.Parse(r.Sunset, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
                if (r.DayLength <= 0 || rise.Year < 2000 || set <= rise) return (true, null);
                return (true, (rise, set));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or FormatException)
            {
                Debug.WriteLine($"sunrise-sunset.org ({latitude}, {longitude}) : {ex.Message}");
                return (false, null);
            }
        }
    }
}
