using System;
using System.Collections.Generic;
using System.Linq;

namespace OopMonitoringLab
{
    // ============================
    // 1. БАЗОВЫЕ МОДЕЛИ
    // ============================

    public class Request
    {
        public string ServiceName { get; set; } = "";
        public int PayloadSize { get; set; }
        public int? DeadlineMs { get; set; }

        public Request(string serviceName, int payloadSize, int? deadlineMs = null)
        {
            ServiceName = serviceName;
            PayloadSize = payloadSize;
            DeadlineMs = deadlineMs;
        }
    }

    public class Response
    {
        public bool IsSuccess { get; set; }
        public int LatencyMs { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }

        public Response(bool isSuccess, int latencyMs, string errorCode = null,
                       string errorMessage = null)
        {
            IsSuccess = isSuccess;
            LatencyMs = latencyMs;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }

    // ============================
    // 2. СИСТЕМА КОНФИГУРАЦИЙ
    // ============================

    public class ServiceConfig
    {
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int BaseLatencyMs { get; set; }
        public double FailureProbability { get; set; }
        public int LatencyVariation { get; set; } // Разброс задержки

        public ServiceConfig() { }

        public ServiceConfig(string name, string displayName, int baseLatency,
                           double failureProb, int latencyVariation = 50)
        {
            Name = name;
            DisplayName = displayName;
            BaseLatencyMs = baseLatency;
            FailureProbability = failureProb;
            LatencyVariation = latencyVariation;
        }

        public ServiceConfig Clone()
        {
            return new ServiceConfig
            {
                Name = this.Name,
                DisplayName = this.DisplayName,
                BaseLatencyMs = this.BaseLatencyMs,
                FailureProbability = this.FailureProbability,
                LatencyVariation = this.LatencyVariation
            };
        }
    }

    public class ScenarioConfig
    {
        public string ScenarioName { get; set; } = "";
        public string Description { get; set; } = "";
        public int TotalRequests { get; set; } = 100;
        public double FastServiceRatio { get; set; } = 0.5; // 50% запросов к FastService
        public List<ServiceConfig> ServiceConfigs { get; set; } = new List<ServiceConfig>();

        public ScenarioConfig() { }

        public ScenarioConfig(string name, string description, int requests,
                            double fastRatio, List<ServiceConfig> configs)
        {
            ScenarioName = name;
            Description = description;
            TotalRequests = requests;
            FastServiceRatio = fastRatio;
            ServiceConfigs = configs;
        }
    }

    // ============================
    // 3. ИНТЕРФЕЙС И БАЗОВЫЙ КЛАСС СЕРВИСА
    // ============================

    public interface IService
    {
        string Name { get; }
        string DisplayName { get; }
        ServiceConfig Config { get; }
        Response Process(Request request);
    }

    public abstract class ServiceBase : IService
    {
        public string Name { get; protected set; } = "";
        public string DisplayName { get; protected set; } = "";
        public ServiceConfig Config { get; protected set; }
        protected Random _random = new Random();

        protected ServiceBase(ServiceConfig config)
        {
            Config = config;
            Name = config.Name;
            DisplayName = config.DisplayName;
        }

        public virtual Response Process(Request request)
        {
            // Базовая реализация имитации задержки
            int latency = Config.BaseLatencyMs + _random.Next(0, Config.LatencyVariation);
            bool isSuccess = _random.NextDouble() >= Config.FailureProbability;

            var response = new Response(
                isSuccess: isSuccess,
                latencyMs: latency,
                errorCode: isSuccess ? null : $"{Name.ToUpper()}_ERROR",
                errorMessage: isSuccess ? null : $"Ошибка в сервисе {DisplayName}"
            );

            return response;
        }
    }

    public class FastService : ServiceBase
    {
        public FastService(ServiceConfig config) : base(config) { }

        public override Response Process(Request request)
        {
            var response = base.Process(request);
            Log(request, response);
            return response;
        }

        private void Log(Request request, Response response)
        {
            string status = response.IsSuccess ? "УСПЕХ" : "ОШИБКА";
            string color = response.IsSuccess ? "✓" : "✗";
            Console.WriteLine($"{color} [Fast] {DisplayName}: {request.PayloadSize} байт → {status} ({response.LatencyMs}мс)");
        }
    }

    public class SlowService : ServiceBase
    {
        public SlowService(ServiceConfig config) : base(config) { }

