using System.Text.Json;
using ChatManagerApi;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_Tags;

/// <summary>
/// VIP_Tags, refactored to be a TAG PROVIDER for the shared ChatManager instead of hooking chat and
/// writing player.Clan directly. It still exposes a per-VIP-group "Tags" feature (toggleable from
/// the VIP menu) and reads the group's ChatColor / ClanTag / NameColor / NameTag, but hands those
/// to ChatManager, which merges them with other providers (e.g. CS2-Tags) into one chat line and
/// one scoreboard tag. This removes the double-message / clan-tag-overwrite conflict.
/// </summary>
public class VipTags : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Tags";
    public override string ModuleVersion => "v1.0.0";

    private IVipCoreApi? _api;
    private TagsFeature? _feature;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _feature = new TagsFeature(this, _api);
        // Toggle so players can enable/disable their VIP tags from the menu.
        _api.RegisterFeature(_feature, FeatureType.Toggle);
    }

    public override void Unload(bool hotReload)
    {
        if (_feature != null)
            _api?.UnRegisterFeature(_feature);

        _feature?.OnUnload();
    }
}

/// <summary>
/// Per-VIP-group value read from the group's "Tags" feature config:
///   ChatColor  - colour of the chat MESSAGE text (colour name, e.g. "lime" or "teamcolor")
///   ClanTag    - plain-text tag shown on the scoreboard (no colours)
///   NameColor  - colour of the player's NAME in chat (colour name or "teamcolor")
///   NameTag    - prefix shown before the name in chat; may contain inline {color} / {teamcolor}
/// </summary>
public class TagsValue
{
    public string ChatColor { get; set; } = "";
    public string ClanTag { get; set; } = "";
    public string NameColor { get; set; } = "";
    public string NameTag { get; set; } = "";
}

public class TagsFeature : VipFeatureBase
{
    public override string Feature => "Tags";

    // VIP is the "left" source: its prefix sits BEFORE the admin prefix (lower Order), but it LOSES
    // the name/message colour contest to admin (lower ColorPriority).
    private const int TagOrder = 10;
    private const int TagColorPriority = 50;
    private const string ProviderName = "vipcore";

    private readonly VipTags _plugin;
    private readonly PluginCapability<IChatManagerApi?> _chatManagerCap = IChatManagerApi.Capability;
    private IChatManagerApi? _chatManager;

    public TagsFeature(VipTags plugin, IVipCoreApi api) : base(api)
    {
        _plugin = plugin;

        // Resolve ChatManager and register ourselves as the VIP tag provider.
        _chatManager = _chatManagerCap.Get();
        _chatManager?.RegisterProvider(ProviderName, GetTag);
    }

    /// <summary>Called from the plugin's Unload so we drop our provider registration.</summary>
    public void OnUnload()
    {
        _chatManager?.UnregisterProvider(ProviderName);
    }

    // When the player toggles the feature or respawns, ask ChatManager to recompute the scoreboard
    // tag so it reflects the new state right away.
    public override void OnSelectItem(CCSPlayerController player, FeatureState state)
    {
        _chatManager?.RefreshTags(player);
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        _chatManager?.RefreshTags(player);
    }

    /// <summary>
    /// Returns the VIP tag for a player, or null when they are not VIP, lack the feature, or have it
    /// disabled - in which case ChatManager adds no VIP tag for them.
    /// </summary>
    private ChatTag? GetTag(CCSPlayerController player)
    {
        if (player is not { IsValid: true } || player.IsBot)
            return null;

        if (!IsClientVip(player) || !PlayerHasFeature(player) ||
            GetPlayerFeatureState(player) != FeatureState.Enabled)
            return null;

        var value = ReadGroupValue(player);
        if (value == null)
            return null;

        return new ChatTag
        {
            // NameTag may contain inline {color}/{teamcolor}; ChatManager resolves them.
            ChatPrefix = value.NameTag ?? string.Empty,
            // Scoreboard clan tag is plain text; ChatManager strips any stray {color} tokens.
            ScoreboardTag = value.ClanTag ?? string.Empty,
            NameColor = value.NameColor ?? string.Empty,
            MessageColor = value.ChatColor ?? string.Empty,
            Order = TagOrder,
            ColorPriority = TagColorPriority
        };
    }

    private TagsValue? ReadGroupValue(CCSPlayerController player)
    {
        try
        {
            var raw = GetFeatureValue<JsonElement>(player).GetRawText();
            if (string.IsNullOrWhiteSpace(raw) || raw == "null")
                return null;
            return JsonSerializer.Deserialize<TagsValue>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }
}
