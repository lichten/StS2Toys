using System.Text.Json.Serialization;

namespace StS2Toys.Models;

public class RunSaveData
{
    [JsonPropertyName("ascension")]
    public int Ascension { get; init; }

    [JsonPropertyName("current_act_index")]
    public int CurrentActIndex { get; init; }

    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("players")]
    public List<PlayerData> Players { get; init; } = [];

    [JsonPropertyName("map_point_history")]
    public List<List<MapPointHistoryEntry>> MapPointHistory { get; init; } = [];

    [JsonPropertyName("acts")]
    public List<ActData> Acts { get; init; } = [];
}

public class ActData
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("rooms")]
    public ActRooms? Rooms { get; init; }
}

public class ActRooms
{
    [JsonPropertyName("boss_id")]
    public string? BossId { get; init; }

    [JsonPropertyName("second_boss_id")]
    public string? SecondBossId { get; init; }

    [JsonPropertyName("elite_encounter_ids")]
    public List<string> EliteEncounterIds { get; init; } = [];

    [JsonPropertyName("elite_encounters_visited")]
    public int EliteEncountersVisited { get; init; }

    [JsonPropertyName("boss_encounters_visited")]
    public int BossEncountersVisited { get; init; }
}

public class MapPointHistoryEntry
{
    [JsonPropertyName("map_point_type")]
    public string MapPointType { get; init; } = "";

    [JsonPropertyName("player_stats")]
    public List<PlayerFloorStats>? PlayerStats { get; init; }
}

public class PlayerFloorStats
{
    [JsonPropertyName("current_hp")]
    public int CurrentHp { get; init; }

    [JsonPropertyName("max_hp")]
    public int MaxHp { get; init; }

    [JsonPropertyName("damage_taken")]
    public int DamageTaken { get; init; }

    [JsonPropertyName("hp_healed")]
    public int HpHealed { get; init; }

    [JsonPropertyName("max_hp_gained")]
    public int MaxHpGained { get; init; }

    [JsonPropertyName("max_hp_lost")]
    public int MaxHpLost { get; init; }

    [JsonPropertyName("current_gold")]
    public int CurrentGold { get; init; }
}

public class PlayerData
{
    [JsonPropertyName("character_id")]
    public string CharacterId { get; init; } = "";

    [JsonPropertyName("current_hp")]
    public int CurrentHp { get; init; }

    [JsonPropertyName("max_hp")]
    public int MaxHp { get; init; }

    [JsonPropertyName("gold")]
    public int Gold { get; init; }

    [JsonPropertyName("max_energy")]
    public int MaxEnergy { get; init; }

    [JsonPropertyName("deck")]
    public List<CardData> Deck { get; init; } = [];

    [JsonPropertyName("relics")]
    public List<RelicData> Relics { get; init; } = [];
}

public class EnchantmentData
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("amount")]
    public int Amount { get; init; }
}

public class CardData
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("floor_added_to_deck")]
    public int FloorAddedToDeck { get; init; }

    [JsonPropertyName("current_upgrade_level")]
    public int? CurrentUpgradeLevel { get; init; }

    [JsonPropertyName("enchantment")]
    public EnchantmentData? Enchantment { get; init; }

    [JsonPropertyName("props")]
    public CardProps? Props { get; init; }

    public int? GetPropInt(string name) =>
        Props?.Ints.FirstOrDefault(x => x.Name == name)?.Value;
}

public class CardProps
{
    [JsonPropertyName("ints")]
    public List<NamedInt> Ints { get; init; } = [];
}

public class NamedInt
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("value")]
    public int Value { get; init; }
}

public class RelicData
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("floor_added_to_deck")]
    public int FloorAddedToDeck { get; init; }
}