        public override Response Process(Request request)
        {
            var response = base.Process(request);
            Log(request, response);
            return response;
        }

        private void Log(Request request, Response response)
        {
            string status = response.IsSuccess ? "УСПЕХ" : "ОШИБКА";
            string color = response.IsSuccess ? "✓" : "✗";
            Console.WriteLine($"{color} [Slow] {DisplayName}: {request.PayloadSize} байт → {status} ({response.LatencyMs}мс)");
        }
    }

    // ============================
    // 4. МЕТРИКИ И ОЦЕНКА СОСТОЯНИЯ
    // ============================

    public class ServiceMetrics
    {
        public string ServiceName { get; }
        public string DisplayName { get; }
        public int TotalRequests { get; private set; }
        public int SuccessfulRequests { get; private set; }
        public int FailedRequests { get; private set; }
        public double AverageLatencyMs { get; private set; }
        public int MaxLatencyMs { get; private set; }
        public int MinLatencyMs { get; private set; }

        public ServiceMetrics(string serviceName, string displayName)
        {
            ServiceName = serviceName;
            DisplayName = displayName;
            MinLatencyMs = int.MaxValue;
        }

        public double ErrorRate => TotalRequests == 0 ? 0.0 : (double)FailedRequests / TotalRequests;
        public double SuccessRate => TotalRequests == 0 ? 0.0 : (double)SuccessfulRequests / TotalRequests;

        public void Update(Response response)
        {
            TotalRequests++;

            if (response.IsSuccess)
                SuccessfulRequests++;
            else
                FailedRequests++;

            // Обновляем среднюю задержку
            if (TotalRequests == 1)
                AverageLatencyMs = response.LatencyMs;
            else
                AverageLatencyMs = (AverageLatencyMs * (TotalRequests - 1) + response.LatencyMs) / TotalRequests;

            // Обновляем мин/макс задержки
            if (response.LatencyMs > MaxLatencyMs)
                MaxLatencyMs = response.LatencyMs;

            if (response.LatencyMs < MinLatencyMs)
                MinLatencyMs = response.LatencyMs;
        }

        public void Reset()
        {
            TotalRequests = 0;
            SuccessfulRequests = 0;
            FailedRequests = 0;
            AverageLatencyMs = 0;
            MaxLatencyMs = 0;
            MinLatencyMs = int.MaxValue;
        }
    }

    public enum ServiceHealth
    {
        Healthy,      // Здоров
        Degraded,     // Деградация
        Unhealthy     // Нездоров
    }

    public class HealthEvaluator
    {
        public double MaxHealthyErrorRate { get; set; } = 0.05;    // 5%
        public double MaxDegradedErrorRate { get; set; } = 0.20;   // 20%
        public int MaxHealthyLatencyMs { get; set; } = 150;        // 150мс
        public int MaxDegradedLatencyMs { get; set; } = 400;       // 400мс

        public ServiceHealth Evaluate(ServiceMetrics metrics)
        {
            if (metrics.TotalRequests == 0)
                return ServiceHealth.Healthy;

            // Проверяем уровень ошибок
            if (metrics.ErrorRate > MaxDegradedErrorRate)
                return ServiceHealth.Unhealthy;

            if (metrics.ErrorRate > MaxHealthyErrorRate)
                return ServiceHealth.Degraded;

            // Проверяем задержку
            if (metrics.AverageLatencyMs > MaxDegradedLatencyMs)
                return ServiceHealth.Unhealthy;

            if (metrics.AverageLatencyMs > MaxHealthyLatencyMs)
                return ServiceHealth.Degraded;

            return ServiceHealth.Healthy;
        }

        public string GetHealthIcon(ServiceHealth health)
        {
            return health switch
            {
                ServiceHealth.Healthy => "✅",
                ServiceHealth.Degraded => "⚠️",
                ServiceHealth.Unhealthy => "❌",
                _ => "❓"
            };
        }

        public string GetHealthText(ServiceHealth health)
        {
            return health switch
            {
                ServiceHealth.Healthy => "ЗДОРОВ",
                ServiceHealth.Degraded => "ДЕГРАДАЦИЯ",
                ServiceHealth.Unhealthy => "НЕЗДОРОВ",
                _ => "НЕИЗВЕСТНО"
            };
        }
    }

    // ============================
    // 5. РЕЗУЛЬТАТЫ СЦЕНАРИЯ И СРАВНЕНИЕ
    // ============================

