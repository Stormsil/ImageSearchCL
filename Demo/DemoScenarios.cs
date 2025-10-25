using System.Drawing;
using ImageSearchCL.API;

namespace ImageSearchCL.Demo;

/// <summary>
/// Демонстрация всех возможностей библиотеки ImageSearchCL
/// </summary>
public static class DemoScenarios
{
    /// <summary>
    /// Сценарий 1: Базовый пример отслеживания
    /// Самый простой код - только Appeared событие
    /// </summary>
    public static async Task Scenario1_BasicTracking()
    {
        PrintHeader("СЦЕНАРИЙ 1: Базовый пример отслеживания");
        Console.WriteLine("Минимальный код для отслеживания кнопки.\n");

        var templates = LoadTemplates();
        if (templates.Length == 0) return;

        Console.Write("⏱️  Время отслеживания (10-60 сек, по умолчанию 15): ");
        var durInput = Console.ReadLine();
        var duration = int.TryParse(durInput, out var d) ? d : 15;

        Console.WriteLine("\n▶️  Запуск отслеживания...\n");

        using var capture = ScreenCaptureAdapter.FromScreen(0, targetFps: 30);
        using var session = Search.For(templates[0].Image)
            .WithConfidence(0.85)
            .In(capture);

        var foundCount = 0;

        session.Appeared += (s, result) =>
        {
            foundCount++;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ [{DateTime.Now:HH:mm:ss}] Кнопка найдена! Позиция: ({result.X}, {result.Y}), Confidence: {result.Confidence:P1}");
            Console.ResetColor();
        };

        session.Disappeared += (s, result) =>
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  [{DateTime.Now:HH:mm:ss}] Кнопка исчезла");
            Console.ResetColor();
        };

