using System.Text.Json;
using System.Text.Json.Serialization;
using Crpg.Domain.Entities;
using Crpg.Domain.Entities.Servers;
using Crpg.GameServerManager.Api;
using Crpg.GameServerManager.Api.Models;
using Crpg.GameServerManager.Common;
using Crpg.GameServerManager.Updater;

namespace Crpg.GameServerManager.GameServers;
public class GameServerManager
{
    private static readonly object _lock = new();
    private static GameServerManager? _instance;
    public static GameServerManager Instance
    {
        get
        {
            lock (_lock)
            {
                return _instance ??= new GameServerManager(CrpgClient.Create());
            }
        }
    }

    public CrpgRegion Region { get; set; }
    public Dictionary<GameMode, GameServer> GameServers { get; private set; } = new();

    private readonly ICrpgClient _client;

    private GameServerManager(ICrpgClient client)
    {
        _client = client;
        string? regionStr = Environment.GetEnvironmentVariable("CRPG_REGION");
        Region = Enum.TryParse(regionStr, ignoreCase: true, out CrpgRegion region) ? region : CrpgRegion.Eu;
    }

    public void InitialiseGameServers()
    {
        string fileContent = File.ReadAllText(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) + "/GameServers/Configurations/GameServers.json");
        List<GameServer>? servers = JsonSerializer.Deserialize<List<GameServer>>(fileContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Converters = { new JsonStringEnumConverter() },
        });

        if (servers != null)
        {
            foreach (GameServer server in servers)
            {
                GameServers.Add(server.Mode, server);
            }
        }
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        Task monitorSchedulesTask = MonitorSchedules(stoppingToken);
        Task monitorUpdatesTask = MonitorUpdates(stoppingToken);
        Task monitorWindowNames = MonitorWindowNames(stoppingToken);
        Task monitorUpcomingBattles = MonitorUpcomingBattles(stoppingToken);
        List<Task> tasks = new()
        {
            monitorSchedulesTask,
            monitorUpdatesTask,
            monitorWindowNames,
            monitorUpcomingBattles,
        };

        await Task.WhenAll(tasks);
    }

    public void StopAllServers()
    {
        Console.WriteLine($"[{DateTime.UtcNow}] Shutting down all game servers...");
        foreach (var server in GameServers.Values)
        {
            if (server.IsRunning())
            {
                server.StopServer();
            }
        }
    }

    public void RegisterShutdownHandler()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => StopAllServers();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            StopAllServers();
            Environment.Exit(0);
        };
    }

    private async Task MonitorUpdates(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await GameServerUpdater.IsLatestVersion())
                {
                    Console.WriteLine($"[{DateTime.UtcNow}] Servers are out of date, running updater!");
                    StopAllServers();

                    await GameServerUpdater.RunUpdateAsync();
                    continue;
                }
            }
            catch (Exception e)
            {

            }

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }

    private async Task MonitorSchedules(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var server in GameServers.Values)
                {
                    if (server.Schedule?.ShouldTurnOff() == true && server.IsRunning())
                    {
                        Console.WriteLine($"[{DateTime.UtcNow}] Stopping {server.Mode} due to schedule.");
                        server.StopServer();
                    }
                }

                foreach (var server in GameServers.Values)
                {
                    if (!server.IsRunning() && (server.Schedule == null || (server.Schedule?.ShouldTurnOn() == true)))
                    {
                        Console.WriteLine($"[{DateTime.UtcNow}] Starting {server.Mode}.");
                        server.StartServer();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.UtcNow}] Error in MonitorSchedules: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task MonitorWindowNames(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var server in GameServers.Values)
            {
                if (server.IsRunning())
                {
                    WindowManager.SetProcessWindowTitle(server.GameServerProcess!, server.Mode + " - " + server.Instance.ToString());
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task MonitorUpcomingBattles(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            CrpgBattle? upcomingBattle = new();
            var upcomingStrategusBattles = await _client.GetUpcomingStrategusBattles(region: Region);
            if (upcomingStrategusBattles.Data != null)
            {
                upcomingBattle = upcomingStrategusBattles.Data.OrderByDescending(b => b.ScheduledFor).Where(b => b.ScheduledFor - DateTime.UtcNow <= TimeSpan.FromMinutes(30)).FirstOrDefault();
            }

            if (upcomingBattle != null && upcomingBattle.Instance == null)
            {
                var claimedBattle = await _client.ClaimStrategusBattle(new() { BattleId = upcomingBattle.Id, Instance = Environment.GetEnvironmentVariable("CRPG_INSTANCE")! });
            }

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}
