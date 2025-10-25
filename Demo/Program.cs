using ImageSearchCL.Demo;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title = "ImageSearchCL - Demo Suite";

while (true)
{
    ShowMenu();
    Console.Write("\nВыберите сценарий (0-6): ");
    var choice = Console.ReadLine();

    try
    {
        switch (choice)
        {
            case "0":
                Console.WriteLine("\nДо свидания!");
                return;

            case "1":
                await DemoScenarios.Scenario1_BasicTracking();
                break;

            case "2":
                await DemoScenarios.Scenario2_ContinuousFindAll();
                break;

            case "3":
                await DemoScenarios.Scenario3_ContinuousTracking();
                break;

            case "4":
                await DemoScenarios.Scenario4_MultiTemplate();
                break;

            case "5":
                await DemoScenarios.Scenario5_WindowCapture();
                break;

            case "6":
                await DemoScenarios.Scenario6_WaitUntil();
                break;

            default:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n⚠️  Неверный выбор. Попробуйте снова.");
                Console.ResetColor();
                await Task.Delay(1500);
                continue;
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n❌ Ошибка: {ex.Message}");
        Console.WriteLine($"   Тип: {ex.GetType().Name}");
        if (ex.StackTrace != null)
        {
            Console.WriteLine($"   Stack: {ex.StackTrace.Split('\n')[0]}");
        }
        Console.ResetColor();
    }

    Console.WriteLine("\n" + new string('─', 70));
    Console.WriteLine("Нажмите любую клавишу для возврата в меню...");
    Console.ReadKey();
}

static void ShowMenu()
{
    Console.Clear();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("╔" + new string('═', 70) + "╗");
    Console.WriteLine("║" + "        ImageSearchCL v1.0.0 - Демонстрация всех возможностей        ".PadRight(71) + "║");
    Console.WriteLine("╚" + new string('═', 70) + "╝");
    Console.ResetColor();
    Console.WriteLine();

    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("📋 ДОСТУПНЫЕ СЦЕНАРИИ:\n");
    Console.ResetColor();

    PrintMenuItem("1", "Базовое отслеживание", "Search.For() - простейший пример с Appeared/Disappeared");
    PrintMenuItem("2", "Мониторинг всех вхождений", "FindAll() в потоке - отслеживание ВСЕХ кнопок в реальном времени");
    PrintMenuItem("3", "Полное отслеживание", "Search.For() с Appeared/Moved/Disappeared + статистика");
    PrintMenuItem("4", "Мультишаблонный поиск", "Search.ForAny() - несколько шаблонов, MultiFindResult");
    PrintMenuItem("5", "Захват окна + Overlay", "WindowCapture с точными координатами (DWM API)");
    PrintMenuItem("6", "Синхронное ожидание", "WaitUntilVisible/WaitUntilNotVisible - блокирующие методы");

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("    0. Выход");
    Console.ResetColor();
}

static void PrintMenuItem(string number, string title, string description)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write($"    {number}. ");
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(title);
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"       {description}");
    Console.ResetColor();
    Console.WriteLine();
}