        session.Start();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"🔍 Отслеживание на {duration} секунд...");
        Console.WriteLine("💡 Откройте/закройте блокнот чтобы увидеть события!");
        Console.ResetColor();
        Console.WriteLine();

        await Task.Delay(duration * 1000);

        session.Stop();

        Console.WriteLine($"\n✅ Обнаружений: {foundCount}");
        templates[0].Image.Dispose();
    }

    /// <summary>
    /// Сценарий 2: Непрерывный мониторинг всех кнопок
    /// Отслеживает ВСЕ кнопки на экране в реальном времени (FindAll в потоке)
    /// </summary>
    public static async Task Scenario2_ContinuousFindAll()
    {
        PrintHeader("СЦЕНАРИЙ 2: Мониторинг всех вхождений в реальном времени");
        Console.WriteLine("Непрерывное отслеживание ВСЕХ кнопок на экране.");
        Console.WriteLine("Откройте несколько блокнотов!\n");

        var templates = LoadTemplates();
        if (templates.Length == 0) return;

        Console.Write("⏱️  Время мониторинга (10-60 сек, по умолчанию 20): ");
        var durInput = Console.ReadLine();
        var duration = int.TryParse(durInput, out var d) ? d : 20;

        Console.WriteLine("\n📝 Инструкция:");
        Console.WriteLine("   1. Откройте 2-3 окна Блокнота");
        Console.WriteLine("   2. Расположите их так чтобы кнопки были видны");
        Console.WriteLine("   3. Попробуйте открывать/закрывать блокноты во время мониторинга\n");

        Console.Write("Готово? (Enter для старта)");
        Console.ReadLine();

        Console.WriteLine("\n▶️  Запуск непрерывного мониторинга...\n");

        using var capture = ScreenCaptureAdapter.FromScreen(0, targetFps: 10); // Lower FPS for FindAll
        var cts = new CancellationTokenSource();
        var monitoringTask = Task.Run(async () =>
        {
            Bitmap? currentFrame = null;

            capture.FrameReady += (s, e) =>
            {
                if (e.Frame != null)
                {
                    var oldFrame = Interlocked.Exchange(ref currentFrame, (Bitmap)e.Frame.Clone());
                    oldFrame?.Dispose();
                }
            };

            capture.Start();

            while (!cts.Token.IsCancellationRequested)
            {
                var frame = Interlocked.Exchange(ref currentFrame, null);
                if (frame != null)
                {
                    try
                    {
                        var results = ImageSearch.FindAll(
                            templates[0].Image,
                            frame,
                            confidence: 0.8,
                            overlapThreshold: 0.5
                        );

                        // Clear previous line and print count
                        Console.Write($"\r🔍 Найдено кнопок: {results.Count}   ");

                        if (results.Count > 0)
                        {
                            Console.Write($"| Позиции: ");
                            for (int i = 0; i < Math.Min(results.Count, 3); i++)
                            {
                                Console.Write($"({results[i].X},{results[i].Y}) ");
                            }
                            if (results.Count > 3)
                            {
                                Console.Write($"+ еще {results.Count - 3}");
                            }
                        }
                    }
                    finally
                    {
                        frame.Dispose();
                    }
                }

                await Task.Delay(500, cts.Token);
            }

            capture.Stop();
            currentFrame?.Dispose();
        }, cts.Token);

        Console.WriteLine("💡 Открывайте/закрывайте блокноты - счетчик обновляется в реальном времени!\n");

        await Task.Delay(duration * 1000);
        cts.Cancel();

        try
        {
            await monitoringTask;
        }
        catch (OperationCanceledException) { }

        Console.WriteLine("\n\n✅ Мониторинг завершен!");
        templates[0].Image.Dispose();
    }

    /// <summary>
    /// Сценарий 3: Непрерывное отслеживание (Search.For)
    /// Отслеживает появление, движение и исчезновение кнопки
    /// </summary>
    public static async Task Scenario3_ContinuousTracking()
    {
        PrintHeader("СЦЕНАРИЙ 3: Непрерывное отслеживание (Search.For)");
        Console.WriteLine("Отслеживание кнопки в реальном времени.");
        Console.WriteLine("Попробуйте открыть/закрыть блокнот, двигать окно!\n");

        var templates = LoadTemplates();
        if (templates.Length == 0) return;

        Console.WriteLine("⚙️  Настройки:");
        Console.Write("   Confidence (0.7-0.95, по умолчанию 0.85): ");
        var confInput = Console.ReadLine();
        var confidence = double.TryParse(confInput, out var c) ? c : 0.85;

        Console.Write("   Время отслеживания в секундах (10-120, по умолчанию 30): ");
        var durInput = Console.ReadLine();
        var duration = int.TryParse(durInput, out var d) ? d : 30;

        // Enable overlay
        ImageSearchConfiguration.EnableDebugOverlay = true;
        ImageSearchConfiguration.DebugOverlayColor = Color.Lime;
        ImageSearchConfiguration.DebugOverlayThickness = 3;

        Console.WriteLine("\n▶️  Запуск отслеживания...\n");

        using var capture = ScreenCaptureAdapter.FromScreen(0, targetFps: 30);
        using var session = Search.For(templates[0].Image)
            .WithConfidence(confidence)
            .WithMovementThreshold(5.0)
            .In(capture);

        var stats = new TrackingStats();

        session.Appeared += (s, result) =>
        {
            stats.AppearCount++;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n✅ [{DateTime.Now:HH:mm:ss}] Кнопка ПОЯВИЛАСЬ!");
            Console.WriteLine($"   📍 Позиция: ({result.X}, {result.Y})");
            Console.WriteLine($"   🎯 Confidence: {result.Confidence:P1}");
            Console.ResetColor();
        };

        session.Disappeared += (s, result) =>
        {
            stats.DisappearCount++;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n⚠️  [{DateTime.Now:HH:mm:ss}] Кнопка ИСЧЕЗЛА!");
            Console.WriteLine($"   📍 Последняя позиция: ({result.X}, {result.Y})");
            Console.ResetColor();
        };

        session.Moved += (s, e) =>
        {
            stats.MoveCount++;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"🔄 [{DateTime.Now:HH:mm:ss}] Кнопка ПЕРЕМЕСТИЛАСЬ:");
            Console.WriteLine($"   📍 {e.OldResult.X},{e.OldResult.Y} → {e.NewResult.X},{e.NewResult.Y}");
            Console.WriteLine($"   📏 Расстояние: {e.Distance:F1}px");
            Console.ResetColor();
        };

        session.Start();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"🔍 Отслеживание активно на {duration} секунд...");
        Console.WriteLine("💡 Попробуйте:");
        Console.WriteLine("   • Открыть/закрыть блокнот");
        Console.WriteLine("   • Двигать окно блокнота");
        Console.WriteLine("   • Свернуть/развернуть окно");
        Console.ResetColor();
        Console.WriteLine();

        for (int i = 0; i < duration; i++)
        {
            await Task.Delay(1000);
            if (i % 10 == 0 && i > 0)
            {
                Console.WriteLine($"⏱️  {i}с... Появлений: {stats.AppearCount}, Перемещений: {stats.MoveCount}, Исчезновений: {stats.DisappearCount}");
            }
        }

        session.Stop();
        ImageSearchConfiguration.EnableDebugOverlay = false;

        Console.WriteLine("\n" + new string('═', 60));
        Console.WriteLine("📊 СТАТИСТИКА:");
        Console.WriteLine(new string('═', 60));
        Console.WriteLine($"✅ Появлений: {stats.AppearCount}");
        Console.WriteLine($"🔄 Перемещений: {stats.MoveCount}");
        Console.WriteLine($"⚠️  Исчезновений: {stats.DisappearCount}");

        templates[0].Image.Dispose();
    }

    /// <summary>
    /// Сценарий 4: Мультишаблонный поиск (Search.ForAny)
    /// Ищет любую из нескольких кнопок (разные состояния, локали, темы)
    /// </summary>
    public static async Task Scenario4_MultiTemplate()
    {
        PrintHeader("СЦЕНАРИЙ 4: Мультишаблонный поиск (Search.ForAny)");
        Console.WriteLine("Поиск ЛЮБОЙ из нескольких кнопок одновременно.");
        Console.WriteLine("Полезно для: разных состояний кнопок, локалей, тем.\n");

        var templates = LoadTemplates();
        if (templates.Length < 2)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️  Для этого сценария нужно минимум 2 изображения в MyImages/");
            Console.WriteLine("   Добавьте еще одну кнопку (например, другую кнопку из блокнота)");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"📋 Загружено шаблонов: {templates.Length}");
        for (int i = 0; i < templates.Length; i++)
        {
            Console.WriteLine($"   {i + 1}. {templates[i].Name} ({templates[i].Image.Width}x{templates[i].Image.Height})");
        }

        Console.Write("\n⚙️  Confidence (0.7-0.95, по умолчанию 0.85): ");
        var confInput = Console.ReadLine();
        var confidence = double.TryParse(confInput, out var c) ? c : 0.85;

        Console.Write("   Время поиска в секундах (10-60, по умолчанию 30): ");
        var durInput = Console.ReadLine();
        var duration = int.TryParse(durInput, out var d) ? d : 30;

        // Enable overlay with different colors for different templates
        ImageSearchConfiguration.EnableDebugOverlay = true;
        ImageSearchConfiguration.DebugOverlayColor = Color.Magenta;
        ImageSearchConfiguration.DebugOverlayThickness = 4;

        Console.WriteLine("\n▶️  Запуск мультишаблонного поиска...\n");

        using var capture = ScreenCaptureAdapter.FromScreen(0, targetFps: 30);
        using var session = Search.ForAny(templates.Select(t => t.Image).ToArray())
            .WithConfidence(confidence)
            .WithMovementThreshold(5.0)
            .In(capture);

        var templateStats = new int[templates.Length];

        session.Appeared += (s, result) =>
        {
            if (result is MultiFindResult multiResult)
            {
                templateStats[multiResult.MatchedTemplateIndex]++;
                var templateName = templates[multiResult.MatchedTemplateIndex].Name;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✅ [{DateTime.Now:HH:mm:ss}] Найден шаблон #{multiResult.MatchedTemplateIndex + 1}: {templateName}");
                Console.WriteLine($"   📍 Позиция: ({result.X}, {result.Y})");
                Console.WriteLine($"   🎯 Confidence: {result.Confidence:P1}");
                Console.WriteLine($"   📐 Размер: {multiResult.MatchedTemplate.Width}x{multiResult.MatchedTemplate.Height}");
                Console.ResetColor();
            }
        };

        session.Moved += (s, e) =>
        {
            if (e.NewResult is MultiFindResult multiResult)
            {
                var templateName = templates[multiResult.MatchedTemplateIndex].Name;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"🔄 [{DateTime.Now:HH:mm:ss}] Шаблон #{multiResult.MatchedTemplateIndex + 1} ({templateName}) переместился");
                Console.ResetColor();
            }
        };

        session.Disappeared += (s, result) =>
        {
            if (result is MultiFindResult multiResult)
            {
                var templateName = templates[multiResult.MatchedTemplateIndex].Name;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n⚠️  [{DateTime.Now:HH:mm:ss}] Шаблон #{multiResult.MatchedTemplateIndex + 1} ({templateName}) исчез");
                Console.ResetColor();
            }
        };

        session.Start();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"🔍 Поиск {templates.Length} шаблонов активен на {duration} секунд...");
        Console.WriteLine("💡 Попробуйте открыть блокноты с разными кнопками!");
        Console.ResetColor();
        Console.WriteLine();

        for (int i = 0; i < duration; i++)
        {
            await Task.Delay(1000);
        }

        session.Stop();
        ImageSearchConfiguration.EnableDebugOverlay = false;

        Console.WriteLine("\n" + new string('═', 60));
        Console.WriteLine("📊 СТАТИСТИКА ПО ШАБЛОНАМ:");
        Console.WriteLine(new string('═', 60));
        for (int i = 0; i < templates.Length; i++)
        {
            Console.WriteLine($"Шаблон #{i + 1} ({templates[i].Name}): {templateStats[i]} обнаружений");
        }

        foreach (var template in templates)
        {
            template.Image.Dispose();
        }
    }

    /// <summary>
    /// Сценарий 5: Захват окна с координатами (Window Capture)
    /// Демонстрирует правильную работу координат при захвате окна
    /// </summary>
    public static async Task Scenario5_WindowCapture()
    {
        PrintHeader("СЦЕНАРИЙ 5: Захват окна с DebugOverlay");
        Console.WriteLine("Захват конкретного окна Notepad с правильными координатами overlay.\n");

        var templates = LoadTemplates();
        if (templates.Length == 0) return;

        Console.WriteLine("🔍 Поиск окон Notepad...");
        var notepadWindows = WindowFinder.FindAllNotepadWindows();

        if (notepadWindows.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Notepad не найден! Запустите Notepad и попробуйте снова.");
            Console.ResetColor();
            templates[0].Image.Dispose();
            return;
        }

        IntPtr windowHandle;
        if (notepadWindows.Count == 1)
        {
            windowHandle = notepadWindows[0].Handle;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ Найдено окно: \"{notepadWindows[0].Title}\"");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine($"\n📋 Найдено {notepadWindows.Count} окон Notepad:");
            for (int i = 0; i < notepadWindows.Count; i++)
            {
                Console.WriteLine($"   {i + 1}. {notepadWindows[i].Title}");
            }
            Console.Write($"\nВыберите окно (1-{notepadWindows.Count}): ");
            var choiceStr = Console.ReadLine();
            if (!int.TryParse(choiceStr, out int choice) || choice < 1 || choice > notepadWindows.Count)
            {
                choice = 1;
            }
            windowHandle = notepadWindows[choice - 1].Handle;
            Console.WriteLine($"✅ Выбрано: \"{notepadWindows[choice - 1].Title}\"");
        }

        Console.Write("\n⏱️  Время отслеживания (10-60 сек, по умолчанию 20): ");
        var durInput = Console.ReadLine();
        var duration = int.TryParse(durInput, out var d) ? d : 20;

        // Enable overlay with window handle for coordinate conversion
        ImageSearchConfiguration.EnableDebugOverlay = true;
        ImageSearchConfiguration.DebugOverlayColor = Color.Red;
        ImageSearchConfiguration.DebugOverlayThickness = 4;
        ImageSearchConfiguration.DebugOverlayWindowHandle = windowHandle;

        Console.WriteLine("\n▶️  Запуск захвата окна...\n");

        using var capture = ScreenCaptureAdapter.FromWindow(windowHandle, targetFps: 30);
        using var session = Search.For(templates[0].Image)
            .WithConfidence(0.85)
            .WithMovementThreshold(3.0)
            .In(capture);

        session.Appeared += (s, result) =>
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n✅ [{DateTime.Now:HH:mm:ss}] Кнопка найдена в окне!");
            Console.WriteLine($"   📍 Координаты ОТНОСИТЕЛЬНО окна: ({result.X}, {result.Y})");
            Console.WriteLine($"   🎯 Confidence: {result.Confidence:P1}");
            Console.WriteLine($"   💡 Overlay показывает ТОЧНОЕ положение на экране!");
            Console.ResetColor();
        };

        session.Moved += (s, e) =>
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"🔄 [{DateTime.Now:HH:mm:ss}] Окно/кнопка переместились");
            Console.ResetColor();
        };

        session.Start();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"🔍 Отслеживание окна на {duration} секунд...");
        Console.WriteLine("💡 Попробуйте:");
        Console.WriteLine("   • Двигать окно Notepad - overlay двигается вместе с ним!");
        Console.WriteLine("   • Координаты overlay ВСЕГДА точные благодаря DWM API");
        Console.ResetColor();
        Console.WriteLine();

        await Task.Delay(duration * 1000);

        session.Stop();
        ImageSearchConfiguration.EnableDebugOverlay = false;

        templates[0].Image.Dispose();
    }

    /// <summary>
    /// Сценарий 6: WaitUntil методы
    /// Демонстрирует синхронное ожидание появления/исчезновения
    /// </summary>
    public static async Task Scenario6_WaitUntil()
    {
        await Task.CompletedTask; // Satisfy async requirement

        PrintHeader("СЦЕНАРИЙ 6: Синхронное ожидание (WaitUntilVisible)");
        Console.WriteLine("Демонстрация блокирующих методов ожидания.\n");

        var templates = LoadTemplates();
        if (templates.Length == 0) return;

        Console.WriteLine("Этот сценарий будет:");
        Console.WriteLine("   1. Ждать пока кнопка ПОЯВИТСЯ на экране");
        Console.WriteLine("   2. Ждать пока кнопка ИСЧЕЗНЕТ с экрана\n");

        Console.Write("Начать? (y/n): ");
        if (Console.ReadLine()?.ToLower() != "y") return;

        using var capture = ScreenCaptureAdapter.FromScreen(0, targetFps: 30);
        using var session = Search.For(templates[0].Image)
            .WithConfidence(0.85)
            .In(capture);

        session.Start();

        Console.WriteLine("\n⏳ Ожидание появления кнопки...");
        Console.WriteLine("   (Откройте блокнот чтобы кнопка появилась)\n");

        var result = session.WaitUntilVisible(TimeSpan.FromSeconds(30));

        if (result != null)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ Кнопка появилась! Позиция: ({result.X}, {result.Y})");
            Console.ResetColor();

            Console.WriteLine("\n⏳ Теперь ожидание исчезновения...");
            Console.WriteLine("   (Закройте блокнот чтобы кнопка исчезла)\n");

            var disappeared = session.WaitUntilNotVisible(TimeSpan.FromSeconds(30));

            if (disappeared)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ Кнопка исчезла!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  Таймаут - кнопка всё еще видна");
                Console.ResetColor();
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️  Таймаут - кнопка не появилась за 30 секунд");
            Console.ResetColor();
        }

        session.Stop();
        templates[0].Image.Dispose();
    }

    // Helper methods

    private static void PrintHeader(string title)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔" + new string('═', 70) + "╗");
        Console.WriteLine($"║ {title.PadRight(68)} ║");
        Console.WriteLine("╚" + new string('═', 70) + "╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    public static (string Name, ReferenceImage Image)[] LoadTemplates()
    {
        string? myImagesPath = FindMyImagesFolder();

        if (myImagesPath == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Папка MyImages/ не найдена!");
            Console.ResetColor();
            Console.WriteLine("\n📝 Инструкция:");
            Console.WriteLine("   1. Создайте папку Demo/MyImages/");
            Console.WriteLine("   2. Добавьте туда PNG файлы кнопок из блокнота");
            Console.WriteLine("   3. Запустите demo снова\n");
            Console.WriteLine("Нажмите любую клавишу...");
            Console.ReadKey();
            return Array.Empty<(string, ReferenceImage)>();
        }

        var imageFiles = Directory.GetFiles(myImagesPath, "*.png");

        if (imageFiles.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️  В папке MyImages/ нет PNG файлов!");
            Console.ResetColor();
            Console.WriteLine($"📂 Путь: {Path.GetFullPath(myImagesPath)}\n");
            Console.WriteLine("Нажмите любую клавишу...");
            Console.ReadKey();
            return Array.Empty<(string, ReferenceImage)>();
        }

        var templates = new List<(string Name, ReferenceImage Image)>();

        foreach (var imagePath in imageFiles)
        {
            try
            {
                var refImage = ReferenceImage.FromFile(imagePath);
                templates.Add((Path.GetFileName(imagePath), refImage));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠️  Не удалось загрузить {Path.GetFileName(imagePath)}: {ex.Message}");
                Console.ResetColor();
            }
        }

        return templates.ToArray();
    }

    private static string? FindMyImagesFolder()
    {
        var possiblePaths = new[]
        {
            "MyImages",
            Path.Combine("..", "MyImages"),
            Path.Combine("..", "..", "MyImages"),
            Path.Combine("Demo", "MyImages"),
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

    private class TrackingStats
    {
        public int AppearCount;
        public int DisappearCount;
        public int MoveCount;
    }
}
