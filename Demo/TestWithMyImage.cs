using System.Drawing;
using ImageSearchCL.API;

namespace ImageSearchCL.Demo;

/// <summary>
/// Простое демо для тестирования с ВАШИМ изображением кнопки на РЕАЛЬНОМ экране.
/// </summary>
public static class TestWithMyImage
{
    /// <summary>
    /// Запускает тест с реальным экраном
    /// </summary>
    public static async Task RunRealScreen()
    {
        var (refImage, imageName) = LoadImage();
        if (refImage == null) return;

        await TestWithRealScreen(refImage, imageName);
        refImage.Dispose();
    }

    private static (ReferenceImage?, string) LoadImage()
    {
        Console.WriteLine("📋 Загрузка изображения из MyImages/...\n");

        // Ищем папку MyImages
        string? myImagesPath = FindMyImagesFolder();

        if (myImagesPath == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Папка MyImages/ не найдена!");
            Console.ResetColor();
            Console.WriteLine($"\n🔍 Текущая директория: {Directory.GetCurrentDirectory()}");
            Console.WriteLine("\n📝 Инструкция:");
            Console.WriteLine("   1. Создайте папку MyImages в директории Demo/");
            Console.WriteLine("   2. Положите туда .png файл вашей кнопки");
            Console.WriteLine("   3. Запустите этот тест снова\n");
            return (null, "");
        }

        Console.WriteLine($"✅ Найдена папка: {Path.GetFullPath(myImagesPath)}\n");

        // Ищем PNG файлы
        var imageFiles = Directory.GetFiles(myImagesPath, "*.png");

        if (imageFiles.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️  В папке MyImages/ нет .png файлов!");
            Console.ResetColor();
            Console.WriteLine($"📂 Полный путь: {Path.GetFullPath(myImagesPath)}");
            Console.WriteLine("\n📸 Как сделать скриншот кнопки:");
            Console.WriteLine("   1. Нажмите Win + Shift + S");
            Console.WriteLine("   2. Выделите вашу кнопку мышкой");
            Console.WriteLine("   3. Откройте Paint (Win + R → mspaint)");
            Console.WriteLine("   4. Вставьте Ctrl + V");
            Console.WriteLine("   5. Сохраните: File → Save As → PNG");
            Console.WriteLine($"   6. Выберите папку: {Path.GetFullPath(myImagesPath)}");
            Console.WriteLine("   7. Назовите: button.png\n");
            return (null, "");
        }

        // Берем первое изображение
        var imagePath = imageFiles[0];
        var imageName = Path.GetFileName(imagePath);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✅ Найдено изображение: {imageName}");
        Console.ResetColor();
        Console.WriteLine($"   📁 Путь: {Path.GetFullPath(imagePath)}");

        // Загружаем изображение
        ReferenceImage? refImage = null;
        try
        {
            refImage = ReferenceImage.FromFile(imagePath);
            Console.WriteLine($"   📏 Размер: {refImage.Width}x{refImage.Height} пикселей");
            Console.WriteLine($"   🎨 Формат: {refImage.PixelFormat}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Ошибка загрузки изображения: {ex.Message}");
            Console.ResetColor();
            return (null, "");
        }

        // Рекомендации по размеру
        if (refImage.Width < 20 || refImage.Height < 20)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n⚠️  Изображение очень маленькое (< 20x20)!");
            Console.WriteLine("   Рекомендуется: 50x50 до 200x200 пикселей");
            Console.ResetColor();
        }
        else if (refImage.Width > 300 || refImage.Height > 300)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n⚠️  Изображение очень большое (> 300x300)!");
            Console.WriteLine("   Template matching будет медленным");
            Console.WriteLine("   Рекомендуется: уменьшите до 150x150");
            Console.ResetColor();
        }

        return (refImage, imageName);
    }

    private static async Task TestWithRealScreen(ReferenceImage refImage, string imageName)
    {
        Console.WriteLine("\n" + new string('─', 60));
        Console.WriteLine("🖥️  РЕЖИМ РЕАЛЬНОГО ЗАХВАТА");
        Console.WriteLine(new string('─', 60));
        Console.WriteLine("\n✅ Используем WindowCaptureCL для захвата!\n");

        // Выбор источника захвата
        Console.WriteLine("📺 Источник захвата:");
        Console.WriteLine("   1. Весь экран (монитор)");
        Console.WriteLine("   2. Окно Notepad");
        Console.Write("\nВыберите (1 или 2, по умолчанию 1): ");
        var sourceChoice = Console.ReadLine();
        bool captureNotepad = sourceChoice == "2";

        Console.Write("\n⚙️  Confidence threshold (0.7-0.95, рекомендуется 0.85): ");
        var confidenceInput = Console.ReadLine();
        var confidence = double.TryParse(confidenceInput, out var conf) ? conf : 0.85;

        Console.Write("\n⏱️  Время поиска в секундах (10-60, рекомендуется 30): ");
        var durationInput = Console.ReadLine();
        var duration = int.TryParse(durationInput, out var dur) ? dur : 30;

        Console.Write("\n🎨 Показать Debug Overlay (прозрачные рамки на экране)? (y/n, рекомендуется y): ");
        var enableOverlay = Console.ReadLine()?.ToLower() == "y";

        if (enableOverlay)
        {
            ImageSearchCL.API.ImageSearchConfiguration.EnableDebugOverlay = true;
            ImageSearchCL.API.ImageSearchConfiguration.DebugOverlayColor = System.Drawing.Color.Lime;
            ImageSearchCL.API.ImageSearchConfiguration.DebugOverlayThickness = 3;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Debug Overlay включен - вы увидите зеленые рамки вокруг найденных объектов!");
            Console.ResetColor();
        }

        Console.WriteLine("\n" + new string('─', 60));
        Console.ForegroundColor = ConsoleColor.Yellow;
        if (captureNotepad)
        {
            Console.WriteLine("⚠️  ВАЖНО: Убедитесь что Notepad запущен и окно видно!");
        }
        else
        {
            Console.WriteLine("⚠️  ВАЖНО: Убедитесь что кнопка видна на экране!");
            Console.WriteLine("   Если кнопка на этом же мониторе - сверните консоль после старта.");
        }
        Console.ResetColor();
        Console.WriteLine(new string('─', 60));

        Console.Write("\nГотовы начать? (y/n): ");
        if (Console.ReadLine()?.ToLower() != "y")
        {
            Console.WriteLine("Отменено.");
            return;
        }

        try
        {
            ScreenCaptureAdapter? capture = null;

            if (captureNotepad)
            {
                // Ищем окно Notepad
                Console.WriteLine("\n🔍 Поиск окна Notepad...");
                var notepadWindows = WindowFinder.FindAllNotepadWindows();

                if (notepadWindows.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Notepad не найден! Запустите Notepad и попробуйте снова.");
                    Console.ResetColor();
                    return;
                }

                IntPtr windowHandle;
                if (notepadWindows.Count == 1)
                {
                    windowHandle = notepadWindows[0].Handle;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ Найдено окно Notepad: \"{notepadWindows[0].Title}\"");
                    Console.ResetColor();
                }
                else
                {
                    // Несколько окон Notepad - предлагаем выбрать
                    Console.WriteLine($"\n📋 Найдено {notepadWindows.Count} окон Notepad:");
                    for (int i = 0; i < notepadWindows.Count; i++)
                    {
                        Console.WriteLine($"   {i + 1}. {notepadWindows[i].Title}");
                    }
                    Console.Write($"\nВыберите окно (1-{notepadWindows.Count}): ");
                    var choiceStr = Console.ReadLine();
                    if (!int.TryParse(choiceStr, out int choice) || choice < 1 || choice > notepadWindows.Count)
                    {
                        Console.WriteLine("Неверный выбор. Используется первое окно.");
                        choice = 1;
                    }
                    windowHandle = notepadWindows[choice - 1].Handle;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ Выбрано окно: \"{notepadWindows[choice - 1].Title}\"");
                    Console.ResetColor();
                }

                Console.WriteLine("\n▶️  Запуск захвата окна Notepad...\n");
                capture = ScreenCaptureAdapter.FromWindow(windowHandle, targetFps: 15);

                // Set window handle for overlay coordinate conversion
                if (enableOverlay)
                {
                    ImageSearchCL.API.ImageSearchConfiguration.DebugOverlayWindowHandle = windowHandle;
                }
            }
            else
            {
                Console.WriteLine("\n▶️  Запуск захвата экрана...\n");
                capture = ScreenCaptureAdapter.FromScreen(monitorIndex: 0, targetFps: 15);

                // Reset window handle for screen capture
                if (enableOverlay)
                {
                    ImageSearchCL.API.ImageSearchConfiguration.DebugOverlayWindowHandle = IntPtr.Zero;
                }
            }

            using (capture)
            {
                Console.WriteLine($"📺 Захват: {capture.FrameWidth}x{capture.FrameHeight}");
                Console.WriteLine($"🎯 Ищем кнопку: {refImage.Width}x{refImage.Height}");
                Console.WriteLine($"🔧 Confidence: {confidence:P0}");
                Console.WriteLine($"⏱️  Длительность: {duration} сек\n");

                // Создаем сессию отслеживания
                using var session = Search.For(refImage)
                    .WithConfidence(confidence)
                    .WithMovementThreshold(5.0)
                    .In(capture);

                var foundCount = 0;
                var lastFoundTime = DateTime.MinValue;

                session.Appeared += (s, result) =>
                {
                    foundCount++;
                    lastFoundTime = DateTime.Now;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n✅ КНОПКА НАЙДЕНА! (находка #{foundCount})");
                    Console.WriteLine($"   📍 Позиция: ({result.X}, {result.Y})");
                    Console.WriteLine($"   🎯 Confidence: {result.Confidence:P1}");
                    Console.WriteLine($"   📐 Размер: {result.Width}x{result.Height}");
                    Console.WriteLine($"   🎯 Центр: ({result.Center.X}, {result.Center.Y})");
                    Console.WriteLine($"   📍 TopLeft: ({result.TopLeft.X}, {result.TopLeft.Y})");
                    Console.WriteLine($"   📍 BottomRight: ({result.BottomRight.X}, {result.BottomRight.Y})");
                    Console.ResetColor();
                };

                session.Disappeared += (s, result) =>
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\n⚠️  Кнопка исчезла с экрана");
                    Console.ResetColor();
                };

                session.Moved += (s, e) =>
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"🔄 Кнопка переместилась: ({e.OldResult.X},{e.OldResult.Y}) → ({e.NewResult.X},{e.NewResult.Y}), расстояние: {e.Distance:F1}px");
                    Console.ResetColor();
                };

                session.Start();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("🔍 Поиск активен... Наблюдаем за экраном...");
                Console.ResetColor();
                Console.WriteLine("   (Нажмите Ctrl+C для остановки)\n");

                // Ждем указанное время
                for (int i = 0; i < duration; i++)
                {
                    await Task.Delay(1000);

                    if (i % 5 == 0 && i > 0)
                    {
                        var timeSinceFound = foundCount > 0
                            ? $"(последнее обнаружение {(DateTime.Now - lastFoundTime).TotalSeconds:F0}с назад)"
                            : "";
                        Console.WriteLine($"⏱️  {i}с прошло... Найдено: {foundCount} раз {timeSinceFound}");
                    }
                }

                session.Stop();

                // Отключить overlay
                if (enableOverlay)
                {
                    ImageSearchCL.API.ImageSearchConfiguration.EnableDebugOverlay = false;
                }

                Console.WriteLine("\n" + new string('─', 60));
                Console.WriteLine("📊 РЕЗУЛЬТАТЫ:");
                Console.WriteLine(new string('─', 60));
                Console.WriteLine($"⏱️  Время поиска: {duration} сек");
                Console.WriteLine($"✅ Обнаружений: {foundCount}");

                if (foundCount == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n⚠️  Кнопка не найдена.");
                    Console.WriteLine("\n💡 Попробуйте:");
                    Console.WriteLine($"   • Понизить Confidence (попробуйте {Math.Max(0.7, confidence - 0.1):F2})");
                    Console.WriteLine("   • Убедитесь что кнопка видна на экране");
                    Console.WriteLine("   • Проверьте что размер кнопки на экране совпадает с изображением");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n✅ УСПЕХ! Кнопка успешно обнаружена на экране!");
                    Console.WriteLine("\n💡 Что дальше:");
                    Console.WriteLine("   • Используйте result.Center для клика мышью");
                    Console.WriteLine("   • Используйте другие anchor points для точного позиционирования");
                    Console.WriteLine($"   • Текущий Confidence ({confidence:P0}) работает хорошо");
                    Console.ResetColor();
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Ошибка: {ex.Message}");
            Console.WriteLine($"   Тип: {ex.GetType().Name}");
            Console.ResetColor();

            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Подробнее: {ex.InnerException.Message}");
            }
        }

        Console.WriteLine("\n✓ Тест завершен!");
    }

    /// <summary>
    /// Ищет папку MyImages в нескольких возможных местах
    /// </summary>
    private static string? FindMyImagesFolder()
    {
        var possiblePaths = new[]
        {
            "MyImages",
            Path.Combine("..", "MyImages"),
            Path.Combine("..", "..", "MyImages"),
            Path.Combine("..", "..", "..", "MyImages"),
            Path.Combine("Demo", "MyImages"),
            Path.Combine("..", "Demo", "MyImages"),
        };

        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