    public class ScenarioResult
    {
        public string ScenarioName { get; set; } = "";
        public DateTime ExecutionTime { get; set; }
        public Dictionary<string, ServiceMetrics> ServiceResults { get; set; } = new Dictionary<string, ServiceMetrics>();
        public Dictionary<string, ServiceHealth> ServiceHealth { get; set; } = new Dictionary<string, ServiceHealth>();
        public TimeSpan ExecutionDuration { get; set; }

        public void PrintSummary(HealthEvaluator evaluator)
        {
            Console.WriteLine($"\n📊 РЕЗУЛЬТАТЫ СЦЕНАРИЯ: {ScenarioName}");
            Console.WriteLine(new string('─', 60));

            foreach (var serviceName in ServiceResults.Keys)
            {
                var metrics = ServiceResults[serviceName];
                var health = ServiceHealth[serviceName];
                var icon = evaluator.GetHealthIcon(health);
                var healthText = evaluator.GetHealthText(health);

                Console.WriteLine($"\n{icon} {metrics.DisplayName}:");
                Console.WriteLine($"   Всего запросов: {metrics.TotalRequests}");
                Console.WriteLine($"   Успешных: {metrics.SuccessfulRequests} ({metrics.SuccessRate:P1})");
                Console.WriteLine($"   Ошибок: {metrics.FailedRequests} ({metrics.ErrorRate:P1})");
                Console.WriteLine($"   Задержка: {metrics.AverageLatencyMs:F0}мс " +
                                $"(мин: {metrics.MinLatencyMs}мс, макс: {metrics.MaxLatencyMs}мс)");
                Console.WriteLine($"   Состояние: {healthText}");
            }

            Console.WriteLine($"\n⏱ Время выполнения: {ExecutionDuration.TotalSeconds:F2} сек");
            Console.WriteLine(new string('─', 60));
        }
    }

    public class ScenarioComparison
    {
        public List<ScenarioResult> Results { get; set; } = new List<ScenarioResult>();
        public HealthEvaluator Evaluator { get; set; } = new HealthEvaluator();

        public void AddResult(ScenarioResult result)
        {
            Results.Add(result);
        }

        public void PrintComparison()
        {
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("📈 СРАВНИТЕЛЬНЫЙ АНАЛИЗ ВСЕХ СЦЕНАРИЕВ");
            Console.WriteLine(new string('=', 70));

            if (Results.Count == 0)
            {
                Console.WriteLine("Нет данных для сравнения.");
                return;
            }

            // Заголовок таблицы
            Console.WriteLine("\n" + new string('─', 90));
            Console.WriteLine("| Сценарий           | Сервис         | Задержка | Ошибки   | Состояние     |");
            Console.WriteLine(new string('─', 90));

            foreach (var result in Results.OrderBy(r => r.ScenarioName))
            {
                bool firstService = true;

                foreach (var serviceName in result.ServiceResults.Keys.OrderBy(k => k))
                {
                    var metrics = result.ServiceResults[serviceName];
                    var health = result.ServiceHealth[serviceName];
                    var icon = Evaluator.GetHealthIcon(health);
                    var healthText = Evaluator.GetHealthText(health);

                    string scenarioName = firstService ? result.ScenarioName : "";
                    string latency = $"{metrics.AverageLatencyMs:F0}мс";
                    string errorRate = $"{metrics.ErrorRate:P1}";

                    Console.WriteLine($"| {scenarioName,-18} | {metrics.DisplayName,-14} | {latency,-8} | {errorRate,-8} | {icon} {healthText,-10} |");

                    firstService = false;
                }

                Console.WriteLine(new string('─', 90));
            }

            // Вывод рекомендаций
            PrintRecommendations();
        }

