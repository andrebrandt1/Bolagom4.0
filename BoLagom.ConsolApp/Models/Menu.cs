using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace BoLagom.ConsolApp.Models
{
    partial class Menu
    {
        private static readonly HttpClient Client = new()
        {
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("BOLAGOM_API_URL") ?? "http://localhost:9000")
        };

        public static async Task CreateProperty()
        {
            Console.WriteLine("Skapa ny fastighet:");

            var property = new
            {
                Name = ReadRequiredValue("Namn"),
                Address = ReadRequiredValue("Adress"),
                Floors = ReadRequiredValue("Våningar"),
                PortCode = ReadRequiredValue("Portkod")
            };

            try
            {
                using HttpResponseMessage response = await Client.PostAsJsonAsync("/api/properties", property);
                if (!response.IsSuccessStatusCode)
                {
                    await WriteApiError(response, "spara fastigheten");
                    return;
                }

                Property? createdProperty = await response.Content.ReadFromJsonAsync<Property>();
                Console.WriteLine($"Fastigheten har skapats med ID {createdProperty?.Id}.");
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("Kunde inte nå API:t. Kontrollera att BoLagom.Api körs med HTTP-profilen på port 9000.");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Anropet tog för lång tid. Kontrollera om fastigheten sparades genom att visa alla fastigheter.");
            }
        }

        public static async Task DeleteProperty()
        {
            Console.WriteLine("Radera fastighet:");
            string input = ReadRequiredValue("Fastighetens ID (0 för att avbryta)");
            if (!int.TryParse(input, out int id) || id < 0)
            {
                Console.WriteLine("Ange ett positivt heltal som ID.");
                return;
            }

            if (id == 0)
            {
                Console.WriteLine("Raderingen avbröts.");
                return;
            }

            try
            {
                using HttpResponseMessage response = await Client.DeleteAsync($"/api/properties/{id}");
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"Ingen fastighet med ID {id} hittades.");
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    await WriteApiError(response, "radera fastigheten");
                    return;
                }

                Console.WriteLine($"Fastigheten med ID {id} har raderats.");
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("Kunde inte nå API:t. Kontrollera att BoLagom.Api körs med HTTP-profilen på port 9000.");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Anropet tog för lång tid. Kontrollera om fastigheten raderades genom att visa alla fastigheter.");
            }
        }

        public static async Task UpdateProperty()
        {
            Console.WriteLine("Uppdatera fastighet:");
            string input = ReadRequiredValue("Fastighetens ID (0 för att avbryta)");
            if (!int.TryParse(input, out int id) || id < 0)
            {
                Console.WriteLine("Ange ett positivt heltal som ID.");
                return;
            }

            if (id == 0)
            {
                Console.WriteLine("Uppdateringen avbröts.");
                return;
            }

            Console.WriteLine("Ange nya uppgifter för samtliga fält.");
            var property = new
            {
                Name = ReadRequiredValue("Namn"),
                Address = ReadRequiredValue("Adress"),
                Floors = ReadRequiredValue("Våningar"),
                PortCode = ReadRequiredValue("Portkod")
            };

            try
            {
                using HttpResponseMessage response = await Client.PutAsJsonAsync($"/api/properties/{id}", property);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"Ingen fastighet med ID {id} hittades.");
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    await WriteApiError(response, "uppdatera fastigheten");
                    return;
                }

                Console.WriteLine($"Fastigheten med ID {id} har uppdaterats.");
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("Kunde inte nå API:t. Kontrollera att BoLagom.Api körs med HTTP-profilen på port 9000.");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Anropet tog för lång tid. Kontrollera om fastigheten uppdaterades genom att visa alla fastigheter.");
            }
        }

        private static string ReadRequiredValue(string label)
        {
            while (true)
            {
                Console.Write($"{label}: ");
                string? value = Console.ReadLine();
                if (value is null)
                {
                    throw new EndOfStreamException("Inmatningen avslutades.");
                }
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }

                Console.WriteLine("Fältet måste fyllas i.");
            }
        }

        public static async Task ShowAllProperties()
        {
            while (true)
            {
                ClearScreen();
                List<Property> properties;
                try
                {
                    using HttpResponseMessage response = await Client.GetAsync("/api/properties");
                    if (!response.IsSuccessStatusCode)
                    {
                        await WriteApiError(response, "hämta fastigheterna");
                        WaitForReturn();
                        return;
                    }

                    properties = await response.Content.ReadFromJsonAsync<List<Property>>() ?? [];
                }
                catch (HttpRequestException)
                {
                    Console.WriteLine("Kunde inte nå API:t. Kontrollera att BoLagom.Api körs.");
                    WaitForReturn();
                    return;
                }
                catch (TaskCanceledException)
                {
                    Console.WriteLine("Anropet tog för lång tid. Fastigheterna kunde inte hämtas.");
                    WaitForReturn();
                    return;
                }

                Console.WriteLine("Alla fastigheter:");
                foreach (Property property in properties)
                {
                    Console.WriteLine($"{property.Id}. {property.Name}");
                    Console.WriteLine($" Adress: {property.Address}. Våningar: {property.Floors}");
                    Console.WriteLine();
                }

                if (properties.Count == 0)
                {
                    Console.WriteLine("Inga fastigheter hittades.");
                    WaitForReturn();
                    return;
                }

                while (true)
                {
                    Console.Write("Välj fastighetens ID (0 för att gå tillbaka): ");
                    string? input = Console.ReadLine();
                    if (input is null || input.Trim() == "0")
                    {
                        return;
                    }

                    Property? selectedProperty = int.TryParse(input, out int id) && id > 0
                        ? properties.Find(property => property.Id == id)
                        : null;
                    if (selectedProperty is null)
                    {
                        Console.WriteLine("Välj ett fastighets-ID som finns i listan.");
                        continue;
                    }

                    await ShowSelectedPropertyMenu(selectedProperty);
                    break;
                }
            }
        }
    }
}
