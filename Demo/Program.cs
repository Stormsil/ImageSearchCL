using ImageSearchCL.Demo;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title = "ImageSearchCL - Real Screen Demo";

Console.Clear();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║                                                          ║");
Console.WriteLine("║        ImageSearchCL v1.0.0 - Real Screen Demo          ║");
Console.WriteLine("║                                                          ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.ResetColor();
Console.WriteLine("\nПоиск кнопки на реальном экране...\n");

try
{
    await TestWithMyImage.RunRealScreen();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n❌ Ошибка: {ex.Message}");
    Console.WriteLine($"   Тип: {ex.GetType().Name}");
    Console.ResetColor();
}

Console.WriteLine("\nНажмите любую клавишу для выхода...");
Console.ReadKey();