        private void PrintRecommendations()
        {
            Console.WriteLine("\n💡 РЕКОМЕНДАЦИИ НА ОСНОВЕ АНАЛИЗА:");
            Console.WriteLine(new string('─', 60));

            var fastServiceResults = Results
                .SelectMany(r => r.ServiceResults
                    .Where(s => s.Key.Contains("Fast"))
                    .Select(s => new { Scenario = r.ScenarioName, Metrics = s.Value, Health = r.ServiceHealth[s.Key] }))
                .ToList();

            var slowServiceResults = Results
                .SelectMany(r => r.ServiceResults
                    .Where(s => s.Key.Contains("Slow"))
                    .Select(s => new { Scenario = r.ScenarioName, Metrics = s.Value, Health = r.ServiceHealth[s.Key] }))
                .ToList();

            // Анализ FastService
            if (fastServiceResults.Any())
            {
                var bestFast = fastServiceResults
                    .Where(r => r.Health == ServiceHealth.Healthy)
                    .OrderBy(r => r.Metrics.AverageLatencyMs)
                    .ThenBy(r => r.Metrics.ErrorRate)
                    .FirstOrDefault();

                var worstFast = fastServiceResults
                    .OrderByDescending(r => r.Metrics.ErrorRate)
                    .ThenByDescending(r => r.Metrics.AverageLatencyMs)
                    .FirstOrDefault();

                if (bestFast != null)
                    Console.WriteLine($"• FastService лучше всего работает в сценарии '{bestFast.Scenario}'");

                if (worstFast != null && worstFast.Health == ServiceHealth.Unhealthy)
                    Console.WriteLine($"• FastService становится нездоровым при задержках > {worstFast.Metrics.AverageLatencyMs:F0}мс или ошибках > {worstFast.Metrics.ErrorRate:P1}");
            }

            // Анализ SlowService
            if (slowServiceResults.Any())
            {
                var worstSlow = slowServiceResults
                    .OrderByDescending(r => r.Metrics.ErrorRate)
                    .ThenByDescending(r => r.Metrics.AverageLatencyMs)
                    .FirstOrDefault();

                var healthySlow = slowServiceResults
                    .Where(r => r.Health == ServiceHealth.Healthy)
                    .OrderByDescending(r => r.Metrics.AverageLatencyMs)
                    .FirstOrDefault();

                if (healthySlow != null && worstSlow != null)
                {
                    double latencyThreshold = (healthySlow.Metrics.AverageLatencyMs + worstSlow.Metrics.AverageLatencyMs) / 2;
                    Console.WriteLine($"• SlowService критическая задержка: ~{latencyThreshold:F0}мс");
                }
            }

            // Общие выводы
            var allHealthy = Results.Where(r => r.ServiceHealth.All(h => h.Value == ServiceHealth.Healthy));
            var anyUnhealthy = Results.Where(r => r.ServiceHealth.Any(h => h.Value == ServiceHealth.Unhealthy));

            if (allHealthy.Any())
                Console.WriteLine($"• Стабильные сценарии: {string.Join(", ", allHealthy.Select(r => r.ScenarioName))}");

            if (anyUnhealthy.Any())
                Console.WriteLine($"• Рисковые сценарии: {string.Join(", ", anyUnhealthy.Select(r => r.ScenarioName))}");
        }
    }

    // ============================
    // 6. СИМУЛЯТОР И МЕНЕДЖЕР СЦЕНАРИЕВ
    // ============================

    public class SimulationManager
    {
        private Random _random = new Random();
        private HealthEvaluator _evaluator = new HealthEvaluator();

