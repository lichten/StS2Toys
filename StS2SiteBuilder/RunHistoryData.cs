using System.Text.Json.Serialization;

public class RunHistoryData
{
    [JsonPropertyName("win")]                 public bool Win { get; init; }
    [JsonPropertyName("was_abandoned")]       public bool WasAbandoned { get; init; }
    [JsonPropertyName("start_time")]          public long StartTime { get; init; }
    [JsonPropertyName("run_time")]            public int RunTime { get; init; }
    [JsonPropertyName("ascension")]           public int Ascension { get; init; }
    [JsonPropertyName("seed")]                public string Seed { get; init; } = "";
    [JsonPropertyName("build_id")]            public string BuildId { get; init; } = "";
    [JsonPropertyName("killed_by_encounter")] public string KilledByEncounter { get; init; } = "";
    [JsonPropertyName("acts")]                public List<string> Acts { get; init; } = [];
    [JsonPropertyName("players")]             public List<RunPlayerData> Players { get; init; } = [];
    [JsonPropertyName("map_point_history")]   public List<List<RunMapPoint>> MapPointHistory { get; init; } = [];
}

public class RunPlayerData
{
    [JsonPropertyName("character")] public string Character { get; init; } = "";
    [JsonPropertyName("deck")]      public List<RunCardData> Deck { get; init; } = [];
    [JsonPropertyName("relics")]    public List<RunRelicData> Relics { get; init; } = [];
}

public class RunCardData
{
    [JsonPropertyName("id")]                   public string Id { get; init; } = "";
    [JsonPropertyName("floor_added_to_deck")]  public int FloorAddedToDeck { get; init; }
    [JsonPropertyName("current_upgrade_level")]public int? CurrentUpgradeLevel { get; init; }
    [JsonPropertyName("enchantment")]          public RunEnchantmentData? Enchantment { get; init; }
}

public class RunEnchantmentData
{
    [JsonPropertyName("id")]     public string Id { get; init; } = "";
    [JsonPropertyName("amount")] public int Amount { get; init; }
}

public class RunRelicData
{
    [JsonPropertyName("id")]                  public string Id { get; init; } = "";
    [JsonPropertyName("floor_added_to_deck")] public int FloorAddedToDeck { get; init; }
}

public class RunMapPoint
{
    [JsonPropertyName("map_point_type")] public string MapPointType { get; init; } = "";
    [JsonPropertyName("rooms")]          public RunMapPointRoom? Rooms { get; init; }
    [JsonPropertyName("player_stats")]   public List<RunFloorStats>? PlayerStats { get; init; }
}

public class RunMapPointRoom
{
    [JsonPropertyName("model_id")]    public string ModelId { get; init; } = "";
    [JsonPropertyName("monster_ids")] public List<string> MonsterIds { get; init; } = [];
    [JsonPropertyName("room_type")]   public string RoomType { get; init; } = "";
    [JsonPropertyName("turns_taken")] public int TurnsTaken { get; init; }
}

public class RunFloorStats
{
    [JsonPropertyName("current_hp")]       public int CurrentHp { get; init; }
    [JsonPropertyName("max_hp")]           public int MaxHp { get; init; }
    [JsonPropertyName("damage_taken")]     public int DamageTaken { get; init; }
    [JsonPropertyName("hp_healed")]        public int HpHealed { get; init; }
    [JsonPropertyName("current_gold")]     public int CurrentGold { get; init; }
    [JsonPropertyName("card_choices")]     public List<RunCardChoice>? CardChoices { get; init; }
    [JsonPropertyName("relic_choices")]    public List<RunRelicChoice>? RelicChoices { get; init; }
    [JsonPropertyName("ancient_choice")]   public List<RunAncientChoice>? AncientChoice { get; init; }
    [JsonPropertyName("cards_gained")]     public List<RunCardIdEntry>? CardsGained { get; init; }
    [JsonPropertyName("cards_removed")]    public List<RunCardIdEntry>? CardsRemoved { get; init; }
    [JsonPropertyName("rest_site_choices")]public List<string>? RestSiteChoices { get; init; }
    [JsonPropertyName("bought_relics")]    public List<string>? BoughtRelics { get; init; }
}

public class RunCardChoice
{
    [JsonPropertyName("card")]       public RunCardIdEntry? Card { get; init; }
    [JsonPropertyName("was_picked")] public bool WasPicked { get; init; }
}

public class RunRelicChoice
{
    [JsonPropertyName("choice")]     public string Choice { get; init; } = "";
    [JsonPropertyName("was_picked")] public bool WasPicked { get; init; }
}

public class RunAncientChoice
{
    [JsonPropertyName("TextKey")]    public string TextKey { get; init; } = "";
    [JsonPropertyName("was_chosen")] public bool WasChosen { get; init; }
}

public class RunCardIdEntry
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
}
