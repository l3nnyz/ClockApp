using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;

namespace ClockApp.ServiceAPI
{
    /// <summary>
    /// Récupère l'heure d'un fuseau via l'API publique https://timeapi.io
    /// et renvoie l'écart avec l'horloge UTC du PC.
    /// </summary>
    public static class TimeApiService
    {
        private static readonly HttpClient Http = new()
        {
            BaseAddress = new Uri("https://timeapi.io/"),
            Timeout = TimeSpan.FromSeconds(10),
        };

        private sealed record TimeApiResponse(string DateTime, string TimeZone);

        /// <returns>L'écart (heure de la ville − UTC du PC), ou null si l'API est injoignable.</returns>
        public static async Task<TimeSpan?> GetOffsetAsync(string timeZoneId, CancellationToken ct = default)
        {
            try
            {
                var start = System.DateTime.UtcNow;
                var sw = Stopwatch.StartNew();
                var resp = await Http.GetFromJsonAsync<TimeApiResponse>(
                    $"api/time/current/zone?timeZone={Uri.EscapeDataString(timeZoneId)}", ct);
                sw.Stop();
                if (resp is null) return null;

                // On compense la latence réseau en prenant le milieu de la requête.
                var requestMidpoint = start + sw.Elapsed / 2;
                var remote = System.DateTime.Parse(resp.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.None);
                return remote - requestMidpoint;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or FormatException)
            {
                Debug.WriteLine($"timeapi.io ({timeZoneId}) : {ex.Message}");
                return null;
            }
        }
    }
}
