using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

// 引数: [sts2.dll のパス] [出力 JSON のパス]
// 省略時はデフォルトパスを使用
var dllPath = args.Length > 0
    ? args[0]
    : @"C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll";

// デフォルト出力先: リポジトリルートの StS2Toys/Resources/
// AppContext.BaseDirectory = .../card-type-extractor/bin/Debug/net10.0/
// 4階層上がるとリポジトリルート
var defaultOut = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\StS2Toys\Resources\card_types.json"));
var outPath = args.Length > 1 ? args[1] : defaultOut;

using var stream = File.OpenRead(dllPath);
using var peReader = new PEReader(stream);
var mr = peReader.GetMetadataReader();

// CardType enum 値マップ
var cardTypeByInt = new Dictionary<int, string>();
foreach (var typeHandle in mr.TypeDefinitions)
{
    var typeDef = mr.GetTypeDefinition(typeHandle);
    if (mr.GetString(typeDef.Name) != "CardType") continue;
    foreach (var fh in typeDef.GetFields())
    {
        var field = mr.GetFieldDefinition(fh);
        var fn = mr.GetString(field.Name);
        if (fn == "value__") continue;
        var ch = field.GetDefaultValue();
        if (ch.IsNil) continue;
        var br = mr.GetBlobReader(mr.GetConstant(ch).Value);
        cardTypeByInt[br.ReadInt32()] = fn;
    }
    break;
}

// CardModel .ctor: どの引数位置が CardType か確認済み → param[2] = type
// Alchemize の例: ldarg.0, ldc.i4(cost), ldc.i4(type), ldc.i4(rarity), ...
// つまり IL の ldc.i4 系が 2 番目に押し込まれる値が CardType

// カードクラス名の Set を CardPool.GenerateAllCards IL から取得
var cardClasses = new HashSet<string>(StringComparer.Ordinal);
foreach (var typeHandle in mr.TypeDefinitions)
{
    var typeDef = mr.GetTypeDefinition(typeHandle);
    if (!mr.GetString(typeDef.Name).EndsWith("CardPool")) continue;
    foreach (var mh in typeDef.GetMethods())
    {
        var method = mr.GetMethodDefinition(mh);
        if (mr.GetString(method.Name) != "GenerateAllCards") continue;
        if (method.RelativeVirtualAddress == 0) continue;
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        if (body == null) continue;
        var il = body.GetILBytes();

        for (int i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != 0x28) continue;
            int token = il[i+1] | (il[i+2]<<8) | (il[i+3]<<16) | (il[i+4]<<24);
            var typeName = ResolveMethodSpecFirstArg(mr, token);
            if (!string.IsNullOrEmpty(typeName))
                cardClasses.Add(typeName);
        }
    }
}

// 各カードクラスの .ctor IL から CardType (2番目の ldc.i4) を取得
var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

foreach (var typeHandle in mr.TypeDefinitions)
{
    var typeDef = mr.GetTypeDefinition(typeHandle);
    var className = mr.GetString(typeDef.Name);
    if (!cardClasses.Contains(className)) continue;

    foreach (var mh in typeDef.GetMethods())
    {
        var method = mr.GetMethodDefinition(mh);
        if (mr.GetString(method.Name) != ".ctor") continue;
        if (method.RelativeVirtualAddress == 0) continue;
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        if (body == null) continue;
        var il = body.GetILBytes();

        // ldc.i4 系の値を順番に収集
        var intArgs = new List<int>();
        for (int i = 0; i < il.Length; )
        {
            var (val, size) = ReadLdcI4(il, i);
            if (val.HasValue)
                intArgs.Add(val.Value);
            i += size;
        }

        // 2番目の整数引数が CardType
        if (intArgs.Count >= 2 && cardTypeByInt.TryGetValue(intArgs[1], out var typeName))
        {
            var cardId = "CARD." + CamelToUpperSnake(className);
            results.TryAdd(cardId, typeName);
        }
        break; // .ctor は一つだけ
    }
}

// JSON 出力
Console.Error.WriteLine($"Extracted {results.Count} card type mappings.");
// outPath は上部で決定済み
var jsonLines = results.OrderBy(kv => kv.Key)
    .Select(kv => $"  \"{kv.Key}\": \"{kv.Value}\"");
File.WriteAllText(outPath, "{\n" + string.Join(",\n", jsonLines) + "\n}\n");
Console.WriteLine(outPath);

// ---- helpers ----

static string CamelToUpperSnake(string name)
{
    // BeaconOfHope → BEACON_OF_HOPE
    var result = Regex.Replace(name, @"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", "_");
    return result.ToUpperInvariant();
}

static (int? val, int size) ReadLdcI4(byte[] il, int i)
{
    if (i >= il.Length) return (null, 1);
    return il[i] switch
    {
        0x15 => (-1, 1),
        0x16 => (0, 1),
        0x17 => (1, 1),
        0x18 => (2, 1),
        0x19 => (3, 1),
        0x1A => (4, 1),
        0x1B => (5, 1),
        0x1C => (6, 1),
        0x1D => (7, 1),
        0x1E => (8, 1),
        0x1F when i+1 < il.Length => ((int?)(sbyte)il[i+1], 2),
        0x20 when i+4 < il.Length =>
            ((int?)(il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24)), 5),
        0x28 or 0x6F or 0x73 or 0x74 => (null, 5), // call 系: 4バイトトークン skip
        0x72 => (null, 5),  // ldstr
        0x7B or 0x7D or 0x7E or 0x80 => (null, 5), // ldfld/stfld etc.
        0xFE => (null, 2),  // 2バイトオペコード
        0x2B or 0x2C or 0x2D or 0x2E or 0x2F => (null, 2), // br.s 等
        0x38 or 0x39 or 0x3A or 0x3B or 0x3C or 0x3D or 0x3E or 0x3F or 0x40
            or 0x41 or 0x45 => (null, 5), // br 等
        0x0E or 0x0F or 0x12 or 0x13 => (null, 2), // ldarg.s/starg.s/ldloc.s/stloc.s
        _ => (null, 1)
    };
}

static string ResolveMethodSpecFirstArg(MetadataReader mr, int token)
{
    try
    {
        var h = MetadataTokens.EntityHandle(token);
        if (h.Kind != HandleKind.MethodSpecification) return "";
        var spec = mr.GetMethodSpecification((MethodSpecificationHandle)h);
        var sig = mr.GetBlobReader(spec.Signature);
        sig.ReadByte(); // 0x0A generic
        int count = sig.ReadCompressedInteger();
        if (count < 1) return "";
        byte code = sig.ReadByte();
        if (code is not (0x11 or 0x12)) return "";
        int typeToken = sig.ReadCompressedInteger();
        int tag = typeToken & 3;
        int row = typeToken >> 2;
        if (tag == 0)
            return mr.GetString(mr.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(row)).Name);
        if (tag == 1)
            return mr.GetString(mr.GetTypeReference(MetadataTokens.TypeReferenceHandle(row)).Name);
        return "";
    }
    catch { return ""; }
}
