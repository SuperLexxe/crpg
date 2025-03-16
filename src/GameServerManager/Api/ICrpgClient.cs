using Crpg.GameServerManager.Api.Models;

namespace Crpg.GameServerManager.Api;

internal interface ICrpgClient : IDisposable
{
    Task<CrpgResult<IList<CrpgBattle>>> GetUpcomingStrategusBattles(CrpgRegion region,
        CancellationToken cancellationToken = default);
    Task<CrpgResult<CrpgBattle>> ClaimStrategusBattle(CrpgClaimBattleRequest req,
        CancellationToken cancellationToken = default);
}
