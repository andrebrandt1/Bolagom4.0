using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BoLagom.ConsolApp.Models;

partial class Menu
{
    // False means the selected property no longer exists.
    private static async Task<bool> ShowApartments(int propertyId)
    {
        Console.WriteLine("Befintliga lägenheter:");
        try
        {
            using HttpResponseMessage response = await Client.GetAsync($"/api/properties/{propertyId}/apartments");
            if (!response.IsSuccessStatusCode)
            {
                await WriteApiError(response, "hämta lägenheterna");
                return response.StatusCode != HttpStatusCode.NotFound;
            }

            List<Apartment> apartments = await response.Content.ReadFromJsonAsync<List<Apartment>>() ?? [];
            if (apartments.Count == 0)
            {
                Console.WriteLine("Inga lägenheter finns i fastigheten ännu.");
            }
            else
            {
                CultureInfo culture = CultureInfo.GetCultureInfo("sv-SE");
                foreach (Apartment apartment in apartments)
                {
                    Console.WriteLine($"ID {apartment.Id} | Lägenhet {apartment.ApartmentNumber} | " +
                        $"Boyta: {apartment.LivingArea.ToString("0.#", culture)} m² | " +
                        $"Hyra: {apartment.MonthlyRent.ToString("N0", culture)} kr/månad");
                }
            }
        }
        catch (HttpRequestException)
        {
            Console.WriteLine("Kunde inte hämta lägenheterna. Kontrollera att BoLagom.Api körs.");
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Anropet tog för lång tid. Lägenheterna kunde inte hämtas.");
        }

        return true;
    }

    // False means the selected property no longer exists; return to the property list.
    private static async Task<bool> CreateApartment(Property property)
    {
        Console.WriteLine($"Skapa lägenhet i {property.Name} (ID {property.Id}):");
        var request = new
        {
            ApartmentNumber = ReadApartmentNumber(),
            LivingArea = ReadLivingArea(),
            MonthlyRent = ReadMonthlyRent()
        };

        try
        {
            using HttpResponseMessage response = await Client.PostAsJsonAsync(
                $"/api/properties/{property.Id}/apartments", request);
            if (!response.IsSuccessStatusCode)
            {
                await WriteApiError(response, "spara lägenheten");
                return response.StatusCode != HttpStatusCode.NotFound;
            }

            Apartment? apartment = await response.Content.ReadFromJsonAsync<Apartment>();
            Console.WriteLine($"Lägenheten har skapats med ID {apartment?.Id} i {property.Name}.");
        }
        catch (HttpRequestException)
        {
            Console.WriteLine("Kunde inte nå API:t. Kontrollera att BoLagom.Api körs.");
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Anropet tog för lång tid. Det är osäkert om lägenheten sparades. Ingen automatisk omsändning görs.");
        }

        return true;
    }

    private static string ReadApartmentNumber()
    {
        while (true)
        {
            string value = ReadRequiredValue("Lägenhetsnummer");
            if (value.Length <= 5)
            {
                return value;
            }

            Console.WriteLine("Lägenhetsnumret får vara högst fem tecken.");
        }
    }

    private static decimal ReadLivingArea()
    {
        while (true)
        {
            string value = ReadRequiredValue("Boyta i m²").Replace(',', '.');
            if (decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out decimal livingArea) &&
                livingArea > 0 && livingArea <= 999.9m && decimal.Round(livingArea, 1) == livingArea)
            {
                return livingArea;
            }

            Console.WriteLine("Ange en boyta större än 0 och högst 999,9 m², med högst en decimal och utan tusentalsavgränsare.");
        }
    }

    private static int ReadMonthlyRent()
    {
        while (true)
        {
            string value = ReadRequiredValue("Månadshyra i kronor");
            if (int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                out int monthlyRent) && monthlyRent >= 0)
            {
                return monthlyRent;
            }

            Console.WriteLine("Ange månadshyran som ett heltal som är minst 0.");
        }
    }

    private static async Task WriteApiError(HttpResponseMessage response, string operation)
    {
        Console.WriteLine($"API:t kunde inte {operation} (HTTP {(int)response.StatusCode}).");
        string body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (root.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty field in errors.EnumerateObject())
                {
                    if (field.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement message in field.Value.EnumerateArray())
                        {
                            Console.WriteLine(message.GetString());
                        }
                    }
                }
            }
            else if (root.TryGetProperty("message", out JsonElement message) ||
                root.TryGetProperty("detail", out message) || root.TryGetProperty("title", out message))
            {
                Console.WriteLine(message.GetString());
            }
        }
        catch (JsonException)
        {
            // An unexpected response format must not hide the HTTP status above.
        }
    }
}
