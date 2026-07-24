using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Server.Network;

namespace DungeonChessBattle.Server;

public class GameServer
{
    private readonly ServerNetworkManager _networkManager;
    private readonly GameLogicService _logicService;

    public GameServer()
    {
        _logicService = new GameLogicService();
        _networkManager = new ServerNetworkManager();
    }

    public void Start()
    {
        _networkManager.Start();
    }

    public void Stop()
    {
        _networkManager.Stop();
    }
}