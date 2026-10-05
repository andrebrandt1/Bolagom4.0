namespace BoLagom.ConsolApp.Models;

partial class Menu
{
    public static async Task ShowPropertyMenu()
    {
        while (true)
        {
            ClearScreen();
            Console.WriteLine("Fastighet");
            Console.WriteLine();
            Console.WriteLine("1. Visa alla fastigheter");
            Console.WriteLine("2. Skapa ny fastighet");
            Console.WriteLine("3. Radera fastighet");
            Console.WriteLine("4. Uppdatera fastighet");
            Console.WriteLine("0. Tillbaka");
            Console.Write("Välj: ");

            try
            {
                switch (Console.ReadLine()?.Trim())
                {
                    case "1":
                        await ShowAllProperties();
                        break;
                    case "2":
                        ClearScreen();
                        await CreateProperty();
                        WaitForReturn();
                        break;
                    case "3":
                        ClearScreen();
                        await DeleteProperty();
                        WaitForReturn();
                        break;
                    case "4":
                        ClearScreen();
                        await UpdateProperty();
                        WaitForReturn();
                        break;
                    case "0":
                    case null:
                        return;
                    default:
                        Console.WriteLine("Du valde ett ogiltigt menyval.");
                        WaitForReturn();
                        break;
                }
            }
            catch (EndOfStreamException)
            {
                return;
            }
        }
    }

    private static async Task ShowSelectedPropertyMenu(Property property)
    {
        while (true)
        {
            ClearScreen();
            Console.WriteLine($"Fastighet: {property.Name} (ID {property.Id})");
            Console.WriteLine($"Adress: {property.Address}");
            Console.WriteLine();
            if (!await ShowApartments(property.Id))
            {
                WaitForReturn();
                return;
            }
            Console.WriteLine();
            Console.WriteLine("1. Skapa lägenhet");
            Console.WriteLine("0. Tillbaka till fastighetslistan");
            Console.Write("Välj: ");

            switch (Console.ReadLine()?.Trim())
            {
                case "1":
                    ClearScreen();
                    bool propertyExists = await CreateApartment(property);
                    WaitForReturn();
                    if (!propertyExists)
                    {
                        return;
                    }
                    break;
                case "0":
                case null:
                    return;
                default:
                    Console.WriteLine("Du valde ett ogiltigt menyval.");
                    WaitForReturn();
                    break;
            }
        }
    }

    private static void ClearScreen()
    {
        if (!Console.IsOutputRedirected)
        {
            Console.Clear();
            Console.CursorVisible = true;
        }
    }

    private static void WaitForReturn()
    {
        Console.WriteLine();
        Console.WriteLine("Tryck på valfri tangent för att återgå till menyn.");
        if (Console.IsInputRedirected)
        {
            Console.ReadLine();
        }
        else
        {
            Console.ReadKey(true);
        }
    }
}
