// 成果物インベントリ CLI: ソリューションの生成物（JSON・画像）を実スキャンして artifacts.html を出力する。
// 依存プロジェクトなし（AssetLocator も不使用）— 自分の位置から repoRoot を計算する純粋なディスクスキャナ。
// 使い方: dotnet run --project StS2SiteBuilder [-- --out <出力パス>]

var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
var repoRoot   = Path.GetFullPath(Path.Combine(projectDir, ".."));

var resourcesDir = Path.Combine(repoRoot, "StS2Shared", "Resources");
if (!Directory.Exists(resourcesDir))
{
    Console.Error.WriteLine($"StS2Shared/Resources が見つかりません（リポジトリ外で実行？）: {resourcesDir}");
    Environment.Exit(1);
    return;
}

var outPath = Path.Combine(projectDir, "out", "artifacts.html");
for (int i = 0; i < args.Length - 1; i++)
    if (args[i] == "--out") outPath = Path.GetFullPath(args[i + 1]);

var html = ArtifactInventory.BuildHtml(repoRoot);
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, html);
Console.WriteLine($"Generated artifact inventory -> {outPath}");
