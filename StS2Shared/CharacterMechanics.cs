namespace StS2Shared.Services;

public record MechanicDef(string EnLabel, string JaLabel, Func<string, bool> Filter);
public record CharGroup(string EnLabel, string JaLabel, MechanicDef[] Mechanics);

public static class CharacterMechanics
{
    public static readonly CharGroup[] All =
    [
        new("Necrobinder", "ネクロバインダー",
        [
            new("Osty", "Osty", CardDatabaseService.IsNecroOsty),
            new("Soul", "Soul", CardDatabaseService.IsNecroSoul),
            new("Doom", "Doom", CardDatabaseService.IsNecroDoom),
        ]),
        new("Ironclad", "アイアンクラッド",
        [
            new("Strength", "ストレングス",   CardDatabaseService.IsIroncladStrength),
            new("Exhaust",  "エグゾースト",   CardDatabaseService.IsIroncladExhaust),
            new("Strike",   "ストライク",     CardDatabaseService.IsIroncladStrike),
        ]),
        new("Silent", "サイレント",
        [
            new("Poison", "毒",   CardDatabaseService.IsSilentPoison),
            new("Shiv",   "Shiv", CardDatabaseService.IsSilentShiv),
        ]),
        new("Defect", "ディフェクト",
        [
            new("Channel",  "チャネル",     CardDatabaseService.IsDefectChannel),
            new("Evoke",    "発動",         CardDatabaseService.IsDefectEvoke),
            new("Focus",    "フォーカス",   CardDatabaseService.IsDefectFocus),
            new("0 Energy", "0エネルギー",  CardDatabaseService.IsDefectZeroEnergy),
        ]),
        new("Regent", "リージェント",
        [
            new("Forge / Sovereign Blade", "Forge / Sovereign Blade",
                id => CardDatabaseService.IsRegentForge(id) || CardDatabaseService.IsRegentBlade(id)),
            new("Card Creation", "カード作成シナジー", CardDatabaseService.IsRegentCreate),
            new("Star Gain",     "Starを得る",         CardDatabaseService.IsRegentStarGain),
            new("Star Spend",    "Starを使用する",     CardDatabaseService.IsRegentStarSpend),
        ]),
        new("Other", "その他", []),
        new("Common", "共通",
        [
            new("Weak",       "脱力", CardDatabaseService.IsWeak),
            new("Vulnerable", "弱体", CardDatabaseService.IsVulnerable),
        ]),
    ];

    public static MechanicDef[] MechanicsFor(string enLabel) =>
        All.FirstOrDefault(g => g.EnLabel == enLabel)?.Mechanics ?? [];
}
