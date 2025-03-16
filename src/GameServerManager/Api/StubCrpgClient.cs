using Crpg.Domain.Entities;
using Crpg.Domain.Entities.Battles;
using Crpg.GameServerManager.Api.Models;

namespace Crpg.GameServerManager.Api;

internal class StubCrpgClient : ICrpgClient
{
    public Task<CrpgResult<IList<CrpgBattle>>> GetUpcomingStrategusBattles(CrpgRegion region,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CrpgResult<IList<CrpgBattle>>()
        {
            Data = new List<CrpgBattle>()
            {
                new()
                {
                    Phase = BattlePhase.Scheduled,
                    Region = Region.Eu,
                    ScheduledFor = DateTime.UtcNow,
                },
            },
        });
    }

    public Task<CrpgResult<CrpgBattle>> ClaimStrategusBattle(CrpgClaimBattleRequest req, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public void Dispose()
    {
    }
}