        public ScenarioResult RunScenario(ScenarioConfig config)
        {
            Console.WriteLine($"\n🚀 ЗАПУСК СЦЕНАРИЯ: {config.ScenarioName}");
            Console.WriteLine($"📝 {config.Description}");
            Console.WriteLine(new string('=', 60));

            var startTime = DateTime.Now;

            // Создаем сервисы на основе конфигурации
            var services = new Dictionary<string, IService>();
            var metrics = new Dictionary<string, ServiceMetrics>();

            foreach (var serviceConfig in config.ServiceConfigs)
            {
                IService service;
                if (serviceConfig.Name.Contains("Fast"))
                    service = new FastService(serviceConfig);
                else
                    service = new SlowService(serviceConfig);

                services[service.Name] = service;
                metrics[service.Name] = new ServiceMetrics(service.Name, service.DisplayName);

                Console.WriteLine($"📋 {service.DisplayName}: задержка {serviceConfig.BaseLatencyMs}мс, ошибки {serviceConfig.FailureProbability:P0}");
            }

            Console.WriteLine($"\n📊 Обрабатываю {config.TotalRequests} запросов...\n");

            // Генерируем и обрабатываем запросы
            for (int i = 0; i < config.TotalRequests; i++)
            {
                // Определяем, к какому сервису отправить запрос
                string serviceName;
                if (_random.NextDouble() < config.FastServiceRatio)
                    serviceName = services.Keys.First(k => k.Contains("Fast"));
                else
                    serviceName = services.Keys.First(k => k.Contains("Slow"));

                // Создаем запрос
                var request = new Request(
                    serviceName: serviceName,
                    payloadSize: _random.Next(50, 1000),
                    deadlineMs: _random.Next(100, 1000)
                );

                // Обрабатываем запрос
                var service = services[serviceName];
                var response = service.Process(request);

                // Обновляем метрики
                metrics[serviceName].Update(response);

                // Прогресс-бар каждые 10%
                if (config.TotalRequests >= 10 && (i + 1) % (config.TotalRequests / 10) == 0)
                {
                    int percent = (i + 1) * 100 / config.TotalRequests;
                    Console.Write($"[{new string('█', percent / 10)}{new string('░', 10 - percent / 10)}] {percent}%\r");
                }
            }

            Console.WriteLine(); // Завершаем строку прогресса

            // Оцениваем состояние сервисов
            var healthStatus = new Dictionary<string, ServiceHealth>();
            foreach (var metric in metrics)
            {
                healthStatus[metric.Key] = _evaluator.Evaluate(metric.Value);
            }

            var endTime = DateTime.Now;
            var duration = endTime - startTime;

            // Создаем результат
            var result = new ScenarioResult
            {
                ScenarioName = config.ScenarioName,
                ExecutionTime = startTime,
                ServiceResults = metrics,
                ServiceHealth = healthStatus,
                ExecutionDuration = duration
            };

            return result;
        }
    }

    // ============================
    // 7. ГЛАВНАЯ ПРОГРАММА
    // ============================

    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("=".PadRight(70, '='));
            Console.WriteLine("🔬 WHAT-IF АНАЛИЗ: СИСТЕМА МОНИТОРИНГА МИКРОСЕРВИСОВ");
            Console.WriteLine("=".PadRight(70, '='));
            Console.WriteLine("\nЗапуск нескольких сценариев с разными параметрами...\n");

            // Создаем менеджер симуляций
            var simulator = new SimulationManager();
            var comparison = new ScenarioComparison();

            // ============================
            // НАСТРОЙКА СЦЕНАРИЕВ
            // ============================

            var scenarios = new List<ScenarioConfig>
            {
                // Сценарий 1: Оптимистичный (все хорошо)
                new ScenarioConfig
                {
                    ScenarioName = "Оптимистичный",
                    Description = "Идеальные условия, низкая нагрузка",
                    TotalRequests = 80,
                    FastServiceRatio = 0.6,
                    ServiceConfigs = new List<ServiceConfig>
                    {
                        new ServiceConfig("FastService1", "Быстрый сервис", 30, 0.02, 20),
                        new ServiceConfig("SlowService1", "Медленный сервис", 120, 0.08, 40)
                    }
                },
                
                // Сценарий 2: Реальный (текущая ситуация)
                new ScenarioConfig
                {
                    ScenarioName = "Реальный",
                    Description = "Текущая конфигурация системы",
                    TotalRequests = 100,
                    FastServiceRatio = 0.5,
                    ServiceConfigs = new List<ServiceConfig>
                    {
                        new ServiceConfig("FastService2", "Быстрый сервис", 50, 0.05, 30),
                        new ServiceConfig("SlowService2", "Медленный сервис", 200, 0.15, 50)
                    }
                },
                
                // Сценарий 3: Пессимистичный (проблемы)
                new ScenarioConfig
                {
                    ScenarioName = "Пессимистичный",
                    Description = "Высокая нагрузка, проблемы с сетью",
                    TotalRequests = 120,
                    FastServiceRatio = 0.4,
                    ServiceConfigs = new List<ServiceConfig>
                    {
                        new ServiceConfig("FastService3", "Быстрый сервис", 100, 0.10, 60),
                        new ServiceConfig("SlowService3", "Медленный сервис", 450, 0.25, 100)
                    }
                },
                
                // Сценарий 4: Критический (все плохо)
                new ScenarioConfig
                {
                    ScenarioName = "Критический",
                    Description = "Система на грани сбоя",
                    TotalRequests = 150,
                    FastServiceRatio = 0.3,
                    ServiceConfigs = new List<ServiceConfig>
                    {
                        new ServiceConfig("FastService4", "Быстрый сервис", 200, 0.20, 100),
                        new ServiceConfig("SlowService4", "Медленный сервис", 800, 0.40, 200)
                    }
                },
                
                // Сценарий 5: Балансированный (оптимальный)
                new ScenarioConfig
                {
                    ScenarioName = "Балансированный",
                    Description = "Оптимизированная конфигурация",
                    TotalRequests = 100,
                    FastServiceRatio = 0.7,
                    ServiceConfigs = new List<ServiceConfig>
                    {
                        new ServiceConfig("FastService5", "Быстрый сервис", 40, 0.03, 25),
                        new ServiceConfig("SlowService5", "Медленный сервис", 180, 0.12, 45)
                    }
                }
            };

