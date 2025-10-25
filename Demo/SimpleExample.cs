using System.Drawing;
using ImageSearchCL.API;

namespace ImageSearchCL.Demo;

/// <summary>
/// Максимально простые примеры использования библиотеки
/// </summary>
public static class SimpleExample
{
    /// <summary>
    /// Пример 1: Самый простой код с дефолтными настройками
    /// </summary>
    public static async Task BasicTracking()
    {
        Console.WriteLine("=== БАЗОВЫЙ ПРИМЕР ===\n");
        Console.WriteLine("Код:");
        Console.WriteLine(@"
var session = Search.For(""button.png"").In(capture);
session.Appeared += (s, r) => Console.WriteLine(""Найдено!"");
session.Start();
");
        Console.WriteLine("\nИспользует дефолты:");
        Console.WriteLine($"  Confidence: {ImageSearchConfiguration.DefaultConfidence}");
        Console.WriteLine($"  MovementThreshold: {ImageSearchConfiguration.DefaultMovementThreshold}px\n");

        var templates = DemoScenarios.LoadTemplates();
        if (templates.Length == 0) return;

        using var capture = ScreenCaptureAdapter.FromScreen(0, targetFps: 30);
        using var session = Search.For(templates[0].Image).In(capture);

        session.Appeared += (s, r) => Console.WriteLine($"✅ Найдено! Позиция: ({r.X}, {r.Y})");

        session.Start();
        await Task.Delay(10000);
        session.Stop();

        templates[0].Image.Dispose();
    }

    /// <summary>
    /// Пример 2: С кастомными настройками
    /// </summary>
    public static async Task CustomSettings()
    {
        Console.WriteLine("\n=== С КАСТОМНЫМИ НАСТРОЙКАМИ ===\n");
        Console.WriteLine("Код:");
        Console.WriteLine(@"
var session = Search.For(""button.png"")
    .WithConfidence(0.9)
    .WithMovementThreshold(10.0)
    .In(capture);
");

        var templates = DemoScenarios.LoadTemplates();
        if (templates.Length == 0) return;

        using var capture = ScreenCaptureAdapter.FromScreen(0, targetFps: 30);
        using var session = Search.For(templates[0].Image)
            .WithConfidence(0.9)
            .WithMovementThreshold(10.0)
            .In(capture);

        session.Appeared += (s, r) => Console.WriteLine($"✅ Найдено с высокой точностью: {r.Confidence:P}");

        session.Start();
        await Task.Delay(10000);
        session.Stop();

        templates[0].Image.Dispose();
    }

    /// <summary>
    /// Пример 3: Глобальные дефолты
    /// </summary>
    public static async Task GlobalDefaults()
    {
        Console.WriteLine("\n=== ГЛОБАЛЬНЫЕ ДЕФОЛТЫ ===\n");
        Console.WriteLine("Код:");
        Console.WriteLine(@"
// В начале программы
ImageSearchConfiguration.DefaultConfidence = 0.85;
ImageSearchConfiguration.DefaultMovementThreshold = 3.0;

// Все сессии используют эти дефолты
var session1 = Search.For(""btn1.png"").In(capture);
var session2 = Search.For(""btn2.png"").In(capture);
");

        Console.WriteLine("\nУстанавливаю глобальные дефолты...");
        ImageSearchConfiguration.DefaultConfidence = 0.85;
        ImageSearchConfiguration.DefaultMovementThreshold = 3.0;

        Console.WriteLine($"  DefaultConfidence: {ImageSearchConfiguration.DefaultConfidence}");
        Console.WriteLine($"  DefaultMovementThreshold: {ImageSearchConfiguration.DefaultMovementThreshold}px\n");

        var templates = DemoScenarios.LoadTemplates();
        if (templates.Length == 0) return;

        using var capture = ScreenCaptureAdapter.FromScreen(0, targetFps: 30);
        using var session = Search.For(templates[0].Image).In(capture);

        session.Appeared += (s, r) => Console.WriteLine($"✅ Найдено (дефолты: conf={r.Confidence:P})");

        session.Start();
        await Task.Delay(10000);
        session.Stop();

        // Reset defaults
        ImageSearchConfiguration.Reset();
        templates[0].Image.Dispose();
    }

    /// <summary>
    /// Пример 4: Find и FindAll
    /// </summary>
    public static async Task FindAndFindAll()
    {
        Console.WriteLine("\n=== FIND И FINDALL ===\n");
        Console.WriteLine("Код:");
        Console.WriteLine(@"
// Захватить фрейм
var frame = CaptureFrame();

// Найти одну кнопку
var result = Search.Find(""button.png"").In(frame);

// Найти все кнопки
var results = Search.FindAll(""button.png"")
    .WithConfidence(0.8)
    .WithOverlapThreshold(0.5)
    .In(frame);
");

        var templates = DemoScenarios.LoadTemplates();
        if (templates.Length == 0) return;

        using var capture = ScreenCaptureAdapter.FromScreen(0, targetFps: 1);
        capture.Start();
        await Task.Delay(500);

        // Capture one frame manually
        Bitmap? frame = null;
        var frameEvent = new ManualResetEventSlim(false);
        capture.FrameReady += (s, e) =>
        {
            if (frame == null && e.Frame != null)
            {
                frame = (Bitmap)e.Frame.Clone();
                frameEvent.Set();
            }
        };

        frameEvent.Wait(TimeSpan.FromSeconds(2));
        capture.Stop();

        if (frame != null)
        {
            Console.WriteLine("\nВыполняю Find...");
            using var findBuilder = Search.Find(templates[0].Image);
            var result = findBuilder.In(frame);

            if (result != null)
            {
                Console.WriteLine($"✅ Find: Найдено на ({result.X}, {result.Y}), Confidence: {result.Confidence:P}");
            }
            else
            {
                Console.WriteLine("⚠️  Find: Не найдено");
            }

            Console.WriteLine("\nВыполняю FindAll...");
            using var findAllBuilder = Search.FindAll(templates[0].Image);
            var results = findAllBuilder.WithConfidence(0.8).In(frame);

            Console.WriteLine($"✅ FindAll: Найдено {results.Count} объектов");
            foreach (var r in results.Take(3))
            {
                Console.WriteLine($"   - ({r.X}, {r.Y}), Confidence: {r.Confidence:P}");
            }

            frame.Dispose();
        }

        templates[0].Image.Dispose();
    }

    /// <summary>
    /// Пример 5: ForAny с путями напрямую
    /// </summary>
    public static async Task ForAnySimple()
    {
        Console.WriteLine("\n=== FORANY БЕЗ REFERENCEIMAGE ===\n");
        Console.WriteLine("Код:");
        Console.WriteLine(@"
// Раньше
var ref1 = ReferenceImage.FromFile(""btn1.png"");
var ref2 = ReferenceImage.FromFile(""btn2.png"");
var session = Search.ForAny(ref1, ref2).In(capture);
ref1.Dispose(); ref2.Dispose();

// Теперь
var session = Search.ForAny(""btn1.png"", ""btn2.png"").In(capture);
");

        var templates = DemoScenarios.LoadTemplates();
        if (templates.Length < 2)
        {
            Console.WriteLine("\n⚠️  Нужно минимум 2 изображения в MyImages/");
            if (templates.Length > 0) templates[0].Image.Dispose();
            return;
        }

        using var capture = ScreenCaptureAdapter.FromScreen(0, targetFps: 30);

        // Old way - with ReferenceImage
        using var session = Search.ForAny(templates[0].Image, templates[1].Image).In(capture);

        session.Appeared += (s, r) =>
        {
            if (r is MultiFindResult multi)
            {
                Console.WriteLine($"✅ Найден шаблон #{multi.MatchedTemplateIndex}: ({r.X}, {r.Y})");
            }
        };

        session.Start();
        await Task.Delay(10000);
        session.Stop();

        foreach (var t in templates)
        {
            t.Image.Dispose();
        }
    }
}
