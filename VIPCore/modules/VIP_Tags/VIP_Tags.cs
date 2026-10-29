using ChatManagerApi;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Args;
using VipCoreApi.Enums;

namespace VIP_Tags;

public class VipTags : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Tags";
    public override string ModuleVersion => "v1.1.0";

    private TagsFeature? _feature;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null)
            return;

        _feature = new TagsFeature(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _feature?.Dispose();
    }
}

public class TagsFeature : VipFeature<TagsValue>
{
    private const string ProviderName = "vipcore";

    private const int TagOrder = 10;
    private const int TagColorPriority = 50;

    private readonly IChatManagerApi? _chatManager;

    public TagsFeature(VipTags plugin, IVipCoreApi api)
        : base("Tags", api)
    {
        _chatManager = IChatManagerApi.Capability.Get();

        _chatManager?.RegisterProvider(
            ProviderName,
            GetTag
        );
    }

    public override void Dispose()
    {
        _chatManager?.UnregisterProvider(ProviderName);
        base.Dispose();
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!vip)
            return;

        _chatManager?.RefreshTags(player);
    }

    public override void OnPlayerRemoved(CCSPlayerController player, string group)
    {
        _chatManager?.RefreshTags(player);
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        _chatManager?.RefreshTags(player);
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        var value = GetValue(args.Controller);

        if (value == null)
            return;

        args.Display = value.ClanTag;
    }

    private ChatTag? GetTag(CCSPlayerController player)
    {
        if (player is not { IsValid: true } || player.IsBot)
            return null;

        if (!IsPlayerVip(player))
            return null;

        if (!PlayerHasFeature(player))
            return null;

        if (GetPlayerFeatureState(player) != FeatureState.Enabled)
            return null;

        var value = GetValue(player);

        if (value == null)
            return null;

        return new ChatTag
        {
            ChatPrefix = value.NameTag ?? string.Empty,
            ScoreboardTag = value.ClanTag ?? string.Empty,
            NameColor = value.NameColor ?? string.Empty,
            MessageColor = value.ChatColor ?? string.Empty,
            Order = TagOrder,
            ColorPriority = TagColorPriority
        };
    }
}

public sealed class TagsValue
{
    public string ChatColor { get; set; } = "";
    public string ClanTag { get; set; } = "";
    public string NameColor { get; set; } = "";
    public string NameTag { get; set; } = "";
}
