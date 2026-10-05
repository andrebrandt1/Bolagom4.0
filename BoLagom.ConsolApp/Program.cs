using BoLagom.ConsolApp.Models;

Console.InputEncoding = System.Text.Encoding.UTF8;
Console.OutputEncoding = System.Text.Encoding.UTF8;

ConsoleColor[] rainbow =
{
    ConsoleColor.Red,
    ConsoleColor.DarkYellow,
    ConsoleColor.Yellow,
    ConsoleColor.Green,
    ConsoleColor.Cyan,
    ConsoleColor.Blue,
    ConsoleColor.Magenta
};

string title = "BoLagom 4.0";
int frame = 0;

while (true)
{
    if (!Console.IsOutputRedirected)
    {
        Console.Clear();
        Console.CursorVisible = false;
    }

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine(new string('═', 40));
    Console.WriteLine();
    Console.WriteLine(new string('═', 40));
    Console.ResetColor();

    Console.WriteLine();
    Console.WriteLine("1. Fastighet");
    Console.WriteLine("0. Avsluta");
    Console.WriteLine();

    // Fortsätt animera tills användaren trycker på en tangent.
    while (!Console.IsInputRedirected && !Console.KeyAvailable)
    {
        Console.SetCursorPosition((40 - title.Length) / 2, 1);

        for (int i = 0; i < title.Length; i++)
        {
            Console.ForegroundColor = rainbow[(i + frame) % rainbow.Length];
            Console.Write(title[i]);
        }

        Console.ResetColor();
        frame++;
        Thread.Sleep(120);
    } 
    
    string? input = Console.IsInputRedirected
        ? Console.ReadLine()
        : Console.ReadKey(true).KeyChar.ToString();
    if (!Console.IsOutputRedirected)
    {
        Console.Clear();
        Console.CursorVisible = true;
    }

    if (input == "1")
    {
        await Menu.ShowPropertyMenu();
    }
    else if (input is "0" or null)
    {
        Console.WriteLine("Programmet avslutas.");
        break;
    }

    else
    {
        Console.WriteLine("Du valde ett ogiltigt menyval.");
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

