namespace StS2Shared.Services;

public static class CharacterMechanics
{
    public static readonly (string CharLabel, (string MecLabel, Func<string, bool> Filter)[] Mechanics)[] All =
    [
        ("Necrobinder",
        [
            ("Osty",  CardDatabaseService.IsNecroOsty),
            ("Soul",  CardDatabaseService.IsNecroSoul),
            ("Doom",  CardDatabaseService.IsNecroDoom),
        ]),
        ("Ironclad",
        [
            ("Strength", CardDatabaseService.IsIroncladStrength),
            ("Exhaust",  CardDatabaseService.IsIroncladExhaust),
            ("Strike",   CardDatabaseService.IsIroncladStrike),
        ]),
        ("Silent",
        [
            ("Poison", CardDatabaseService.IsSilentPoison),
            ("Shiv",   CardDatabaseService.IsSilentShiv),
        ]),
        ("Defect",
        [
            ("Channel", CardDatabaseService.IsDefectChannel),
            ("Evoke",   CardDatabaseService.IsDefectEvoke),
            ("Focus",   CardDatabaseService.IsDefectFocus),
        ]),
        ("Regent",
        [
            ("Forge / Sovereign Blade", id => CardDatabaseService.IsRegentForge(id) || CardDatabaseService.IsRegentBlade(id)),
            ("カード作成シナジー",        CardDatabaseService.IsRegentCreate),
        ]),
        ("その他", []),
    ];

    public static (string MecLabel, Func<string, bool> Filter)[] MechanicsFor(string charLabel) =>
        All.FirstOrDefault(c => c.CharLabel == charLabel).Mechanics ?? [];
}
