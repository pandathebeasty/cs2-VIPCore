﻿using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;

namespace VIP_Items;

public class VipItems : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Items";
    public override string ModuleVersion => "1.0.1";

    private Items _items = null!;
    private IVipCoreApi? _api;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _items = new Items(_api);
        _api.RegisterFeature(_items);
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_items);
    }
}

public class Items : VipFeatureBase
{
    public override string Feature => "Items";
    
    private readonly Dictionary<string, int> _grenadeIndex = new()
    {
        ["flashbang"] = 14, 
        ["smokegrenade"] = 15,
        ["decoy"] = 17,
        ["incgrenade"] = 16,
        ["molotov"] = 16,
        ["hegrenade"] = 13
    };
    
    public Items(IVipCoreApi api) : base(api)
    {
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (IsPistolRound()) return;

        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return;

        var itemsConfig = GetFeatureValue<Dictionary<string, List<string>>?>(player);
        if (itemsConfig == null) return;

        var teamKey = player.TeamNum switch
        {
            2 => "T",
            3 => "CT",
            _ => null
        };

        if (teamKey == null || !itemsConfig.ContainsKey(teamKey)) return;

        var items = itemsConfig[teamKey];

        var playerPawnValue = player.PlayerPawn.Value;
        if (playerPawnValue == null) return;

        var weaponService = playerPawnValue.WeaponServices;
        if (weaponService == null || items is not { Count: > 0 }) return;

        foreach (var item in items)
        {
            var itemName = _grenadeIndex.ContainsKey(item) ? item : null;
            var ammoIndex = itemName != null ? _grenadeIndex[item] : -1;
            
            if (itemName != null && weaponService.Ammo[ammoIndex] == 0)
                player.GiveNamedItem(item);
            else
            {
                if (weaponService.MyWeapons.ToList().Find(m => m.Value != null && m.Value.DesignerName == item) == null)
                    player.GiveNamedItem(item);
            }
        }
    }
}