            // ============================
            // ЗАПУСК ВСЕХ СЦЕНАРИЕВ
            // ============================

            int scenarioNumber = 1;
            foreach (var scenario in scenarios)
            {
                Console.WriteLine($"\n[{scenarioNumber}/{scenarios.Count}] ");

                var result = simulator.RunScenario(scenario);
                result.PrintSummary(new HealthEvaluator());

                comparison.AddResult(result);

                scenarioNumber++;

                // Пауза между сценариями
                if (scenarioNumber <= scenarios.Count)
                {
                    Console.WriteLine("\n⏳ Подготовка следующего сценария...");
                    System.Threading.Thread.Sleep(1000);
                }
            }

            // ============================
            // СРАВНИТЕЛЬНЫЙ АНАЛИЗ
            // ============================

            comparison.PrintComparison();

            // ============================
            // ВЫВОД СТАТИСТИКИ
            // ============================

            PrintFinalStatistics(comparison.Results);

            Console.WriteLine("\n" + "=".PadRight(70, '='));
            Console.WriteLine("✅ WHAT-IF АНАЛИЗ ЗАВЕРШЕН");
            Console.WriteLine("=".PadRight(70, '='));

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

        static void PrintFinalStatistics(List<ScenarioResult> results)
        {
            Console.WriteLine("\n📈 СТАТИСТИКА ВСЕХ СЦЕНАРИЕВ:");
            Console.WriteLine(new string('─', 60));

            int totalRequests = results.Sum(r => r.ServiceResults.Sum(s => s.Value.TotalRequests));
            int totalSuccess = results.Sum(r => r.ServiceResults.Sum(s => s.Value.SuccessfulRequests));
            int totalFailures = results.Sum(r => r.ServiceResults.Sum(s => s.Value.FailedRequests));

            Console.WriteLine($"• Всего обработано запросов: {totalRequests}");
            Console.WriteLine($"• Успешных запросов: {totalSuccess} ({(double)totalSuccess / totalRequests:P1})");
            Console.WriteLine($"• Ошибочных запросов: {totalFailures} ({(double)totalFailures / totalRequests:P1})");
            Console.WriteLine($"• Всего сценариев: {results.Count}");

            var healthyScenarios = results.Count(r => r.ServiceHealth.All(h => h.Value == ServiceHealth.Healthy));
            var degradedScenarios = results.Count(r => r.ServiceHealth.Any(h => h.Value == ServiceHealth.Degraded));
            var unhealthyScenarios = results.Count(r => r.ServiceHealth.Any(h => h.Value == ServiceHealth.Unhealthy));

            Console.WriteLine($"• Сценариев со здоровыми сервисами: {healthyScenarios}");
            Console.WriteLine($"• Сценариев с деградацией: {degradedScenarios}");
            Console.WriteLine($"• Сценариев с нездоровыми сервисами: {unhealthyScenarios}");

            // Находим лучший и худший сценарии
            var bestScenario = results
                .OrderByDescending(r => r.ServiceResults.Sum(s => s.Value.SuccessfulRequests))
                .ThenBy(r => r.ServiceResults.Average(s => s.Value.AverageLatencyMs))
                .FirstOrDefault();

            var worstScenario = results
                .OrderBy(r => r.ServiceResults.Sum(s => s.Value.SuccessfulRequests))
                .ThenByDescending(r => r.ServiceResults.Average(s => s.Value.AverageLatencyMs))
                .FirstOrDefault();

            if (bestScenario != null && worstScenario != null)
            {
                Console.WriteLine($"\n🏆 Лучший сценарий: '{bestScenario.ScenarioName}'");
                Console.WriteLine($"💀 Худший сценарий: '{worstScenario.ScenarioName}'");
            }
        }
    }
}