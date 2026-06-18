using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using StS2Shared.Services;

// 引数: [sts2.dll のパス] [出力 JSON のパス]
// 省略時はデフォルトパスを使用
const string defaultDll = @"C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll";
var dllPath = (args.Length > 0 && !args[0].StartsWith("--")) ? args[0] : defaultDll;

// バージョン文字列を release_info.json から取得（sts2.dll の一つ上のフォルダ）
var releaseInfoPath = Path.GetFullPath(
    Path.Combine(Path.GetDirectoryName(dllPath)!, "..", "release_info.json"));
var gameVersion = File.Exists(releaseInfoPath)
    ? System.Text.Json.JsonDocument.Parse(File.ReadAllText(releaseInfoPath))
          .RootElement.GetProperty("version").GetString()!
    : "unknown";

// デフォルト出力先: StS2Shared/Resources/{version}/
// AppContext.BaseDirectory = .../card-type-extractor/bin/Debug/net10.0/
// 4階層上がるとリポジトリルート
var defaultOut = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, $@"..\..\..\..\StS2Shared\Resources\{gameVersion}\card_types.json"));
// 特殊調査モード（--dump-* / --list-* など）は outPath 不要なので、args[1] をそのまま使わない
bool isDebugMode = args.Length > 0 && args[0].StartsWith("--");
var outPath = isDebugMode
    ? defaultOut
    : (args.Length > 1 ? args[1] : defaultOut);
if (!isDebugMode)
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

using var stream = File.OpenRead(dllPath);
using var peReader = new PEReader(stream);
var mr = peReader.GetMetadataReader();

// --- 調査: マニフェスト埋め込みリソース一覧 ---
if (args.Length > 0 && args[0] == "--list-resources")
{
    foreach (var handle in mr.ManifestResources)
    {
        var res = mr.GetManifestResource(handle);
        Console.WriteLine(mr.GetString(res.Name));
    }
    return;
}

// --- 調査: DynamicVarSet の get_Xxx メソッドが使うキー文字列をダンプ ---
if (args.Length > 0 && args[0] == "--dump-varset-keys")
{
    foreach (var typeHandle in mr.TypeDefinitions)
    {
        var typeDef = mr.GetTypeDefinition(typeHandle);
        if (mr.GetString(typeDef.Name) != "DynamicVarSet") continue;
        foreach (var mh in typeDef.GetMethods())
        {
            var method = mr.GetMethodDefinition(mh);
            var methodName = mr.GetString(method.Name);
            if (!methodName.StartsWith("get_") || method.RelativeVirtualAddress == 0) continue;
            var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
            if (body == null) continue;
            var il = body.GetILBytes();
            // ldstr (0x72) at start of method after ldfld
            for (int i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] != 0x72) continue;
                int strToken = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                // castclass (0x74) to find the target type
                int castToken = 0;
                for (int j = i+5; j+4 < il.Length; j++)
                {
                    if (il[j] == 0x74) { castToken = il[j+1]|(il[j+2]<<8)|(il[j+3]<<16)|(il[j+4]<<24); break; }
                }
                string keyStr = "";
                try { keyStr = mr.GetUserString(MetadataTokens.UserStringHandle(strToken & 0xFFFFFF)); } catch { }
                string castType = "";
                try
                {
                    var ch = MetadataTokens.EntityHandle(castToken);
                    if (ch.Kind == HandleKind.TypeDefinition)
                        castType = mr.GetString(mr.GetTypeDefinition((TypeDefinitionHandle)ch).Name);
                    else if (ch.Kind == HandleKind.TypeReference)
                        castType = mr.GetString(mr.GetTypeReference((TypeReferenceHandle)ch).Name);
                    else if (ch.Kind == HandleKind.TypeSpecification)
                        castType = $"TypeSpec(0x{castToken:X8})";
                } catch { }
                Console.WriteLine($"{methodName[4..],24} key=\"{keyStr}\" castTo={castType}");
                break;
            }
        }
        break;
    }
    return;
}

// --- 調査: *Var クラスのコンストラクタトークン一覧を表示 ---
if (args.Length > 0 && args[0] == "--list-var-ctors")
{
    foreach (var typeHandle in mr.TypeDefinitions)
    {
        var typeDef = mr.GetTypeDefinition(typeHandle);
        var typeName = mr.GetString(typeDef.Name);
        if (!typeName.EndsWith("Var")) continue;
        var ns = mr.GetString(typeDef.Namespace);
        if (!ns.Contains("DynamicVars", StringComparison.OrdinalIgnoreCase)) continue;
        foreach (var mh in typeDef.GetMethods())
        {
            var method = mr.GetMethodDefinition(mh);
            if (mr.GetString(method.Name) != ".ctor") continue;
            int token = MetadataTokens.GetToken(mr, mh);
            Console.WriteLine($"0x{token:X8}  {typeName}");
        }
    }
    return;
}

// --- 調査: 型フルネームに部分文字列を含む型を一覧（名前空間・クラス特定用）---
if (args.Length > 0 && args[0] == "--list-types")
{
    var sub = args.Length > 1 ? args[1] : "";
    foreach (var th in mr.TypeDefinitions)
    {
        var td = mr.GetTypeDefinition(th);
        var ns = mr.GetString(td.Namespace);
        var nm = mr.GetString(td.Name);
        var full = string.IsNullOrEmpty(ns) ? nm : ns + "." + nm;
        if (full.Contains(sub, StringComparison.OrdinalIgnoreCase))
            Console.WriteLine(full);
    }
    return;
}

// --- 調査: 指定型のメソッド・フィールド・各メソッドが参照する型名を表示 ---
if (args.Length > 0 && args[0] == "--dump-type")
{
    var target = args.Length > 1 ? args[1] : "";
    foreach (var th in mr.TypeDefinitions)
    {
        var td = mr.GetTypeDefinition(th);
        var ns = mr.GetString(td.Namespace);
        var nm = mr.GetString(td.Name);
        var full = string.IsNullOrEmpty(ns) ? nm : ns + "." + nm;
        if (!full.Equals(target, StringComparison.OrdinalIgnoreCase) &&
            !nm.Equals(target, StringComparison.OrdinalIgnoreCase)) continue;

        Console.WriteLine($"TYPE {full}");
        foreach (var fh in td.GetFields())
        {
            var fd = mr.GetFieldDefinition(fh);
            Console.WriteLine($"  FIELD {mr.GetString(fd.Name)}");
        }
        foreach (var mh in td.GetMethods())
        {
            var md = mr.GetMethodDefinition(mh);
            var mn = mr.GetString(md.Name);
            var refs = new List<string>();
            if (md.RelativeVirtualAddress != 0)
            {
                var body = peReader.GetMethodBody(md.RelativeVirtualAddress);
                var il = body?.GetILBytes();
                if (il != null)
                {
                    var seenStr = new List<string>();
                    for (int i = 0; i + 4 < il.Length; i++)
                    {
                        byte op = il[i];
                        if (op is 0x28 or 0x6F) // call / callvirt
                        {
                            int tok = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                            var tn = ResolveMethodSpecFirstArg(mr, tok);
                            if (!string.IsNullOrEmpty(tn)) refs.Add(tn);
                            i += 4;
                        }
                        else if (op == 0x72) // ldstr
                        {
                            int tok = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                            try { seenStr.Add(mr.GetUserString(MetadataTokens.UserStringHandle(tok & 0xFFFFFF))); } catch { }
                            i += 4;
                        }
                    }
                    if (seenStr.Count > 0) refs.Add("STR:[" + string.Join("|", seenStr.Take(12)) + "]");
                }
            }
            var refStr = refs.Count > 0 ? "  => " + string.Join(", ", refs.Distinct().Take(30)) : "";
            Console.WriteLine($"  METHOD {mn}{refStr}");
        }
    }
    return;
}

// --- 調査: UserString トークンを読み取る ---
if (args.Length > 0 && args[0] == "--read-string")
{
    int token = Convert.ToInt32(args[1], 16);
    int offset = token & 0xFFFFFF;
    var s = mr.GetUserString(MetadataTokens.UserStringHandle(offset));
    Console.WriteLine($"0x{token:X8} → \"{s}\"");
    return;
}

// --- 調査: *Var クラスのコンストラクタが渡す name 文字列を一覧表示 ---
if (args.Length > 0 && args[0] == "--list-var-names")
{
    // DynamicVar..ctor token (base ctor that stores the name)
    const int dynamicVarCtorToken = 0x06007861;
    foreach (var typeHandle in mr.TypeDefinitions)
    {
        var typeDef = mr.GetTypeDefinition(typeHandle);
        var typeName = mr.GetString(typeDef.Name);
        if (!typeName.EndsWith("Var") && !typeName.Contains("Var<")) continue;
        foreach (var mh in typeDef.GetMethods())
        {
            var method = mr.GetMethodDefinition(mh);
            if (mr.GetString(method.Name) != ".ctor") continue;
            if (method.RelativeVirtualAddress == 0) continue;
            var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
            if (body == null) continue;
            var il = body.GetILBytes();
            // Find ldstr (0x72) followed eventually by call DynamicVar..ctor
            for (int i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] != 0x72) continue;
                int strTok = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                // Verify next call after ldstr is DynamicVar..ctor
                bool hasDynCtor = false;
                for (int j = i+5; j+4 < il.Length; j++)
                {
                    if (il[j] == 0x28)
                    {
                        int callTok = il[j+1]|(il[j+2]<<8)|(il[j+3]<<16)|(il[j+4]<<24);
                        if (callTok == dynamicVarCtorToken) { hasDynCtor = true; break; }
                    }
                    if (il[j] == 0x2A) break;
                }
                if (!hasDynCtor) continue;
                string name = "";
                try { name = mr.GetUserString(MetadataTokens.UserStringHandle(strTok & 0xFFFFFF)); } catch { }
                int ctorToken = MetadataTokens.GetToken(mr, mh);
                Console.WriteLine($"0x{ctorToken:X8}  {typeName,-30} name=\"{name}\"");
                break;
            }
        }
    }
    return;
}

// --- 調査: MemberRef の親 TypeSpec を確認 ---
if (args.Length > 0 && args[0] == "--check-memberref-parent")
{
    int token = Convert.ToInt32(args[1], 16);
    var h = MetadataTokens.EntityHandle(token);
    if (h.Kind == HandleKind.MemberReference)
    {
        var member = mr.GetMemberReference((MemberReferenceHandle)h);
        var parent = member.Parent;
        Console.WriteLine($"MemberRef 0x{token:X8} name={mr.GetString(member.Name)} parent.Kind={parent.Kind} parent.Token=0x{MetadataTokens.GetToken(parent):X8}");
    }
    return;
}

// --- 調査: DynamicVarSet プロパティの TypeSpec キャストトークン → プロパティ名 の対応を表示 ---
if (args.Length > 0 && args[0] == "--build-typespec-map")
{
    // DynamicVarSet の各 get_Xxx で使われる castclass TypeSpec トークンを収集
    // → その TypeSpec の MemberRef .ctor を見つける
    var typeSpecToPropName = new Dictionary<int, string>();
    foreach (var typeHandle in mr.TypeDefinitions)
    {
        var typeDef = mr.GetTypeDefinition(typeHandle);
        if (mr.GetString(typeDef.Name) != "DynamicVarSet") continue;
        foreach (var mh in typeDef.GetMethods())
        {
            var method = mr.GetMethodDefinition(mh);
            var methodName = mr.GetString(method.Name);
            if (!methodName.StartsWith("get_") || method.RelativeVirtualAddress == 0) continue;
            var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
            if (body == null) continue;
            var il = body.GetILBytes();
            for (int i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] != 0x74) continue; // castclass
                int castToken = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                var ch = MetadataTokens.EntityHandle(castToken);
                if (ch.Kind == HandleKind.TypeSpecification)
                    typeSpecToPropName.TryAdd(castToken, methodName[4..]);
                break;
            }
        }
        break;
    }
    // 全 MemberReference を走査して TypeSpec 親を持つ .ctor を列挙
    foreach (var mrHandle in mr.MemberReferences)
    {
        var memberRef = mr.GetMemberReference(mrHandle);
        if (mr.GetString(memberRef.Name) != ".ctor") continue;
        if (memberRef.Parent.Kind != HandleKind.TypeSpecification) continue;
        int parentToken = MetadataTokens.GetToken(memberRef.Parent);
        if (!typeSpecToPropName.TryGetValue(parentToken, out var propName)) continue;
        int memberRefToken = MetadataTokens.GetToken(mr, mrHandle);
        Console.WriteLine($"0x{memberRefToken:X8}  → {propName}  (TypeSpec 0x{parentToken:X8})");
    }
    return;
}

// --- 調査: トークンを解決してクラス.メソッド名を表示 ---
if (args.Length > 0 && args[0] == "--resolve-token")
{
    int token = Convert.ToInt32(args[1], 16);
    var h = MetadataTokens.EntityHandle(token);
    if (h.Kind == HandleKind.MethodDefinition)
    {
        var method = mr.GetMethodDefinition((MethodDefinitionHandle)h);
        var methodName = mr.GetString(method.Name);
        var declaringType = mr.GetTypeDefinition(method.GetDeclaringType());
        var ns = mr.GetString(declaringType.Namespace);
        var typeName = mr.GetString(declaringType.Name);
        Console.WriteLine($"{(string.IsNullOrEmpty(ns) ? "" : ns + ".")}{typeName}.{methodName}");
    }
    else if (h.Kind == HandleKind.MemberReference)
    {
        var member = mr.GetMemberReference((MemberReferenceHandle)h);
        var memberName = mr.GetString(member.Name);
        var parentHandle = member.Parent;
        if (parentHandle.Kind == HandleKind.TypeReference)
        {
            var typeRef = mr.GetTypeReference((TypeReferenceHandle)parentHandle);
            Console.WriteLine($"{mr.GetString(typeRef.Namespace)}.{mr.GetString(typeRef.Name)}.{memberName}");
        }
        else
        {
            Console.WriteLine($"(parent kind {parentHandle.Kind}).{memberName}");
        }
    }
    return;
}

// --- 調査: メソッドトークンから IL をダンプ ---
if (args.Length > 0 && args[0] == "--dump-token")
{
    int token = Convert.ToInt32(args[1], 16);
    var h = MetadataTokens.EntityHandle(token);
    if (h.Kind == HandleKind.MethodDefinition)
    {
        var method = mr.GetMethodDefinition((MethodDefinitionHandle)h);
        Console.Error.WriteLine($"Method: {mr.GetString(method.Name)}");
        if (method.RelativeVirtualAddress == 0) { Console.WriteLine("No RVA"); return; }
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        var il = body!.GetILBytes();
        for (int i = 0; i < il.Length;)
        {
            byte op = il[i];
            var (val, size) = ReadLdcI4(il, i);
            if (val.HasValue) { Console.WriteLine($"  [{i:X3}] ldc.i4 {val}"); i += size; continue; }
            if (op == 0x02) { Console.WriteLine($"  [{i:X3}] ldarg.0"); i++; continue; }
            if (op == 0x03) { Console.WriteLine($"  [{i:X3}] ldarg.1"); i++; continue; }
            if (op == 0x04) { Console.WriteLine($"  [{i:X3}] ldarg.2"); i++; continue; }
            if (op == 0x05) { Console.WriteLine($"  [{i:X3}] ldarg.3"); i++; continue; }
            if ((op == 0x28 || op == 0x6F) && i+4 < il.Length)
            {
                int tok = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                var mn = ResolveMethodName(mr, tok);
                Console.WriteLine($"  [{i:X3}] {(op==0x28?"call":"callvirt")} {mn} (0x{tok:X8})");
                i += 5; continue;
            }
            if ((op == 0x7B || op == 0x7D) && i+4 < il.Length)
            {
                int tok = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                var fn = ResolveFieldToken(mr, tok);
                Console.WriteLine($"  [{i:X3}] {(op==0x7B?"ldfld":"stfld")} {fn} (0x{tok:X8})");
                i += 5; continue;
            }
            if (op == 0x14) { Console.WriteLine($"  [{i:X3}] ldnull"); i++; continue; }
            if (op == 0x2A) { Console.WriteLine($"  [{i:X3}] ret"); i++; continue; }
            Console.WriteLine($"  [{i:X3}] 0x{op:X2}");
            i++;
        }
    }
    return;
}

// --- 調査: Ancient 関連クラスを検索 ---
if (args.Length > 0 && args[0] == "--investigate-ancients")
{
    var ancientNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Tanx", "Orobas", "Darv", "Neow", "Pael", "Nonupeipe", "Tezcatara", "TheArchitect", "Vakuu",
          "Ancient" };
    foreach (var typeHandle in mr.TypeDefinitions)
    {
        var typeDef = mr.GetTypeDefinition(typeHandle);
        var name = mr.GetString(typeDef.Name);
        var ns = mr.GetString(typeDef.Namespace);
        bool match = ancientNames.Any(a => name.Contains(a, StringComparison.OrdinalIgnoreCase));
        if (!match) continue;
        Console.WriteLine($"CLASS: {ns}.{name}");
        foreach (var mh in typeDef.GetMethods())
        {
            var method = mr.GetMethodDefinition(mh);
            Console.WriteLine($"  method: {mr.GetString(method.Name)}");
        }
    }
    return;
}

// --- 調査: MethodSpec の型引数を解決 ---
if (args.Length > 0 && args[0] == "--resolve-methodspec")
{
    int token = Convert.ToInt32(args[1], 16);
    var h = MetadataTokens.EntityHandle(token);
    if (h.Kind == HandleKind.MethodSpecification)
    {
        var spec = mr.GetMethodSpecification((MethodSpecificationHandle)h);
        var sig = mr.GetBlobReader(spec.Signature);
        sig.ReadByte(); // 0x0A generic
        int count = sig.ReadCompressedInteger();
        Console.Write($"MethodSpec 0x{token:X8} generic args ({count}): ");
        for (int i = 0; i < count; i++)
        {
            byte code = sig.ReadByte();
            if (code is 0x11 or 0x12) // VALUETYPE or CLASS
            {
                int typeToken = sig.ReadCompressedInteger();
                int tag = typeToken & 3;
                int row = typeToken >> 2;
                string typeName = tag switch
                {
                    0 => mr.GetString(mr.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(row)).Name),
                    1 => mr.GetString(mr.GetTypeReference(MetadataTokens.TypeReferenceHandle(row)).Name),
                    _ => $"TypeSpec(tag={tag})"
                };
                Console.Write(typeName + " ");
            }
        }
        Console.WriteLine();
        // Also resolve the method name
        var methodHandle = spec.Method;
        if (methodHandle.Kind == HandleKind.MemberReference)
        {
            var memberRef = mr.GetMemberReference((MemberReferenceHandle)methodHandle);
            Console.WriteLine($"  Method name: {mr.GetString(memberRef.Name)}");
        }
    }
    return;
}

// --- 調査: RelicPool の GenerateAllRelics から relic クラス名を列挙 ---
if (args.Length > 0 && args[0] == "--dump-relic-classes")
{
    foreach (var typeHandle in mr.TypeDefinitions)
    {
        var typeDef = mr.GetTypeDefinition(typeHandle);
        var poolName = mr.GetString(typeDef.Name);
        if (!poolName.EndsWith("RelicPool")) continue;
        Console.Error.WriteLine($"=== {poolName} ===");
        foreach (var mh in typeDef.GetMethods())
        {
            var method = mr.GetMethodDefinition(mh);
            if (mr.GetString(method.Name) != "GenerateAllRelics") continue;
            if (method.RelativeVirtualAddress == 0) continue;
            var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
            if (body == null) continue;
            var il = body.GetILBytes();
            for (int i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] != 0x28 && il[i] != 0x6F) continue;
                int token = il[i+1] | (il[i+2]<<8) | (il[i+3]<<16) | (il[i+4]<<24);
                var typeName = ResolveMethodSpecFirstArg(mr, token);
                if (!string.IsNullOrEmpty(typeName))
                    Console.WriteLine($"{typeName}  →  {CamelToUpperSnake(typeName)}");
            }
        }
    }
    return;
}

// --- 調査: CardPool クラス名一覧を表示 ---
if (args.Length > 0 && args[0] == "--list-cardpools")
{
    foreach (var typeHandle in mr.TypeDefinitions)
    {
        var typeDef = mr.GetTypeDefinition(typeHandle);
        var name = mr.GetString(typeDef.Name);
        if (name.EndsWith("CardPool") || name.Contains("CardPool"))
            Console.WriteLine($"{mr.GetString(typeDef.Namespace)}.{name}");
    }
    return;
}

// --- 調査: カードクラスの名前空間一覧を表示 ---
if (args.Length > 0 && args[0] == "--dump-namespaces")
{
    // GenerateAllCards から cardClasses を収集
    var cardClassesNs = new HashSet<string>(StringComparer.Ordinal);
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
                    cardClassesNs.Add(typeName);
            }
        }
    }
    // 各カードクラスの名前空間を表示
    foreach (var typeHandle in mr.TypeDefinitions)
    {
        var typeDef = mr.GetTypeDefinition(typeHandle);
        var className = mr.GetString(typeDef.Name);
        if (!cardClassesNs.Contains(className)) continue;
        var ns = mr.GetString(typeDef.Namespace);
        Console.WriteLine($"{className}\t{ns}");
    }
    return;
}

// --- 調査: 特定クラスの全メソッド IL をダンプ ---
if (args.Length > 0 && args[0] == "--dump-class")
{
    var targetClass = args.Length > 1 ? args[1] : "Bash";
    foreach (var typeHandle in mr.TypeDefinitions)
    {
        var typeDef = mr.GetTypeDefinition(typeHandle);
        if (mr.GetString(typeDef.Name) != targetClass) continue;
        foreach (var mh in typeDef.GetMethods())
        {
            var method = mr.GetMethodDefinition(mh);
            var methodName = mr.GetString(method.Name);
            if (method.RelativeVirtualAddress == 0) { Console.WriteLine($"[{methodName}] (abstract/extern)"); continue; }
            var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
            if (body == null) { Console.WriteLine($"[{methodName}] (no body)"); continue; }
            var il = body.GetILBytes();
            Console.WriteLine($"[{methodName}] ({il.Length} bytes)");
            for (int i = 0; i < il.Length;)
            {
                byte op = il[i];
                var (val, size) = ReadLdcI4(il, i);
                if (val.HasValue) { Console.WriteLine($"  [{i:X3}] ldc.i4 {val}"); i += size; continue; }
                if (op == 0x02) { Console.WriteLine($"  [{i:X3}] ldarg.0"); i++; continue; }
                if (op == 0x03) { Console.WriteLine($"  [{i:X3}] ldarg.1"); i++; continue; }
                if (op == 0x04) { Console.WriteLine($"  [{i:X3}] ldarg.2"); i++; continue; }
                if (op == 0x05) { Console.WriteLine($"  [{i:X3}] ldarg.3"); i++; continue; }
                if ((op == 0x28 || op == 0x6F || op == 0x73) && i+4 < il.Length)
                {
                    int tok = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                    var mn = ResolveMethodName(mr, tok);
                    var opName = op == 0x28 ? "call" : op == 0x6F ? "callvirt" : "newobj";
                    Console.WriteLine($"  [{i:X3}] {opName} {mn} (0x{tok:X8})");
                    i += 5; continue;
                }
                if ((op == 0x7B || op == 0x7D) && i+4 < il.Length)
                {
                    int tok = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                    var fn = ResolveFieldToken(mr, tok);
                    Console.WriteLine($"  [{i:X3}] {(op==0x7B?"ldfld":"stfld")} {fn} (0x{tok:X8})");
                    i += 5; continue;
                }
                if (op == 0x14) { Console.WriteLine($"  [{i:X3}] ldnull"); i++; continue; }
                if (op == 0x2A) { Console.WriteLine($"  [{i:X3}] ret"); i++; continue; }
                if (op == 0x25) { Console.WriteLine($"  [{i:X3}] dup"); i++; continue; }
                if (op == 0x26) { Console.WriteLine($"  [{i:X3}] pop"); i++; continue; }
                Console.WriteLine($"  [{i:X3}] 0x{op:X2}");
                i++;
            }
        }
        break;
    }
    return;
}

// --- 調査: 特定カードの .ctor IL を全命令ダンプ ---
if (args.Length > 0 && args[0] == "--dump-ctor")
{
    var targetClass = args.Length > 1 ? args[1] : "Bash";
    foreach (var typeHandle in mr.TypeDefinitions)
    {
        var typeDef = mr.GetTypeDefinition(typeHandle);
        if (mr.GetString(typeDef.Name) != targetClass) continue;
        foreach (var mh in typeDef.GetMethods())
        {
            var method = mr.GetMethodDefinition(mh);
            if (mr.GetString(method.Name) != ".ctor") continue;
            if (method.RelativeVirtualAddress == 0) continue;
            var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
            if (body == null) continue;
            var il = body.GetILBytes();
            Console.Error.WriteLine($".ctor IL for {targetClass} ({il.Length} bytes):");
            for (int i = 0; i < il.Length;)
            {
                byte op = il[i];
                var (val, size) = ReadLdcI4(il, i);
                if (val.HasValue)
                    { Console.WriteLine($"  [{i:X3}] ldc.i4 {val}"); i += size; continue; }
                if (op == 0x02) { Console.WriteLine($"  [{i:X3}] ldarg.0"); i++; continue; }
                if ((op == 0x28 || op == 0x6F) && i+4 < il.Length)
                {
                    int tok = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                    var mn = ResolveMethodName(mr, tok);
                    Console.WriteLine($"  [{i:X3}] {(op==0x28?"call":"callvirt")} {mn} (0x{tok:X8})");
                    i += 5; continue;
                }
                if (op == 0x7D && i+4 < il.Length)
                {
                    int tok = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                    var fn = ResolveFieldToken(mr, tok);
                    Console.WriteLine($"  [{i:X3}] stfld {fn} (0x{tok:X8})");
                    i += 5; continue;
                }
                Console.WriteLine($"  [{i:X3}] 0x{op:X2}");
                i++;
            }
            break;
        }
        break;
    }
    return;
}

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

// Rarity enum 値マップ（Common/Uncommon/Rare 等のフィールドを持つ enum を検索）
var rarityByInt = new Dictionary<int, string>();
foreach (var typeHandle in mr.TypeDefinitions)
{
    var typeDef = mr.GetTypeDefinition(typeHandle);
    var fields = typeDef.GetFields().ToList();
    // value__ を除くフィールド名に Common/Uncommon/Rare が含まれる enum を探す
    var fieldNames = fields
        .Select(fh => mr.GetString(mr.GetFieldDefinition(fh).Name))
        .Where(fn => fn != "value__")
        .ToList();
    if (!fieldNames.Any(fn => fn is "Common" or "Uncommon" or "Rare")) continue;
    foreach (var fh in fields)
    {
        var field = mr.GetFieldDefinition(fh);
        var fn = mr.GetString(field.Name);
        if (fn == "value__") continue;
        var ch = field.GetDefaultValue();
        if (ch.IsNil) continue;
        var br = mr.GetBlobReader(mr.GetConstant(ch).Value);
        rarityByInt[br.ReadInt32()] = fn;
    }
    Console.Error.WriteLine($"Rarity enum '{mr.GetString(typeDef.Name)}': {string.Join(", ", rarityByInt.Select(kv => $"{kv.Key}={kv.Value}"))}");
    break;
}

// CardModel .ctor: どの引数位置が CardType か確認済み → param[2] = type
// Alchemize の例: ldarg.0, ldc.i4(cost), ldc.i4(type), ldc.i4(rarity), ...
// つまり IL の ldc.i4 系が 2 番目に押し込まれる値が CardType

// キャラクター名マップ: CardPool クラス名 → キャラクター名
var characterPoolMap = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["IroncladCardPool"]   = "Ironclad",
    ["SilentCardPool"]     = "Silent",
    ["DefectCardPool"]     = "Defect",
    ["NecrobinderCardPool"] = "Necrobinder",
    ["RegentCardPool"]     = "Regent",
};

// カードクラス名の Set を CardPool.GenerateAllCards IL から取得
// また、キャラクター別の Set も収集する
var cardClasses = new HashSet<string>(StringComparer.Ordinal);
var cardCharacters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);  // CARD.XXX → character
foreach (var typeHandle in mr.TypeDefinitions)
{
    var typeDef = mr.GetTypeDefinition(typeHandle);
    var poolName = mr.GetString(typeDef.Name);
    if (!poolName.EndsWith("CardPool")) continue;
    characterPoolMap.TryGetValue(poolName, out var character);

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
            if (string.IsNullOrEmpty(typeName)) continue;
            cardClasses.Add(typeName);
            if (character != null)
                cardCharacters.TryAdd("CARD." + CamelToUpperSnake(typeName), character);
        }
    }
}

// 各カードクラスの .ctor IL から CardType (2番目の ldc.i4) とコスト (1番目)、
// およびフィールド代入 (ldarg.0, ldc.i4 N, stfld F) を取得
var results       = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var costs         = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var upgradedCosts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var rarities      = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var cardStats     = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
var starCostCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var cardKeywords  = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

// get_CanonicalKeywords が返す int → キーワード名 のマッピング
// (DLLのenum値を実験的に特定)
var keywordNameById = new Dictionary<int, string>
{
    { 1, "EXHAUST"    },
    { 2, "ETHEREAL"   },
    { 3, "INNATE"     },
    { 4, "UNPLAYABLE" },
    { 5, "RETAIN"     },
    { 6, "ETERNAL"    },
    { 7, "SLY"        },
};

// コンストラクタトークン → DynamicVar 名 のマップを構築
// フェーズA: 具体 *Var クラス（ldstr で名前を持つもの）
var varNameByCtorToken = new Dictionary<int, string>();
foreach (var typeHandle in mr.TypeDefinitions)
{
    var typeDef = mr.GetTypeDefinition(typeHandle);
    if (!mr.GetString(typeDef.Name).EndsWith("Var")) continue;
    string? varName = null;
    foreach (var mhv in typeDef.GetMethods())
    {
        var mv = mr.GetMethodDefinition(mhv);
        if (mr.GetString(mv.Name) != ".ctor" || mv.RelativeVirtualAddress == 0) continue;
        var bv = peReader.GetMethodBody(mv.RelativeVirtualAddress);
        if (bv == null) continue;
        var iv = bv.GetILBytes();
        for (int i = 0; i + 4 < iv.Length; i++)
        {
            if (iv[i] != 0x72) continue;
            int strTok = iv[i+1]|(iv[i+2]<<8)|(iv[i+3]<<16)|(iv[i+4]<<24);
            try { var s = mr.GetUserString(MetadataTokens.UserStringHandle(strTok & 0xFFFFFF)); if (!string.IsNullOrEmpty(s)) { varName = s; break; } } catch { }
        }
        if (varName != null) break;
    }
    if (varName == null) continue;
    foreach (var mhv in typeDef.GetMethods())
    {
        var mv = mr.GetMethodDefinition(mhv);
        if (mr.GetString(mv.Name) != ".ctor") continue;
        varNameByCtorToken.TryAdd(MetadataTokens.GetToken(mr, mhv), varName);
    }
}
// フェーズB: TypeSpec/TypeRef Var — DynamicVarSet の castclass トークンから逆引き
// TypeSpec: ジェネリック型パラメータ付きの Var（Vulnerable 等）
// TypeRef: 外部アセンブリに定義された Var（VigorPower 等）— Akabeko で使用
{
    var typeTokToPropName = new Dictionary<int, string>();
    foreach (var th in mr.TypeDefinitions)
    {
        var td = mr.GetTypeDefinition(th);
        if (mr.GetString(td.Name) != "DynamicVarSet") continue;
        foreach (var mhv in td.GetMethods())
        {
            var mv = mr.GetMethodDefinition(mhv);
            var mn = mr.GetString(mv.Name);
            if (!mn.StartsWith("get_") || mv.RelativeVirtualAddress == 0) continue;
            var bv = peReader.GetMethodBody(mv.RelativeVirtualAddress);
            if (bv == null) continue;
            var iv = bv.GetILBytes();
            for (int i = 0; i + 4 < iv.Length; i++)
            {
                if (iv[i] != 0x74) continue; // castclass
                int castTok = iv[i+1]|(iv[i+2]<<8)|(iv[i+3]<<16)|(iv[i+4]<<24);
                var kind = MetadataTokens.EntityHandle(castTok).Kind;
                if (kind is HandleKind.TypeSpecification or HandleKind.TypeReference)
                    typeTokToPropName.TryAdd(castTok, mn[4..]);
                break;
            }
        }
        break;
    }
    foreach (var mrh in mr.MemberReferences)
    {
        var memberRef = mr.GetMemberReference(mrh);
        if (mr.GetString(memberRef.Name) != ".ctor") continue;
        var parentKind = memberRef.Parent.Kind;
        if (parentKind is not (HandleKind.TypeSpecification or HandleKind.TypeReference)) continue;
        int parentTok = MetadataTokens.GetToken(memberRef.Parent);
        if (!typeTokToPropName.TryGetValue(parentTok, out var propName)) continue;
        varNameByCtorToken.TryAdd(MetadataTokens.GetToken(mr, mrh), propName);
    }
}
// フェーズC: TypeSpec 親の MemberRef .ctor で DynamicVarSet に未登録のものを TypeSpec blob デコードで解決
// TypeSpec blob: GENERICINST (0x15) + CLASS/VALUETYPE + base-type-token + arg-count + arg-type-token
// 引数型が TypeRef 名 "XxxVar" なら propName = "Xxx" として登録
{
    foreach (var mrh in mr.MemberReferences)
    {
        var memberRef = mr.GetMemberReference(mrh);
        if (mr.GetString(memberRef.Name) != ".ctor") continue;
        if (memberRef.Parent.Kind != HandleKind.TypeSpecification) continue;
        // 既に登録済みはスキップ
        int ctorTok = MetadataTokens.GetToken(mr, mrh);
        if (varNameByCtorToken.ContainsKey(ctorTok)) continue;

        try
        {
            var typeSpec = mr.GetTypeSpecification((TypeSpecificationHandle)memberRef.Parent);
            var sig = mr.GetBlobReader(typeSpec.Signature);
            if (sig.ReadByte() != 0x15) continue; // GENERICINST
            sig.ReadByte(); // CLASS or VALUETYPE
            sig.ReadCompressedInteger(); // base type token (encoded)
            int argCount = sig.ReadCompressedInteger();
            if (argCount < 1) continue;
            // 最初の型引数を読む
            byte argTypeCode = sig.ReadByte();
            if (argTypeCode is not (0x11 or 0x12)) continue; // CLASS or VALUETYPE
            int argTypeToken = sig.ReadCompressedInteger();
            int tag = argTypeToken & 3;
            int row = argTypeToken >> 2;
            string argTypeName = tag switch
            {
                0 => mr.GetString(mr.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(row)).Name),
                1 => mr.GetString(mr.GetTypeReference(MetadataTokens.TypeReferenceHandle(row)).Name),
                _ => ""
            };
            if (string.IsNullOrEmpty(argTypeName)) continue;
            // 型引数名をそのまま変数名として使う（"VigorPower", "Poison" 等）
            varNameByCtorToken.TryAdd(ctorTok, argTypeName);
        }
        catch { /* blob デコード失敗はスキップ */ }
    }
}

foreach (var typeHandle in mr.TypeDefinitions)
{
    var typeDef = mr.GetTypeDefinition(typeHandle);
    var className = mr.GetString(typeDef.Name);
    if (!cardClasses.Contains(className)) continue;

    var cardId = "CARD." + CamelToUpperSnake(className);

    // パス1+2: .ctor からコスト・タイプ・stfld を取得
    foreach (var mh in typeDef.GetMethods())
    {
        var method = mr.GetMethodDefinition(mh);
        if (mr.GetString(method.Name) != ".ctor") continue;
        if (method.RelativeVirtualAddress == 0) continue;
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        if (body == null) continue;
        var il = body.GetILBytes();

        // パス1: ldc.i4 系の値を順番に収集（コスト・カード種別）
        var intArgs = new List<int>();
        for (int i = 0; i < il.Length; )
        {
            var (val, size) = ReadLdcI4(il, i);
            if (val.HasValue)
                intArgs.Add(val.Value);
            i += size;
        }

        if (intArgs.Count >= 2)
        {
            costs.TryAdd(cardId, intArgs[0]);
            if (cardTypeByInt.TryGetValue(intArgs[1], out var typeName))
                results.TryAdd(cardId, typeName);
            if (intArgs.Count >= 3 && rarityByInt.TryGetValue(intArgs[2], out var rarityName))
                rarities.TryAdd(cardId, rarityName);
        }

        // パス2: フィールド代入 (stfld) とプロパティセッター (call/callvirt set_Xxx) を収集
        int? lastInt = null;
        var fields = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < il.Length;)
        {
            byte op = il[i];
            if ((op == 0x7D || op == 0x28 || op == 0x6F) && i + 4 < il.Length)
            {
                if (lastInt.HasValue)
                {
                    int token = il[i+1] | (il[i+2]<<8) | (il[i+3]<<16) | (il[i+4]<<24);
                    string? name = null;
                    if (op == 0x7D)
                        name = ResolveFieldToken(mr, token);
                    else
                    {
                        var rawName = ResolveMethodName(mr, token);
                        if (rawName.StartsWith("set_", StringComparison.Ordinal))
                            name = rawName[4..];
                    }
                    if (!string.IsNullOrEmpty(name))
                        fields.TryAdd(name, lastInt.Value);
                }
                lastInt = null;
                i += 5;
                continue;
            }
            var (val, size) = ReadLdcI4(il, i);
            if (val.HasValue) lastInt = val;
            else if (op != 0x02) lastInt = null;
            i += size;
        }
        if (fields.Count > 0)
            cardStats[cardId] = fields;

        break;
    }

    // パス3: get_CanonicalVars から DynamicVar 値を取得
    // パターン: ldc.i4 N → newobj Decimal..ctor → newobj XxxVar..ctor
    // レリック用パスR2 と同じ緩いロジック: 「Var newobj 直前の最後の ldc.i4」を採用する
    // （Decimal トークン不問。Damage/Block/Magic 等の base 値を取りこぼさないため）。
    foreach (var mh in typeDef.GetMethods())
    {
        var method = mr.GetMethodDefinition(mh);
        if (mr.GetString(method.Name) != "get_CanonicalVars") continue;
        if (method.RelativeVirtualAddress == 0) continue;
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        if (body == null) continue;
        var il = body.GetILBytes();

        int? pendingInt = null;
        var canonFields = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < il.Length; )
        {
            byte op = il[i];
            if (op == 0x73 && i + 4 < il.Length) // newobj
            {
                int tok = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                if (pendingInt.HasValue && varNameByCtorToken.TryGetValue(tok, out var vname))
                {
                    canonFields.TryAdd(vname, pendingInt.Value);
                    pendingInt = null;
                }
                // 非Var ctor（Decimal 等）では pendingInt を維持して次の Var ctor に引き継ぐ
                i += 5;
                continue;
            }
            var (val, size) = ReadLdcI4(il, i);
            if (val.HasValue) pendingInt = val; // ldc.i4 を見るたびに更新（最後の値を保持）
            i += size;
        }
        if (canonFields.Count > 0)
        {
            if (!cardStats.ContainsKey(cardId))
                cardStats[cardId] = canonFields;
            else
                foreach (var (k, v) in canonFields)
                    cardStats[cardId].TryAdd(k, v);
        }
        break;
    }

    // パス4: Has*CostX / HasStarCost / CanonicalStarCost が true/正値を返す場合の処理
    // HasEnergyCostX / HasStarCostX → cost=-1、HasStarCost(X) / CanonicalStarCost(>0) → starCostCards に追加
    foreach (var mh in typeDef.GetMethods())
    {
        var method = mr.GetMethodDefinition(mh);
        var mn = mr.GetString(method.Name);
        bool isXCost          = mn.StartsWith("get_Has") && mn.EndsWith("CostX");
        bool isFixedStar      = mn == "get_HasStarCost";
        bool isCanonicalStar  = mn == "get_CanonicalStarCost";
        if (!isXCost && !isFixedStar && !isCanonicalStar) continue;
        if (method.RelativeVirtualAddress == 0) continue;
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        if (body == null) continue;
        var il = body.GetILBytes();
        if (isCanonicalStar)
        {
            // 正の整数を返す = 固定スターコスト（1★, 2★, 3★ 等）
            var (val, _) = ReadLdcI4(il, 0);
            if (val.HasValue && val > 0)
                starCostCards.Add(cardId);
        }
        else
        {
            // ldc.i4.1 (0x17) + ret (0x2A) = 常に true を返す
            if (il.Length == 2 && il[0] == 0x17 && il[1] == 0x2A)
            {
                if (isXCost) costs[cardId] = -1;
                if (mn.Contains("Star")) starCostCards.Add(cardId);
            }
        }
    }

    // パス5: OnUpgrade から UpgradedXxx 値を取得
    // パターン: callvirt get_Xxx → ldc.i4 N → newobj Decimal.ctor → callvirt UpgradeValueBy/UpgradeValueTo
    // UpgradeValueBy → UpgradedXxx = base + N
    // UpgradeValueTo → UpgradedXxx = N（絶対値）
    // ldsfld 経由の delta（非リテラル）は解決不能のためスキップ
    foreach (var mh in typeDef.GetMethods())
    {
        var method = mr.GetMethodDefinition(mh);
        if (mr.GetString(method.Name) != "OnUpgrade") continue;
        if (method.RelativeVirtualAddress == 0) break;
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        if (body == null) break;
        var il = body.GetILBytes();

        string? upgVar = null;
        int? pendingAmount = null;

        for (int i = 0; i < il.Length; )
        {
            byte op = il[i];

            // call / callvirt
            if ((op == 0x28 || op == 0x6F) && i + 4 < il.Length)
            {
                int tok = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                var name = ResolveMethodName(mr, tok);
                if (name.StartsWith("get_", StringComparison.Ordinal) && name != "get_DynamicVars")
                {
                    // 対象 DynamicVar の切り替え
                    upgVar = name[4..];
                    pendingAmount = null;
                }
                else if ((name == "UpgradeValueBy" || name == "UpgradeValueTo") &&
                         upgVar != null && pendingAmount.HasValue)
                {
                    if (!cardStats.TryGetValue(cardId, out var sd))
                        cardStats[cardId] = sd = new Dictionary<string, int>();
                    int baseVal = 0;
                    foreach (var (k, v) in sd)
                        if (string.Equals(k.TrimStart('_'), upgVar, StringComparison.OrdinalIgnoreCase))
                        { baseVal = v; break; }
                    var upgKey = "Upgraded" + upgVar;
                    var upgVal = name == "UpgradeValueBy" ? baseVal + pendingAmount.Value : pendingAmount.Value;
                    sd.TryAdd(upgKey, upgVal);
                    upgVar = null;
                    pendingAmount = null;
                }
                else if ((name == "UpgradeBy" || name == "UpgradeTo") &&
                         upgVar == "EnergyCost" && pendingAmount.HasValue)
                {
                    // コスト（EnergyCost）のアップグレード変動。base は .ctor 由来（X コスト=-1 は除外）。
                    if (costs.TryGetValue(cardId, out var baseCost) && baseCost >= 0)
                    {
                        var upg = name == "UpgradeBy" ? baseCost + pendingAmount.Value : pendingAmount.Value;
                        if (upg >= 0 && upg != baseCost)
                            upgradedCosts[cardId] = upg;
                    }
                    upgVar = null;
                    pendingAmount = null;
                }
                else
                {
                    pendingAmount = null;
                }
                i += 5;
                continue;
            }

            // newobj: Decimal.ctor(int) などは pendingAmount を維持してスキップ
            if (op == 0x73 && i + 4 < il.Length) { i += 5; continue; }

            var (val, size) = ReadLdcI4(il, i);
            if (val.HasValue)
                pendingAmount = val;
            else if (op is not (0x02 or 0x25)) // ldarg.0, dup は中立
                pendingAmount = null;
            i += size;
        }
        break;
    }

    // パス6: get_CanonicalKeywords から int[] を取得してキーワード名リストに変換
    foreach (var mh in typeDef.GetMethods())
    {
        var method = mr.GetMethodDefinition(mh);
        if (mr.GetString(method.Name) != "get_CanonicalKeywords") continue;
        if (method.RelativeVirtualAddress == 0) break;
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        if (body == null) break;
        var il = body.GetILBytes();

        var kwValues = ParseCanonicalKeywords(il);
        if (kwValues.Count > 0)
        {
            var kwNames = kwValues
                .Select(v => keywordNameById.TryGetValue(v, out var n) ? n : null)
                .Where(n => n != null)
                .Select(n => n!)
                .ToList();
            if (kwNames.Count > 0)
                cardKeywords[cardId] = kwNames;
        }
        break;
    }
}

// ── Relic 抽出 ──────────────────────────────────────────────────────────────

// *RelicPool の GenerateAllRelics() から relic クラス名を収集
var relicClasses = new HashSet<string>(StringComparer.Ordinal);
foreach (var typeHandle in mr.TypeDefinitions)
{
    var typeDef = mr.GetTypeDefinition(typeHandle);
    if (!mr.GetString(typeDef.Name).EndsWith("RelicPool")) continue;
    foreach (var mh in typeDef.GetMethods())
    {
        var method = mr.GetMethodDefinition(mh);
        if (mr.GetString(method.Name) != "GenerateAllRelics") continue;
        if (method.RelativeVirtualAddress == 0) continue;
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        if (body == null) continue;
        var il = body.GetILBytes();
        for (int i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != 0x28 && il[i] != 0x6F) continue;
            int token = il[i+1] | (il[i+2]<<8) | (il[i+3]<<16) | (il[i+4]<<24);
            var typeName = ResolveMethodSpecFirstArg(mr, token);
            if (!string.IsNullOrEmpty(typeName) && !typeName.Contains('<'))
                relicClasses.Add(typeName);
        }
    }
}
Console.Error.WriteLine($"Found {relicClasses.Count} relic classes.");

// 各 relic クラスから get_Rarity と get_CanonicalVars を抽出
var relicRarities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var relicStats    = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
int dbgCanonVars = 0;
int dbgCtorFields = 0;

foreach (var typeHandle in mr.TypeDefinitions)
{
    var typeDef   = mr.GetTypeDefinition(typeHandle);
    var className = mr.GetString(typeDef.Name);
    if (!relicClasses.Contains(className)) continue;

    var relicId = CamelToUpperSnake(className);

    // get_Rarity → ldc.i4 N + ret
    foreach (var mh in typeDef.GetMethods())
    {
        var method = mr.GetMethodDefinition(mh);
        if (mr.GetString(method.Name) != "get_Rarity") continue;
        if (method.RelativeVirtualAddress == 0) continue;
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        if (body == null) continue;
        var il = body.GetILBytes();
        var (val, _) = ReadLdcI4(il, 0);
        if (val.HasValue && rarityByInt.TryGetValue(val.Value, out var rarityName))
            relicRarities[relicId] = rarityName;
        break;
    }

    // パスR1: .ctor からフィールド代入 (stfld) とプロパティセッター (call set_Xxx) を取得
    // カードの "パス2" と同じアプローチ。{MaxHp} などの変数名はフィールド/プロパティ名と一致する想定。
    foreach (var mh in typeDef.GetMethods())
    {
        var method = mr.GetMethodDefinition(mh);
        if (mr.GetString(method.Name) != ".ctor") continue;
        if (method.RelativeVirtualAddress == 0) continue;
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        if (body == null) continue;
        var il = body.GetILBytes();

        int? lastInt = null;
        var ctorFields = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < il.Length;)
        {
            byte op = il[i];
            if ((op == 0x7D || op == 0x28 || op == 0x6F) && i + 4 < il.Length)
            {
                if (lastInt.HasValue)
                {
                    int token = il[i+1] | (il[i+2]<<8) | (il[i+3]<<16) | (il[i+4]<<24);
                    string? name = null;
                    if (op == 0x7D)
                        name = ResolveFieldToken(mr, token);
                    else
                    {
                        var rawName = ResolveMethodName(mr, token);
                        if (rawName.StartsWith("set_", StringComparison.Ordinal))
                            name = rawName[4..];
                    }
                    if (!string.IsNullOrEmpty(name))
                        ctorFields.TryAdd(name, lastInt.Value);
                }
                lastInt = null;
                i += 5;
                continue;
            }
            var (val2, size2) = ReadLdcI4(il, i);
            if (val2.HasValue) lastInt = val2;
            else if (op != 0x02) lastInt = null;
            i += size2;
        }
        if (ctorFields.Count > 0)
        {
            relicStats[relicId] = ctorFields;
            dbgCtorFields++;
        }
        break;
    }

    // パスR2: get_CanonicalVars → 「最後の ldc.i4 N」を次の newobj XxxVar の値として使う
    // カードは ldc.i4 N → newobj Decimal(0x1317) → newobj XxxVar だが、
    // レリックは別のDecimalトークン(0x134D等)や Decimal なしの直接 int を使う場合がある。
    // いずれも「Var コンストラクタ直前の最後の ldc.i4」が実値なので、柔軟パターンで対応。
    foreach (var mh in typeDef.GetMethods())
    {
        var method = mr.GetMethodDefinition(mh);
        if (mr.GetString(method.Name) != "get_CanonicalVars") continue;
        if (method.RelativeVirtualAddress == 0) continue;
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        if (body == null) continue;
        var il = body.GetILBytes();

        int? pendingValR = null;
        var canonFields = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < il.Length; )
        {
            byte op = il[i];
            if (op == 0x73 && i + 4 < il.Length) // newobj
            {
                int tok = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                if (pendingValR.HasValue && varNameByCtorToken.TryGetValue(tok, out var vname))
                {
                    canonFields.TryAdd(vname, pendingValR.Value);
                    pendingValR = null;
                }
                // 非Var ctor (Decimal等) では pendingValR を維持して次の Var ctor に引き継ぐ
                i += 5;
                continue;
            }
            var (valR, sizeR) = ReadLdcI4(il, i);
            if (valR.HasValue) pendingValR = valR; // ldc.i4 を見るたびに更新（最後の値を保持）
            i += sizeR;
        }
        if (canonFields.Count > 0)
        {
            if (!relicStats.ContainsKey(relicId))
                relicStats[relicId] = canonFields;
            else
                foreach (var (k, v) in canonFields)
                    relicStats[relicId].TryAdd(k, v);
            dbgCanonVars++;
        }
        break;
    }
}
Console.Error.WriteLine($"Relics: ctor-fields={dbgCtorFields}, canonicalVars={dbgCanonVars}, total-with-stats={relicStats.Count}");

// relic_rarities.json 出力 → StS2Shared/Resources/
var relicRaritiesOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "relic_rarities.json");
Console.Error.WriteLine($"Extracted {relicRarities.Count} relic rarity mappings.");
var relicRarityLines = relicRarities.OrderBy(kv => kv.Key)
    .Select(kv => $"  \"{kv.Key}\": \"{kv.Value}\"");
WriteJson(relicRaritiesOutPath, "{\n" + string.Join(",\n", relicRarityLines) + "\n}\n");
Console.WriteLine(relicRaritiesOutPath);

// relic_stats.json 出力 → StS2Shared/Resources/
var relicStatsOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "relic_stats.json");
Console.Error.WriteLine($"Extracted {relicStats.Count} relic stat mappings.");
var relicStatsEntries = relicStats.OrderBy(kv => kv.Key).Select(kv =>
{
    var fieldPairs = kv.Value.OrderBy(f => f.Key)
        .Select(f => $"    \"{f.Key}\": {f.Value}");
    return $"  \"{kv.Key}\": {{\n{string.Join(",\n", fieldPairs)}\n  }}";
});
WriteJson(relicStatsOutPath, "{\n" + string.Join(",\n", relicStatsEntries) + "\n}\n");
Console.WriteLine(relicStatsOutPath);

// card_characters.json 出力
var charsOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "card_characters.json");
Console.Error.WriteLine($"Extracted {cardCharacters.Count} card character mappings.");
var charLines = cardCharacters.OrderBy(kv => kv.Key)
    .Select(kv => $"  \"{kv.Key}\": \"{kv.Value}\"");
WriteJson(charsOutPath, "{\n" + string.Join(",\n", charLines) + "\n}\n");
Console.WriteLine(charsOutPath);

// card_types.json 出力
Console.Error.WriteLine($"Extracted {results.Count} card type mappings.");
var jsonLines = results.OrderBy(kv => kv.Key)
    .Select(kv => $"  \"{kv.Key}\": \"{kv.Value}\"");
WriteJson(outPath, "{\n" + string.Join(",\n", jsonLines) + "\n}\n");
Console.WriteLine(outPath);

// card_costs.json 出力
var costsOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "card_costs.json");
Console.Error.WriteLine($"Extracted {costs.Count} card cost mappings.");
var costLines = costs.OrderBy(kv => kv.Key)
    .Select(kv => $"  \"{kv.Key}\": {kv.Value}");
WriteJson(costsOutPath, "{\n" + string.Join(",\n", costLines) + "\n}\n");
Console.WriteLine(costsOutPath);

// card_upgraded_costs.json 出力（アップグレードでコストが変わるカードのみ）
var upgCostsOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "card_upgraded_costs.json");
Console.Error.WriteLine($"Extracted {upgradedCosts.Count} upgraded card cost mappings.");
var upgCostLines = upgradedCosts.OrderBy(kv => kv.Key)
    .Select(kv => $"  \"{kv.Key}\": {kv.Value}");
WriteJson(upgCostsOutPath, "{\n" + string.Join(",\n", upgCostLines) + "\n}\n");
Console.WriteLine(upgCostsOutPath);

// card_rarities.json 出力
var raritiesOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "card_rarities.json");
Console.Error.WriteLine($"Extracted {rarities.Count} card rarity mappings.");
var rarityLines = rarities.OrderBy(kv => kv.Key)
    .Select(kv => $"  \"{kv.Key}\": \"{kv.Value}\"");
WriteJson(raritiesOutPath, "{\n" + string.Join(",\n", rarityLines) + "\n}\n");
Console.WriteLine(raritiesOutPath);

// card_stats.json 出力
var statsOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "card_stats.json");
Console.Error.WriteLine($"Extracted {cardStats.Count} card stat mappings.");
var statsEntries = cardStats.OrderBy(kv => kv.Key).Select(kv =>
{
    var fieldPairs = kv.Value.OrderBy(f => f.Key)
        .Select(f => $"    \"{f.Key}\": {f.Value}");
    return $"  \"{kv.Key}\": {{\n{string.Join(",\n", fieldPairs)}\n  }}";
});
WriteJson(statsOutPath, "{\n" + string.Join(",\n", statsEntries) + "\n}\n");
Console.WriteLine(statsOutPath);

// card_star_costs.json 出力 (Starをコストとして消費するカードの ID リスト)
var starCostsOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "card_star_costs.json");
Console.Error.WriteLine($"Extracted {starCostCards.Count} star-cost card mappings.");
var starCostLines = starCostCards.OrderBy(id => id).Select(id => $"  \"{id}\"");
WriteJson(starCostsOutPath, "[\n" + string.Join(",\n", starCostLines) + "\n]\n");
Console.WriteLine(starCostsOutPath);

// card_keywords.json 出力 (カードID → キーワード名リスト)
var kwOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "card_keywords.json");
Console.Error.WriteLine($"Extracted {cardKeywords.Count} card keyword mappings.");
var kwEntries = cardKeywords.OrderBy(kv => kv.Key).Select(kv =>
{
    var kwList = string.Join(", ", kv.Value.Select(k => $"\"{k}\""));
    return $"  \"{kv.Key}\": [{kwList}]";
});
WriteJson(kwOutPath, "{\n" + string.Join(",\n", kwEntries) + "\n}\n");
Console.WriteLine(kwOutPath);

// 生ローカライズ JSON をバージョンフォルダへ取り込む（版の固定）。
// tools/extracted は git 未追跡でゲーム更新時に内容が変わるため、各バージョンの版を
// Resources/{version}/localization/{eng,jpn}/ にコピーして保持する。
// StS2Shared 側は ResourceResolver で最新版を解決する。
{
    var outDir = Path.GetDirectoryName(outPath)!;
    var repoRoot = Path.GetFullPath(Path.Combine(outDir, "..", "..", ".."));
    var locDir = Path.Combine(repoRoot, "tools", "extracted", "localization");
    string[] locFiles =
    {
        "relics", "card_keywords", "afflictions", "enchantments",
        "encounters", "acts", "events", "ancients", "potions", "rest_site_ui"
    };
    foreach (var lang in new[] { "eng", "jpn" })
    {
        var destDir = Path.Combine(outDir, "localization", lang);
        Directory.CreateDirectory(destDir);
        foreach (var f in locFiles)
        {
            var src = Path.Combine(locDir, lang, $"{f}.json");
            if (File.Exists(src))
                File.Copy(src, Path.Combine(destDir, $"{f}.json"), overwrite: true);
            else
                Console.Error.WriteLine($"WARNING: {src} not found; skipping.");
        }
    }
    Console.Error.WriteLine($"Copied raw localization into {Path.Combine(outDir, "localization")}.");
}

// card_database.json 出力 (カード・レリックの EN/JP 表示名)
// 名前は DLL ではなくローカライズの {ID}.title に存在するため、
// tools/extracted/localization/{eng,jpn}/{cards,relics}.json から生成する。
{
    var outDir = Path.GetDirectoryName(outPath)!;
    // outDir = .../StS2Shared/Resources/{version} → 3階層上がリポジトリルート
    var repoRoot = Path.GetFullPath(Path.Combine(outDir, "..", "..", ".."));
    var locDir = Path.Combine(repoRoot, "tools", "extracted", "localization");

    // 指定ファイルの {PREFIX}{suffix} キー（PREFIX に追加ドット無し）を PREFIX→値 辞書に集約する。
    static Dictionary<string, string> LoadLocSuffix(string path, string suffix)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path)) return map;
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (!prop.Name.EndsWith(suffix, StringComparison.Ordinal)) continue;
            var prefix = prop.Name[..^suffix.Length];
            if (prefix.Contains('.')) continue; // 単一セグメントのみ
            map[prefix] = prop.Value.GetString() ?? "";
        }
        return map;
    }

    var engCards   = LoadLocSuffix(Path.Combine(locDir, "eng", "cards.json"), ".title");
    var jpnCards   = LoadLocSuffix(Path.Combine(locDir, "jpn", "cards.json"), ".title");
    var engRelics  = LoadLocSuffix(Path.Combine(locDir, "eng", "relics.json"), ".title");
    var jpnRelics  = LoadLocSuffix(Path.Combine(locDir, "jpn", "relics.json"), ".title");

    // 日本語をエスケープせず生のまま出力する（既存フォーマットに合わせる）。
    var jsonOpts = new System.Text.Json.JsonSerializerOptions
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    string J(string s) => System.Text.Json.JsonSerializer.Serialize(s, jsonOpts);

    if (engCards.Count == 0 && engRelics.Count == 0)
    {
        Console.Error.WriteLine($"WARNING: localization not found under {locDir}; skipping card_database.json.");
    }
    else
    {
        // (id, en, ja) を組み立てる。ja が無ければ en にフォールバック。
        var entries = new List<(string Id, string En, string Ja)>();
        foreach (var (prefix, en) in engCards)
            entries.Add(($"CARD.{prefix}", en, jpnCards.TryGetValue(prefix, out var ja) && ja.Length > 0 ? ja : en));
        foreach (var (prefix, en) in engRelics)
            entries.Add(($"RELIC.{prefix}", en, jpnRelics.TryGetValue(prefix, out var ja) && ja.Length > 0 ? ja : en));

        var dbOutPath = Path.Combine(outDir, "card_database.json");
        var dbLines = entries.OrderBy(e => e.Id, StringComparer.Ordinal).Select(e =>
            $"  {J(e.Id)}: {{ \"en\": {J(e.En)}, \"ja\": {J(e.Ja)} }}");
        WriteJson(dbOutPath, "{\n" + string.Join(",\n", dbLines) + "\n}\n");
        Console.Error.WriteLine($"Extracted {entries.Count} card/relic name mappings.");
        Console.WriteLine(dbOutPath);
    }

    // card_descriptions.json 出力（カードの EN/JP 説明文。生テキスト＝タグ・テンプレート保持）。
    var engCardDesc = LoadLocSuffix(Path.Combine(locDir, "eng", "cards.json"), ".description");
    var jpnCardDesc = LoadLocSuffix(Path.Combine(locDir, "jpn", "cards.json"), ".description");
    if (engCardDesc.Count == 0)
    {
        Console.Error.WriteLine($"WARNING: card descriptions not found under {locDir}; skipping card_descriptions.json.");
    }
    else
    {
        var descLines = engCardDesc.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv =>
        {
            var ja = jpnCardDesc.TryGetValue(kv.Key, out var j) && j.Length > 0 ? j : kv.Value;
            return $"  {J($"CARD.{kv.Key}")}: {{ \"en\": {J(kv.Value)}, \"ja\": {J(ja)} }}";
        });
        var descOutPath = Path.Combine(outDir, "card_descriptions.json");
        WriteJson(descOutPath, "{\n" + string.Join(",\n", descLines) + "\n}\n");
        Console.Error.WriteLine($"Extracted {engCardDesc.Count} card descriptions.");
        Console.WriteLine(descOutPath);
    }

    // card_descriptions_resolved.json / relic_descriptions_resolved.json 出力
    // 色タグ（[gold] 等）を保持したまま {Var} を実数値に解決する（個別カード説明文の表示用）。
    // base は純 base 値（combineDiff:false）、upgraded は純 upgraded 値。
    {
        string Res(string raw, IReadOnlyDictionary<string, int>? st, bool ja, bool upg) =>
            DescriptionFormatter.Resolve(raw, st, japanese: ja, upgraded: upg,
                preserveTags: true, combineDiff: false);

        // カード: { en, ja, enUpgraded, jaUpgraded }
        var cardLines = new List<string>();
        foreach (var (prefix, rawEn) in engCardDesc.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var id    = "CARD." + prefix;
            var rawJa = jpnCardDesc.TryGetValue(prefix, out var j) && j.Length > 0 ? j : rawEn;
            var st    = cardStats.TryGetValue(id, out var sd) ? sd : null;
            cardLines.Add(
                $"  {J(id)}: {{ \"en\": {J(Res(rawEn, st, false, false))}, \"ja\": {J(Res(rawJa, st, true, false))}, " +
                $"\"enUpgraded\": {J(Res(rawEn, st, false, true))}, \"jaUpgraded\": {J(Res(rawJa, st, true, true))} }}");
        }
        var cardResOut = Path.Combine(outDir, "card_descriptions_resolved.json");
        WriteJson(cardResOut, "{\n" + string.Join(",\n", cardLines) + "\n}\n");
        Console.Error.WriteLine($"Resolved {cardLines.Count} card descriptions.");
        Console.WriteLine(cardResOut);

        // レリック: { en, ja }（アップグレード概念なし）
        var engRelicDesc = LoadLocSuffix(Path.Combine(locDir, "eng", "relics.json"), ".description");
        var jpnRelicDesc = LoadLocSuffix(Path.Combine(locDir, "jpn", "relics.json"), ".description");
        var relicLines = new List<string>();
        foreach (var (prefix, rawEn) in engRelicDesc.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrEmpty(rawEn)) continue;
            var id    = "RELIC." + prefix;
            var rawJa = jpnRelicDesc.TryGetValue(prefix, out var j) && j.Length > 0 ? j : rawEn;
            var st    = relicStats.TryGetValue(prefix, out var sd) ? sd : null;
            relicLines.Add(
                $"  {J(id)}: {{ \"en\": {J(Res(rawEn, st, false, false))}, \"ja\": {J(Res(rawJa, st, true, false))} }}");
        }
        var relicResOut = Path.Combine(outDir, "relic_descriptions_resolved.json");
        WriteJson(relicResOut, "{\n" + string.Join(",\n", relicLines) + "\n}\n");
        Console.Error.WriteLine($"Resolved {relicLines.Count} relic descriptions.");
        Console.WriteLine(relicResOut);
    }
}

// card_related.json 出力（カードのホバーツールチップに出る関連カード = get_ExtraHoverTips）
// 値はカードクラスのみにフィルタ（power/orb/DynamicVar ノイズを除外）。CREATED BY は参照側で逆引き。
{
    var outDir = Path.GetDirectoryName(outPath)!;
    const string cardsNs = "MegaCrit.Sts2.Core.Models.Cards";
    var cardRelated = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
    foreach (var th in mr.TypeDefinitions)
    {
        var td = mr.GetTypeDefinition(th);
        if (mr.GetString(td.Namespace) != cardsNs) continue;
        var cls = mr.GetString(td.Name);
        if (cls.StartsWith('<') || !cardClasses.Contains(cls)) continue;
        var srcId = "CARD." + CamelToUpperSnake(cls);
        var refs = CollectGenericArgRefs(mr, peReader, td, n => n == "get_ExtraHoverTips")
            .Where(r => cardClasses.Contains(r))
            .Select(r => "CARD." + CamelToUpperSnake(r))
            .Where(id => id != srcId)
            .Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        if (refs.Count > 0)
            cardRelated[srcId] = refs;
    }
    var relLines = cardRelated.Select(kv =>
        $"  \"{kv.Key}\": [{string.Join(", ", kv.Value.Select(v => $"\"{v}\""))}]");
    var relOutPath = Path.Combine(outDir, "card_related.json");
    WriteJson(relOutPath, "{\n" + string.Join(",\n", relLines) + "\n}\n");
    Console.Error.WriteLine($"Extracted {cardRelated.Count} card related-card mappings.");
    Console.WriteLine(relOutPath);
}

// monster_names.json / encounter_monsters.json / event_acts.json 出力
// monster_names: アニメーション dir × ローカライズ名（loc に無い表示名は既存ファイルを引き継ぐ）
// encounter_monsters / event_acts: DLL のモデルクラス（Encounters / Acts）から IL 抽出
{
    var outDir2  = Path.GetDirectoryName(outPath)!;
    var repoRoot2 = Path.GetFullPath(Path.Combine(outDir2, "..", "..", ".."));
    var locDir2  = Path.Combine(repoRoot2, "tools", "extracted", "localization");

    var jsonOpts2 = new System.Text.Json.JsonSerializerOptions
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    string J(string s) => System.Text.Json.JsonSerializer.Serialize(s, jsonOpts2);

    var excludedEvents = new HashSet<string>(StringComparer.Ordinal)
        { "DEPRECATED_EVENT", "ERROR", "MOCK_EVENT_MODEL", "PROCEED" };

    // ---- 有効なモンスターモデル ID 集合（DLL）----
    // monster_names と encounter_monsters は同じ「モデル snake 小文字」を ID 空間として共有する。
    // SiteBuilder は ID をそのまま images/monsters/{id}.png|gif とページ monsters/{id}.html に使うため、
    // 画像フォルダが存在する ID は画像表示、無い ID は名前のみ（グレースフル）になる。
    const string monNs = "MegaCrit.Sts2.Core.Models.Monsters";
    var monsterIdToKey = new SortedDictionary<string, string>(StringComparer.Ordinal); // id(lower) → SNAKE
    foreach (var th in mr.TypeDefinitions)
    {
        var td = mr.GetTypeDefinition(th);
        if (mr.GetString(td.Namespace) != monNs) continue;       // .Monsters のみ（.Mocks 等は除外）
        var cls = mr.GetString(td.Name);
        if (cls.StartsWith('<') || cls.Contains("Mock") || cls.Contains("Deprecated")) continue;
        var snake = CamelToUpperSnake(cls);
        monsterIdToKey[snake.ToLowerInvariant()] = snake;
    }

    // ---- monster_names.json（モデル粒度。名前は localization {SNAKE}.name）----
    var engMon = LoadLocBySuffix(Path.Combine(locDir2, "eng", "monsters.json"), ".name");
    var jpnMon = LoadLocBySuffix(Path.Combine(locDir2, "jpn", "monsters.json"), ".name");

    // 既存 monster_names.json を表示名フォールバックとして読む（loc に無い手書き名を保持）
    var monFallback = new Dictionary<string, (string En, string Ja)>(StringComparer.OrdinalIgnoreCase);
    foreach (var cand in new[] { Path.Combine(outDir2, "monster_names.json"),
                                 Path.Combine(repoRoot2, "StS2Shared", "Resources", "monster_names.json") })
    {
        if (!File.Exists(cand)) continue;
        using var d = System.Text.Json.JsonDocument.Parse(File.ReadAllText(cand));
        foreach (var el in d.RootElement.EnumerateArray())
        {
            var dn = el.GetProperty("dirName").GetString();
            if (string.IsNullOrEmpty(dn)) continue;
            var en = el.TryGetProperty("en", out var ev) ? ev.GetString() ?? "" : "";
            var ja = el.TryGetProperty("ja", out var jv) ? jv.GetString() ?? "" : "";
            monFallback[dn] = (en, ja);
        }
        break;
    }

    var monLines = new List<string>();
    foreach (var (id, key) in monsterIdToKey)
    {
        string en, ja;
        if (engMon.TryGetValue(key, out var le) && le.Length > 0)
        {
            en = le;
            ja = jpnMon.TryGetValue(key, out var lj) && lj.Length > 0 ? lj : le;
        }
        else if (monFallback.TryGetValue(id, out var fb) && fb.En.Length > 0)
        {
            en = fb.En;
            ja = fb.Ja.Length > 0 ? fb.Ja : fb.En;
        }
        else
        {
            en = SnakeToTitle(key);
            ja = en;
        }
        monLines.Add($"  {{ \"dirName\": {J(id)}, \"en\": {J(en)}, \"ja\": {J(ja)} }}");
    }
    WriteJson(Path.Combine(outDir2, "monster_names.json"),
        "[\n" + string.Join(",\n", monLines) + "\n]\n");
    Console.Error.WriteLine($"Extracted {monLines.Count} monster names (model granularity).");
    Console.WriteLine(Path.Combine(outDir2, "monster_names.json"));

    // ---- encounter_monsters.json（encounter → モンスターモデル ID）----
    const string encNs = "MegaCrit.Sts2.Core.Models.Encounters";
    var encMap = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
    foreach (var th in mr.TypeDefinitions)
    {
        var td = mr.GetTypeDefinition(th);
        if (mr.GetString(td.Namespace) != encNs) continue;
        var cls = mr.GetString(td.Name);
        if (cls.StartsWith('<') || cls.Contains("Deprecated") || cls.Contains("Mock")) continue;

        var refs = CollectGenericArgRefs(mr, peReader, td, n => n == "get_AllPossibleMonsters");
        if (refs.Count == 0)
            refs = CollectGenericArgRefs(mr, peReader, td, n => n == "GenerateMonsters");
        // 有効なモンスターモデル ID のみ（基底型 MonsterModel 等のノイズを除外）
        var monIds = refs.Select(r => CamelToUpperSnake(r).ToLowerInvariant())
            .Where(id => monsterIdToKey.ContainsKey(id))
            .Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        if (monIds.Count > 0)
            encMap[CamelToUpperSnake(cls)] = monIds;
    }
    var encLines = encMap.Select(kv => $"  {J(kv.Key)}: [{string.Join(", ", kv.Value.Select(J))}]");
    WriteJson(Path.Combine(outDir2, "encounter_monsters.json"),
        "{\n" + string.Join(",\n", encLines) + "\n}\n");
    Console.Error.WriteLine($"Extracted {encMap.Count} encounter→monster mappings.");
    Console.WriteLine(Path.Combine(outDir2, "encounter_monsters.json"));

    // ---- event_acts.json ----
    const string actNs = "MegaCrit.Sts2.Core.Models.Acts";
    var actEvents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    foreach (var th in mr.TypeDefinitions)
    {
        var td = mr.GetTypeDefinition(th);
        if (mr.GetString(td.Namespace) != actNs) continue;
        var cls = mr.GetString(td.Name);
        if (cls.StartsWith('<') || cls.Contains("Deprecated")) continue;
        var refs = CollectGenericArgRefs(mr, peReader, td, n => n == "get_AllEvents");
        actEvents[CamelToUpperSnake(cls)] = refs.Select(CamelToUpperSnake)
            .Where(e => !excludedEvents.Contains(e))
            .Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    var engActTitle = LoadLocBySuffix(Path.Combine(locDir2, "eng", "acts.json"), ".title");
    var jpnActTitle = LoadLocBySuffix(Path.Combine(locDir2, "jpn", "acts.json"), ".title");

    // ALL_ACTS = どの Act の get_AllEvents にも属さないイベント（全幕共通）
    var allEventIds = LoadLocBySuffix(Path.Combine(locDir2, "eng", "events.json"), ".title").Keys
        .Where(id => !excludedEvents.Contains(id));
    var claimed = actEvents.Values.SelectMany(x => x).ToHashSet(StringComparer.Ordinal);
    var globalEvents = allEventIds.Where(e => !claimed.Contains(e))
        .Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();

    // Act の表示名の接頭辞・順序は編集上の定義（ローカライズには素のタイトルのみ）。
    var actMeta = new (string Id, string PrefixEn, string PrefixJp)[]
    {
        ("OVERGROWTH", "Act 1: ", "第一幕："),
        ("UNDERDOCKS", "Act 1: ", "第一幕："),
        ("HIVE",       "Act 2: ", "第二幕："),
        ("GLORY",      "Act 3: ", "第三幕："),
    };
    var actEntries = new List<string>();
    foreach (var (id, pe, pj) in actMeta)
    {
        if (!actEvents.TryGetValue(id, out var evs)) continue;
        var en = pe + (engActTitle.TryGetValue(id, out var te) ? te : SnakeToTitle(id));
        var ja = pj + (jpnActTitle.TryGetValue(id, out var tj) ? tj : SnakeToTitle(id));
        actEntries.Add($"  {{ \"id\": {J(id)}, \"nameJp\": {J(ja)}, \"nameEn\": {J(en)}, \"events\": [{string.Join(", ", evs.Select(J))}] }}");
    }
    actEntries.Add($"  {{ \"id\": \"ALL_ACTS\", \"nameJp\": \"全幕共通\", \"nameEn\": \"All Acts\", \"events\": [{string.Join(", ", globalEvents.Select(J))}] }}");
    WriteJson(Path.Combine(outDir2, "event_acts.json"),
        "[\n" + string.Join(",\n", actEntries) + "\n]\n");
    Console.Error.WriteLine($"Extracted {actEvents.Count} acts (+ALL_ACTS, {globalEvents.Count} global events).");
    Console.WriteLine(Path.Combine(outDir2, "event_acts.json"));

    // ---- encounter_acts.json ----（event_acts.json と同形式。アクト→戦闘エンカウンターを階層別＋ボス順で）
    // 各 Act クラスの GenerateAllEncounters() オーバーライドが Add<EncounterType>() で
    // 自分専用のエンカウンターを登録する（event_acts の get_AllEvents と同パターン）。
    // 階層は ID サフィックスで判定。ボス順は get_BossDiscoveryOrder（IL 順 = 出現順）。
    static string TierOf(string id) =>
        id.EndsWith("_WEAK")  ? "weak"  : id.EndsWith("_NORMAL") ? "normal" :
        id.EndsWith("_ELITE") ? "elite" : id.EndsWith("_BOSS")   ? "boss"   : "special";

    var actEncs = new Dictionary<string, (List<string> weak, List<string> normal,
        List<string> elite, List<string> boss, List<string> order)>(StringComparer.Ordinal);
    foreach (var th in mr.TypeDefinitions)
    {
        var td = mr.GetTypeDefinition(th);
        if (mr.GetString(td.Namespace) != actNs) continue;
        var cls = mr.GetString(td.Name);
        if (cls.StartsWith('<') || cls.Contains("Deprecated")) continue;

        var all = CollectGenericArgRefs(mr, peReader, td, n => n == "GenerateAllEncounters")
            .Select(CamelToUpperSnake).Distinct().ToList();
        var bossOrder = CollectGenericArgRefs(mr, peReader, td, n => n == "get_BossDiscoveryOrder")
            .Select(CamelToUpperSnake).Distinct().ToList();   // IL 順保持（並べ替えない）

        var g = (weak: new List<string>(), normal: new List<string>(),
                 elite: new List<string>(), boss: new List<string>(), order: bossOrder);
        foreach (var id in all.OrderBy(x => x, StringComparer.Ordinal))
            switch (TierOf(id))
            {
                case "weak":   g.weak.Add(id);   break;
                case "normal": g.normal.Add(id); break;
                case "elite":  g.elite.Add(id);  break;
                case "boss":   g.boss.Add(id);   break;
            }
        actEncs[CamelToUpperSnake(cls)] = g;
    }

    var encActEntries = new List<string>();
    foreach (var (id, pe, pj) in actMeta)   // 既存 actMeta（順序・接頭辞）を共有
    {
        if (!actEncs.TryGetValue(id, out var g)) continue;
        var en = pe + (engActTitle.TryGetValue(id, out var te) ? te : SnakeToTitle(id));
        var ja = pj + (jpnActTitle.TryGetValue(id, out var tj) ? tj : SnakeToTitle(id));
        string Arr(List<string> xs) => "[" + string.Join(", ", xs.Select(J)) + "]";
        encActEntries.Add($"  {{ \"id\": {J(id)}, \"nameJp\": {J(ja)}, \"nameEn\": {J(en)}, " +
            $"\"weak\": {Arr(g.weak)}, \"normal\": {Arr(g.normal)}, \"elite\": {Arr(g.elite)}, " +
            $"\"boss\": {Arr(g.boss)}, \"bossOrder\": {Arr(g.order)} }}");
    }
    WriteJson(Path.Combine(outDir2, "encounter_acts.json"),
        "[\n" + string.Join(",\n", encActEntries) + "\n]\n");
    Console.Error.WriteLine($"Extracted {actEncs.Count} acts' encounter pools.");
    Console.WriteLine(Path.Combine(outDir2, "encounter_acts.json"));

    // ---- event_images.json（イベント ID → events 内の主画像相対パス）----
    // ルート直下の {id}.png.import のみを主画像として採用（サブフォルダ/_foreground 等の副次素材は除外）。
    var eventsDir = Path.Combine(repoRoot2, "tools", "extracted", "images", "events");
    var evImg = new SortedDictionary<string, string>(StringComparer.Ordinal);
    foreach (var id in allEventIds)
    {
        var raw = id.ToLowerInvariant();
        if (File.Exists(Path.Combine(eventsDir, raw + ".png.import")))
            evImg[id] = raw + ".png";
    }
    WriteJson(Path.Combine(outDir2, "event_images.json"),
        "{\n" + string.Join(",\n", evImg.Select(kv => $"  {J(kv.Key)}: {J(kv.Value)}")) + "\n}\n");
    Console.Error.WriteLine($"Extracted {evImg.Count} event image paths.");
    Console.WriteLine(Path.Combine(outDir2, "event_images.json"));
}

// card_images.json 出力（カード ID → card_portraits_png 内のソース相対パス）
// 実ファイルをスキャンして対応付ける。探索順は Toys 実装に合わせ、まず全 dir+ルートで
// {rawId}.png、見つからなければ {rawId}_{type}.png を探す。
{
    var outDir3 = Path.GetDirectoryName(outPath)!;
    var repoRoot3 = Path.GetFullPath(Path.Combine(outDir3, "..", "..", ".."));
    var portraitsDir = Path.Combine(repoRoot3, "tools", "extracted", "images", "card_portraits_png");

    var jsonOpts3 = new System.Text.Json.JsonSerializerOptions
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    string J(string s) => System.Text.Json.JsonSerializer.Serialize(s, jsonOpts3);

    if (!Directory.Exists(portraitsDir))
    {
        Console.Error.WriteLine($"WARNING: {portraitsDir} not found; skipping card_images.json.");
    }
    else
    {
        // 探索対象: 各サブディレクトリ + ルート直下（ルートは subdir 名 "" で表す）
        var searchDirs = Directory.GetDirectories(portraitsDir)
            .Select(d => (name: Path.GetFileName(d)!, full: d))
            .Append(("", portraitsDir))
            .ToList();

        string? Find(string fileName)
        {
            foreach (var (name, full) in searchDirs)
                if (File.Exists(Path.Combine(full, fileName)))
                    return name.Length == 0 ? fileName : name + "/" + fileName;
            return null;
        }

        var imgEntries = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (cardId, type) in results)
        {
            var raw = (cardId.Contains('.') ? cardId[(cardId.IndexOf('.') + 1)..] : cardId).ToLowerInvariant();
            var rel = Find(raw + ".png");
            if (rel is null && !string.IsNullOrEmpty(type))
                rel = Find(raw + "_" + type.ToLowerInvariant() + ".png");
            if (rel is not null)
                imgEntries[cardId] = rel;
        }
        var imgLines = imgEntries.Select(kv => $"  {J(kv.Key)}: {J(kv.Value)}");
        WriteJson(Path.Combine(outDir3, "card_images.json"),
            "{\n" + string.Join(",\n", imgLines) + "\n}\n");
        Console.Error.WriteLine($"Extracted {imgEntries.Count} card image paths.");
        Console.WriteLine(Path.Combine(outDir3, "card_images.json"));
    }
}

// relic_images.json 出力（レリック ID → relics 内のソース相対パス）
// 実ファイル（.png.import）をスキャンして対応付ける。beta/ サブフォルダも対象。
// キーは接頭辞なし大文字 ID（relic_rarities.json と同形）。値は .png 相対パス。
{
    var outDir4 = Path.GetDirectoryName(outPath)!;
    var repoRoot4 = Path.GetFullPath(Path.Combine(outDir4, "..", "..", ".."));
    var relicsDir = Path.Combine(repoRoot4, "tools", "extracted", "images", "relics");

    var jsonOpts4 = new System.Text.Json.JsonSerializerOptions
    { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    string J4(string s) => System.Text.Json.JsonSerializer.Serialize(s, jsonOpts4);

    if (!Directory.Exists(relicsDir))
    {
        Console.Error.WriteLine($"WARNING: {relicsDir} not found; skipping relic_images.json.");
    }
    else
    {
        // 探索対象: 各サブディレクトリ（beta 等）+ ルート直下（ルートは subdir 名 "" で表す）
        var searchDirs = Directory.GetDirectories(relicsDir)
            .Select(d => (name: Path.GetFileName(d)!, full: d))
            .Append(("", relicsDir))
            .ToList();

        string? Find(string raw)
        {
            foreach (var (name, full) in searchDirs)
                if (File.Exists(Path.Combine(full, raw + ".png.import")))
                    return name.Length == 0 ? raw + ".png" : name + "/" + raw + ".png";
            return null;
        }

        var relImg = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var cls in relicClasses)
        {
            var id = CamelToUpperSnake(cls);
            var rel = Find(id.ToLowerInvariant());
            if (rel is not null) relImg[id] = rel;
        }
        var relImgLines = relImg.Select(kv => $"  {J4(kv.Key)}: {J4(kv.Value)}");
        WriteJson(Path.Combine(outDir4, "relic_images.json"),
            "{\n" + string.Join(",\n", relImgLines) + "\n}\n");
        Console.Error.WriteLine($"Extracted {relImg.Count} relic image paths.");
        Console.WriteLine(Path.Combine(outDir4, "relic_images.json"));
    }
}

// ── Potion 抽出 ─────────────────────────────────────────────────────────────
// カード/レリックと同一の IL パターン。クラス集合は名前空間 Models.Potions から直接列挙し
// （プールの生成メソッド名に依存しない）、キャラ帰属のみ *PotionPool の生成メソッド IL から補う。
// レアリティは専用 enum PotionRarity（COMMON/UNCOMMON/RARE/POTENCY）を get_Rarity で引く。
{
    var outDirP   = Path.GetDirectoryName(outPath)!;
    var repoRootP = Path.GetFullPath(Path.Combine(outDirP, "..", "..", ".."));
    const string potionsNs = "MegaCrit.Sts2.Core.Models.Potions";

    var jsonOptsP = new System.Text.Json.JsonSerializerOptions
    { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    string JP(string s) => System.Text.Json.JsonSerializer.Serialize(s, jsonOptsP);
    static string TitleCase(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

    // (1) ポーションクラス集合（名前空間ベース。Mock/Deprecated/コンパイラ生成は除外）
    var potionClasses = new HashSet<string>(StringComparer.Ordinal);
    foreach (var th in mr.TypeDefinitions)
    {
        var td = mr.GetTypeDefinition(th);
        if (mr.GetString(td.Namespace) != potionsNs) continue;
        var cls = mr.GetString(td.Name);
        if (cls.StartsWith('<') || cls.Contains("Mock") || cls.Contains("Deprecated")) continue;
        potionClasses.Add(cls);
    }
    Console.Error.WriteLine($"Found {potionClasses.Count} potion classes.");

    // (2) キャラ／出所帰属（*PotionPool が生成するポーション型 → プール由来ラベル）
    // 各プールの GenerateAllPotions / GetUnlockedPotions IL を走査し、
    //   ・generic Add<Xxx>()       … MethodSpec 第一型引数（SharedPotionPool が使用）
    //   ・newobj Xxx::.ctor()       … 具体生成（将来のキャラ専用プール用に対応）
    // の双方からポーション型名を収集する。
    // 注: v0.107.0 ではキャラ専用プール（Ironclad/Silent/Defect/Necrobinder/Regent）は空で、
    //   標準ポーションは全て Shared。残りの特殊／生成系ポーションはどのプールにも属さず未帰属になる。
    // 同一ポーションが複数プールに現れた場合は priority 順（Shared > Event > Token > 各キャラ）で確定する。
    var potionPoolMap = new (string Pool, string Label)[]
    {
        ("SharedPotionPool",      "Shared"),
        ("EventPotionPool",       "Event"),
        ("TokenPotionPool",       "Token"),
        ("IroncladPotionPool",    "Ironclad"),
        ("SilentPotionPool",      "Silent"),
        ("DefectPotionPool",      "Defect"),
        ("NecrobinderPotionPool", "Necrobinder"),
        ("RegentPotionPool",      "Regent"),
    };

    List<string> CollectPoolPotions(string poolName)
    {
        var ids = new List<string>();
        foreach (var th in mr.TypeDefinitions)
        {
            var td = mr.GetTypeDefinition(th);
            if (mr.GetString(td.Name) != poolName) continue;
            foreach (var mh in td.GetMethods())
            {
                var m = mr.GetMethodDefinition(mh);
                var mn = mr.GetString(m.Name);
                if (mn != "GenerateAllPotions" && mn != "GetUnlockedPotions") continue;
                if (m.RelativeVirtualAddress == 0) continue;
                var il = peReader.GetMethodBody(m.RelativeVirtualAddress)?.GetILBytes();
                if (il == null) continue;
                for (int i = 0; i + 4 < il.Length; i++)
                {
                    int tok = il[i+1] | (il[i+2]<<8) | (il[i+3]<<16) | (il[i+4]<<24);
                    string? tn = (il[i] == 0x28 || il[i] == 0x6F) ? ResolveMethodSpecFirstArg(mr, tok)
                               :  il[i] == 0x73                    ? ResolveCtorDeclaringType(mr, tok)
                               :  null;
                    if (string.IsNullOrEmpty(tn) || tn.Contains('<') || !potionClasses.Contains(tn)) continue;
                    ids.Add(CamelToUpperSnake(tn));
                }
            }
            break;
        }
        return ids;
    }

    var potionCharacters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var (pool, label) in potionPoolMap)        // priority = 配列順
        foreach (var id in CollectPoolPotions(pool))
            potionCharacters.TryAdd(id, label);          // 先勝ち

    // (3) PotionRarity enum 値マップ（COMMON/UNCOMMON/RARE/POTENCY → int）
    var potionRarityByInt = new Dictionary<int, string>();
    foreach (var th in mr.TypeDefinitions)
    {
        var td = mr.GetTypeDefinition(th);
        if (mr.GetString(td.Name) != "PotionRarity") continue;
        foreach (var fh in td.GetFields())
        {
            var f = mr.GetFieldDefinition(fh);
            var fn = mr.GetString(f.Name);
            if (fn == "value__") continue;
            var c = f.GetDefaultValue(); if (c.IsNil) continue;
            var br = mr.GetBlobReader(mr.GetConstant(c).Value);
            potionRarityByInt[br.ReadInt32()] = fn;
        }
        break;
    }

    // (4) 各ポーションクラスから rarity / stats / canonicalVars（レリックと同ロジック）
    var potionRarities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var potionStats    = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
    foreach (var th in mr.TypeDefinitions)
    {
        var td  = mr.GetTypeDefinition(th);
        var cls = mr.GetString(td.Name);
        if (!potionClasses.Contains(cls)) continue;
        var id = CamelToUpperSnake(cls);

        // get_Rarity → ldc.i4 N + ret
        foreach (var mh in td.GetMethods())
        {
            var m = mr.GetMethodDefinition(mh);
            if (mr.GetString(m.Name) != "get_Rarity") continue;
            if (m.RelativeVirtualAddress == 0) continue;
            var il = peReader.GetMethodBody(m.RelativeVirtualAddress)?.GetILBytes();
            if (il == null) continue;
            var (val, _) = ReadLdcI4(il, 0);
            if (val.HasValue && potionRarityByInt.TryGetValue(val.Value, out var rn))
                potionRarities[id] = TitleCase(rn);
            break;
        }

        // パスP1: .ctor のフィールド代入 (stfld) / プロパティセッター (set_Xxx)
        foreach (var mh in td.GetMethods())
        {
            var m = mr.GetMethodDefinition(mh);
            if (mr.GetString(m.Name) != ".ctor") continue;
            if (m.RelativeVirtualAddress == 0) continue;
            var il = peReader.GetMethodBody(m.RelativeVirtualAddress)?.GetILBytes();
            if (il == null) continue;
            int? lastInt = null;
            var ctorFields = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < il.Length;)
            {
                byte op = il[i];
                if ((op == 0x7D || op == 0x28 || op == 0x6F) && i + 4 < il.Length)
                {
                    if (lastInt.HasValue)
                    {
                        int token = il[i+1] | (il[i+2]<<8) | (il[i+3]<<16) | (il[i+4]<<24);
                        string? name = null;
                        if (op == 0x7D) name = ResolveFieldToken(mr, token);
                        else
                        {
                            var rawName = ResolveMethodName(mr, token);
                            if (rawName.StartsWith("set_", StringComparison.Ordinal)) name = rawName[4..];
                        }
                        if (!string.IsNullOrEmpty(name)) ctorFields.TryAdd(name, lastInt.Value);
                    }
                    lastInt = null;
                    i += 5;
                    continue;
                }
                var (v2, sz2) = ReadLdcI4(il, i);
                if (v2.HasValue) lastInt = v2;
                else if (op != 0x02) lastInt = null;       // ldarg.0 は値を維持
                i += sz2;
            }
            if (ctorFields.Count > 0) potionStats[id] = ctorFields;
            break;
        }

        // パスP2: get_CanonicalVars → 直前の最後の ldc.i4 を次の newobj XxxVar の値とする
        foreach (var mh in td.GetMethods())
        {
            var m = mr.GetMethodDefinition(mh);
            if (mr.GetString(m.Name) != "get_CanonicalVars") continue;
            if (m.RelativeVirtualAddress == 0) continue;
            var il = peReader.GetMethodBody(m.RelativeVirtualAddress)?.GetILBytes();
            if (il == null) continue;
            int? pending = null;
            var canon = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < il.Length;)
            {
                byte op = il[i];
                if (op == 0x73 && i + 4 < il.Length)       // newobj
                {
                    int tok = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                    if (pending.HasValue && varNameByCtorToken.TryGetValue(tok, out var vname))
                    {
                        canon.TryAdd(vname, pending.Value);
                        pending = null;
                    }
                    i += 5;
                    continue;
                }
                var (vR, szR) = ReadLdcI4(il, i);
                if (vR.HasValue) pending = vR;
                i += szR;
            }
            if (canon.Count > 0)
            {
                if (!potionStats.ContainsKey(id)) potionStats[id] = canon;
                else foreach (var (k, v) in canon) potionStats[id].TryAdd(k, v);
            }
            break;
        }
    }
    Console.Error.WriteLine($"Potions: rarities={potionRarities.Count}, with-stats={potionStats.Count}, characters={potionCharacters.Count}");

    // (5) 出力: potion_rarities / potion_stats / potion_characters
    var pRarLines = potionRarities.OrderBy(kv => kv.Key, StringComparer.Ordinal)
        .Select(kv => $"  {JP(kv.Key)}: {JP(kv.Value)}");
    WriteJson(Path.Combine(outDirP, "potion_rarities.json"),
        "{\n" + string.Join(",\n", pRarLines) + "\n}\n");
    Console.WriteLine(Path.Combine(outDirP, "potion_rarities.json"));

    var pStatLines = potionStats.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv =>
    {
        var fields = kv.Value.OrderBy(f => f.Key, StringComparer.Ordinal)
            .Select(f => $"    {JP(f.Key)}: {f.Value}");
        return $"  {JP(kv.Key)}: {{\n{string.Join(",\n", fields)}\n  }}";
    });
    WriteJson(Path.Combine(outDirP, "potion_stats.json"),
        "{\n" + string.Join(",\n", pStatLines) + "\n}\n");
    Console.WriteLine(Path.Combine(outDirP, "potion_stats.json"));

    var pCharLines = potionCharacters.OrderBy(kv => kv.Key, StringComparer.Ordinal)
        .Select(kv => $"  {JP(kv.Key)}: {JP(kv.Value)}");
    WriteJson(Path.Combine(outDirP, "potion_characters.json"),
        "{\n" + string.Join(",\n", pCharLines) + "\n}\n");
    Console.WriteLine(Path.Combine(outDirP, "potion_characters.json"));

    // (6) potion_images.json（実ファイルスキャン。potions/ はサブフォルダ無し）
    var potionsImgDir = Path.Combine(repoRootP, "tools", "extracted", "images", "potions");
    if (Directory.Exists(potionsImgDir))
    {
        var pImg = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var cls in potionClasses)
        {
            var id = CamelToUpperSnake(cls);
            var file = id.ToLowerInvariant() + ".png";
            if (File.Exists(Path.Combine(potionsImgDir, file + ".import")))
                pImg[id] = file;
        }
        var pImgLines = pImg.Select(kv => $"  {JP(kv.Key)}: {JP(kv.Value)}");
        WriteJson(Path.Combine(outDirP, "potion_images.json"),
            "{\n" + string.Join(",\n", pImgLines) + "\n}\n");
        Console.Error.WriteLine($"Extracted {pImg.Count} potion image paths.");
        Console.WriteLine(Path.Combine(outDirP, "potion_images.json"));
    }
    else Console.Error.WriteLine($"WARNING: {potionsImgDir} not found; skipping potion_images.json.");

    // (7) 表示名・説明文（localization potions.json）。キーは POTION. 接頭辞。
    var locDirP = Path.Combine(repoRootP, "tools", "extracted", "localization");
    var engTitle = LoadLocBySuffix(Path.Combine(locDirP, "eng", "potions.json"), ".title");
    var jpnTitle = LoadLocBySuffix(Path.Combine(locDirP, "jpn", "potions.json"), ".title");
    var engDesc  = LoadLocBySuffix(Path.Combine(locDirP, "eng", "potions.json"), ".description");
    var jpnDesc  = LoadLocBySuffix(Path.Combine(locDirP, "jpn", "potions.json"), ".description");

    if (engTitle.Count > 0)
    {
        var dbLines = engTitle.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv =>
        {
            var ja = jpnTitle.TryGetValue(kv.Key, out var j) && j.Length > 0 ? j : kv.Value;
            return $"  {JP($"POTION.{kv.Key}")}: {{ \"en\": {JP(kv.Value)}, \"ja\": {JP(ja)} }}";
        });
        WriteJson(Path.Combine(outDirP, "potion_database.json"),
            "{\n" + string.Join(",\n", dbLines) + "\n}\n");
        Console.Error.WriteLine($"Extracted {engTitle.Count} potion name mappings.");
        Console.WriteLine(Path.Combine(outDirP, "potion_database.json"));
    }
    else Console.Error.WriteLine($"WARNING: potions localization not found under {locDirP}; skipping potion descriptions.");

    if (engDesc.Count > 0)
    {
        // potion_descriptions.json（生テキスト＝タグ・{Var} 保持）
        var rawLines = engDesc.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv =>
        {
            var ja = jpnDesc.TryGetValue(kv.Key, out var j) && j.Length > 0 ? j : kv.Value;
            return $"  {JP($"POTION.{kv.Key}")}: {{ \"en\": {JP(kv.Value)}, \"ja\": {JP(ja)} }}";
        });
        WriteJson(Path.Combine(outDirP, "potion_descriptions.json"),
            "{\n" + string.Join(",\n", rawLines) + "\n}\n");
        Console.WriteLine(Path.Combine(outDirP, "potion_descriptions.json"));

        // potion_descriptions_resolved.json（{Var} を potion_stats で実数解決、色タグ保持）
        string Res(string raw, IReadOnlyDictionary<string, int>? st, bool ja) =>
            DescriptionFormatter.Resolve(raw, st, japanese: ja, upgraded: false,
                preserveTags: true, combineDiff: false);
        var resLines = engDesc.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv =>
        {
            var rawEn = kv.Value;
            var rawJa = jpnDesc.TryGetValue(kv.Key, out var j) && j.Length > 0 ? j : rawEn;
            var st = potionStats.TryGetValue(kv.Key, out var sd) ? sd : null;
            return $"  {JP($"POTION.{kv.Key}")}: {{ \"en\": {JP(Res(rawEn, st, false))}, \"ja\": {JP(Res(rawJa, st, true))} }}";
        });
        WriteJson(Path.Combine(outDirP, "potion_descriptions_resolved.json"),
            "{\n" + string.Join(",\n", resLines) + "\n}\n");
        Console.Error.WriteLine($"Resolved {engDesc.Count} potion descriptions.");
        Console.WriteLine(Path.Combine(outDirP, "potion_descriptions_resolved.json"));
    }
}

// ancient_options.json 出力
// Ancient イベントクラスのオプションプールを IL から抽出する
{
    const string ancientNs = "MegaCrit.Sts2.Core.Models.Events";
    var ancientClassList = new HashSet<string>(StringComparer.Ordinal)
        { "Darv", "Neow", "Nonupeipe", "Orobas", "Pael", "Tanx", "Tezcatara", "TheArchitect", "Vakuu" };

    // フレームワーク・インフラ型（アイテムではない）を除外
    var noiseTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "EVENT_OPTION", "RELIC_MODEL", "CARD_MODEL", "I_HOVER_TIP", "LOC_STRING",
        "CHARACTER_MODEL", "VALID_RELIC_SET", "CURSED_PEARL"
    };
    // 集約メソッド（個別プールに委譲するだけ）は出力しない
    var skipMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "AllPossibleOptions", "GenerateInitialOptions", "GenerateInitialOptionsWrapper" };

    var ancientOptions = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);

    foreach (var typeHandle in mr.TypeDefinitions)
    {
        var typeDef = mr.GetTypeDefinition(typeHandle);
        var className = mr.GetString(typeDef.Name);
        var ns = mr.GetString(typeDef.Namespace);
        if (ns != ancientNs || !ancientClassList.Contains(className)) continue;

        var ancientId = CamelToUpperSnake(className);
        var methodGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mhx in typeDef.GetMethods())
        {
            var method = mr.GetMethodDefinition(mhx);
            var methodName = mr.GetString(method.Name);

            // コンパイラ生成メソッドをスキップ
            if (methodName.StartsWith('<')) continue;

            // オプション関連メソッドのみ対象（.cctor は static 初期化でプールを持つ場合があるため含める）
            if (!methodName.Contains("Option", StringComparison.OrdinalIgnoreCase) &&
                !methodName.Contains("Pool", StringComparison.OrdinalIgnoreCase) &&
                !methodName.Contains("Generate", StringComparison.OrdinalIgnoreCase) &&
                !methodName.Contains("Discovery", StringComparison.OrdinalIgnoreCase) &&
                !methodName.Contains("Weapon", StringComparison.OrdinalIgnoreCase) &&
                methodName != ".cctor")
                continue;

            if (method.RelativeVirtualAddress == 0) continue;
            var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
            if (body == null) continue;
            var il = body.GetILBytes();

            var typeNames = new List<string>();
            for (int i = 0; i + 4 < il.Length; i++)
            {
                byte op = il[i];
                if (op is 0x28 or 0x6F) // call, callvirt
                {
                    int token = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                    var tname = ResolveMethodSpecFirstArg(mr, token);
                    if (!string.IsNullOrEmpty(tname) && tname.Length > 2 && !tname.Contains('<'))
                    {
                        var snakeId = CamelToUpperSnake(tname);
                        if (!noiseTypes.Contains(snakeId))
                            typeNames.Add(tname);
                    }
                    i += 4;
                }
            }

            if (typeNames.Count == 0) continue;
            var key = methodName == ".cctor" ? "RelicPool"
                    : methodName.StartsWith("get_") ? methodName[4..]
                    : methodName;
            if (skipMethods.Contains(key)) continue;
            if (!methodGroups.TryGetValue(key, out var lst))
                methodGroups[key] = lst = new List<string>();
            lst.AddRange(typeNames);
        }

        if (methodGroups.Count > 0)
            ancientOptions[ancientId] = methodGroups;
    }

    // cardClasses の UPPER_SNAKE 形式セット（カード判定用）
    var cardClassesSnake = new HashSet<string>(cardClasses.Select(CamelToUpperSnake), StringComparer.OrdinalIgnoreCase);

    var optionsOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "ancient_options.json");
    var ancientEntries = ancientOptions.OrderBy(kv => kv.Key).Select(kv =>
    {
        var methodEntries = kv.Value.OrderBy(m => m.Key).Select(m =>
        {
            var itemStrs = m.Value
                .Select(t => CamelToUpperSnake(t))
                .Distinct()
                .OrderBy(t => t)
                .Select(id => cardClassesSnake.Contains(id) ? $"      \"CARD.{id}\"" : $"      \"{id}\"");
            return $"    \"{m.Key}\": [\n{string.Join(",\n", itemStrs)}\n    ]";
        });
        return $"  \"{kv.Key}\": {{\n{string.Join(",\n", methodEntries)}\n  }}";
    });
    WriteJson(optionsOutPath, "{\n" + string.Join(",\n", ancientEntries) + "\n}\n");
    Console.Error.WriteLine($"Extracted Ancient options for {ancientOptions.Count} Ancients.");
    Console.WriteLine(optionsOutPath);

    // ---- ancient_acts.json ----（アクト配置。event_acts と同じ CollectGenericArgRefs パターン）
    // 各 Act クラスの get_AllAncients が Add<AncientType>() で登場 Ancient を登録する。
    //   region→act 番号: OVERGROWTH/UNDERDOCKS=1, HIVE=2, GLORY=3（Neow は Act1 の両 region に現れる→最小採用）。
    //   Darv/TheArchitect はどのアクトの AllAncients にも無い特殊扱い→出力しない（act=null）。
    var regionToAct = new Dictionary<string, int>(StringComparer.Ordinal)
        { ["OVERGROWTH"] = 1, ["UNDERDOCKS"] = 1, ["HIVE"] = 2, ["GLORY"] = 3 };

    var ancientAct = new Dictionary<string, int>(StringComparer.Ordinal);          // ANCIENT_ID -> act番号
    foreach (var th in mr.TypeDefinitions)
    {
        var td = mr.GetTypeDefinition(th);
        if (mr.GetString(td.Namespace) != "MegaCrit.Sts2.Core.Models.Acts") continue;
        var region = CamelToUpperSnake(mr.GetString(td.Name));
        if (!regionToAct.TryGetValue(region, out var actNo)) continue;
        foreach (var anc in CollectGenericArgRefs(mr, peReader, td, n => n == "get_AllAncients"))
        {
            var id = CamelToUpperSnake(anc);
            if (!ancientAct.TryGetValue(id, out var cur) || actNo < cur) ancientAct[id] = actNo;
        }
    }

    var ancientActEntries = ancientAct.OrderBy(kv => kv.Key, StringComparer.Ordinal)
        .Select(kv => $"  \"{kv.Key}\": {{ \"act\": {kv.Value} }}");
    var ancientActsOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "ancient_acts.json");
    WriteJson(ancientActsOutPath, "{\n" + string.Join(",\n", ancientActEntries) + "\n}\n");
    Console.Error.WriteLine($"Extracted Ancient act placement for {ancientAct.Count} Ancients.");
    Console.WriteLine(ancientActsOutPath);

    // ---- ancient_images.json（Ancient ID → ancients 内の主画像相対パス）----（event_images と同形）
    // Ancient 画像は現状プレースホルダのみ（{id}_placeholder.png.import）。
    // 汎用フォールバックの under_construction は Ancient ID ではないため除外。
    // 値はソース相対ファイル名（_placeholder 付き、例 "orobas_placeholder.png"）。
    var ancRepoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(outPath)!, "..", "..", ".."));
    var ancientsDir = Path.Combine(ancRepoRoot, "tools", "extracted", "images", "ancients");
    var ancImg = new SortedDictionary<string, string>(StringComparer.Ordinal);
    if (Directory.Exists(ancientsDir))
    {
        const string suffix = "_placeholder.png.import";
        foreach (var path in Directory.GetFiles(ancientsDir, "*" + suffix))
        {
            var file = Path.GetFileName(path);
            var stem = file[..^suffix.Length];                 // "orobas" / "the_architect"
            if (stem.Equals("under_construction", StringComparison.OrdinalIgnoreCase)) continue;
            ancImg[stem.ToUpperInvariant()] = stem + "_placeholder.png";
        }
    }
    var ancImgOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "ancient_images.json");
    WriteJson(ancImgOutPath,
        "{\n" + string.Join(",\n", ancImg.Select(kv => $"  \"{kv.Key}\": \"{kv.Value}\"")) + "\n}\n");
    Console.Error.WriteLine($"Extracted {ancImg.Count} ancient image paths.");
    Console.WriteLine(ancImgOutPath);
}

// ---- character_colors.json ----（キャラクターの識別色＋UIパレットを DLL から抽出）
// 各 CharacterModel（Models.Characters）の Color プロパティから取得：
//   NameColor      = Helpers.StsColors の名前付き定数（red/green/blue/purple/orange）。StsColors.cctor の hex を解決。
//   MapDrawingColor / DialogueColor / EnergyLabelOutlineColor / RemoteTargetingLineColor
//                  = Godot `new Color("hex")` の hex 文字列（ldstr）。8桁(RRGGBBAA)は RRGGBB に正規化。
{
    var outDirC = Path.GetDirectoryName(outPath)!;
    const string charNs = "MegaCrit.Sts2.Core.Models.Characters";
    var charClasses = new[] { "Ironclad", "Silent", "Defect", "Necrobinder", "Regent" };

    string? ReadUserString(int tok)
    {
        try { return mr.GetUserString(MetadataTokens.UserStringHandle(tok & 0xFFFFFF)); } catch { return null; }
    }
    int Tok(byte[] il, int i) => il[i+1] | (il[i+2]<<8) | (il[i+3]<<16) | (il[i+4]<<24);
    byte[]? GetterIl(TypeDefinition td, string getter)
    {
        foreach (var mh in td.GetMethods())
        {
            var md = mr.GetMethodDefinition(mh);
            if (mr.GetString(md.Name) != getter || md.RelativeVirtualAddress == 0) continue;
            return peReader.GetMethodBody(md.RelativeVirtualAddress)?.GetILBytes();
        }
        return null;
    }
    string? FirstLdstr(TypeDefinition td, string getter)
    {
        var il = GetterIl(td, getter); if (il == null) return null;
        for (int i = 0; i + 4 < il.Length; i++)
            if (il[i] == 0x72) { var s = ReadUserString(Tok(il, i)); if (!string.IsNullOrEmpty(s)) return s; }
        return null;
    }
    string? FirstLdsfld(TypeDefinition td, string getter)
    {
        var il = GetterIl(td, getter); if (il == null) return null;
        for (int i = 0; i + 4 < il.Length; i++)
            if (il[i] == 0x7E) { var n = ResolveFieldToken(mr, Tok(il, i)); if (!string.IsNullOrEmpty(n)) return n; }
        return null;
    }
    static string NormHex(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var h = raw.Trim().TrimStart('#').ToUpperInvariant();
        if (h.Length == 8) h = h[..6];        // RRGGBBAA → RRGGBB
        return h.Length == 6 ? "#" + h : "";
    }

    // StsColors の名前付き定数 → hex（.cctor の「ldstr "hex" … stsfld 名前」を対応付け）
    var stsColors = new Dictionary<string, string>(StringComparer.Ordinal);
    var charTds = new Dictionary<string, TypeDefinition>(StringComparer.Ordinal);
    foreach (var th in mr.TypeDefinitions)
    {
        var td = mr.GetTypeDefinition(th);
        var nm = mr.GetString(td.Name);
        if (mr.GetString(td.Namespace) == charNs && charClasses.Contains(nm)) charTds[nm] = td;
        if (nm != "StsColors") continue;
        var il = GetterIl(td, ".cctor"); if (il == null) continue;
        string? lastStr = null;
        for (int i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] == 0x72) lastStr = ReadUserString(Tok(il, i));
            else if (il[i] == 0x80) // stsfld
            {
                var fn = ResolveFieldToken(mr, Tok(il, i));
                if (!string.IsNullOrEmpty(fn) && lastStr != null) stsColors.TryAdd(fn, lastStr);
                lastStr = null;
            }
        }
    }

    var colorEntries = new List<string>();
    foreach (var cn in charClasses)
    {
        if (!charTds.TryGetValue(cn, out var td)) continue;
        var id        = CamelToUpperSnake(cn);
        var nameField = FirstLdsfld(td, "get_NameColor") ?? "";
        var nameHex   = stsColors.TryGetValue(nameField, out var nh) ? NormHex(nh) : "";
        var map       = NormHex(FirstLdstr(td, "get_MapDrawingColor"));
        var dialogue  = NormHex(FirstLdstr(td, "get_DialogueColor"));
        var energy    = NormHex(FirstLdstr(td, "get_EnergyLabelOutlineColor"));
        var targeting = NormHex(FirstLdstr(td, "get_RemoteTargetingLineColor"));
        colorEntries.Add(
            $"  \"{id}\": {{ \"name\": \"{nameField}\", \"nameColor\": \"{nameHex}\", " +
            $"\"mapDrawingColor\": \"{map}\", \"dialogueColor\": \"{dialogue}\", " +
            $"\"energyOutlineColor\": \"{energy}\", \"targetingLineColor\": \"{targeting}\" }}");
    }
    var colorsOutPath = Path.Combine(outDirC, "character_colors.json");
    WriteJson(colorsOutPath, "{\n" + string.Join(",\n", colorEntries) + "\n}\n");
    Console.Error.WriteLine($"Extracted character colors for {colorEntries.Count} characters.");
    Console.WriteLine(colorsOutPath);
}

// merchant_prices.json 出力
// マーチャント（店）の価格ロジックを DLL の IL から抽出する。
//   ・カード/ポーション基本価格（レアリティ別）  … *Entry.GetCost のリテラル
//   ・無色カードのマークアップ・変動率・セール率   … *Entry.GetCost / CalcCost の ldc.r4
//   ・レリック基本価格（レアリティ別）            … RelicModel.get_MerchantCost のスイッチ
//   ・カード除去コスト（基本値・増分・アセンション） … MerchantCardRemovalEntry.get_BaseCost / get_PriceIncrease
// 数値はゲーム更新で変わり得るが、構造（レアリティの大小関係・スイッチ）は安定なので
// リテラルを IL から拾い、スロット（レアリティ）への割り当てのみ既知の規則で行う。
{
    var outDirM = Path.GetDirectoryName(outPath)!;

    // 指定型・メソッドの IL を返す（最初に一致したもの）
    byte[] MIL(string typeName, string methodName)
    {
        foreach (var th in mr.TypeDefinitions)
        {
            var td = mr.GetTypeDefinition(th);
            if (mr.GetString(td.Name) != typeName) continue;
            foreach (var mh in td.GetMethods())
            {
                var m = mr.GetMethodDefinition(mh);
                if (mr.GetString(m.Name) != methodName || m.RelativeVirtualAddress == 0) continue;
                return peReader.GetMethodBody(m.RelativeVirtualAddress)?.GetILBytes() ?? Array.Empty<byte>();
            }
        }
        return Array.Empty<byte>();
    }

    // IL を命令単位で走査し ldc.i4（整数）と ldc.r4（float）を文書順で収集
    (List<int> Ints, List<float> Floats) ScanConsts(byte[] il)
    {
        var ints = new List<int>();
        var floats = new List<float>();
        for (int i = 0; i < il.Length;)
        {
            if (il[i] == 0x22 && i + 4 < il.Length) { floats.Add(BitConverter.ToSingle(il, i + 1)); i += 5; continue; }
            if (il[i] == 0x23 && i + 8 < il.Length) { i += 9; continue; } // ldc.r8 は skip
            var (v, sz) = ReadLdcI4(il, i);
            if (v.HasValue) ints.Add(v.Value);
            i += sz;
        }
        return (ints, floats);
    }

    // _cost / N（セール半額）の除数 N を返す: div(0x5B) 直前の ldc.i4
    int FindDivisor(byte[] il)
    {
        int? last = null;
        for (int i = 0; i < il.Length;)
        {
            if (il[i] == 0x5B) return last ?? 0;             // div
            if (il[i] == 0x22) { i += 5; continue; }         // ldc.r4
            var (v, sz) = ReadLdcI4(il, i);
            if (v.HasValue) last = v;
            i += sz;
        }
        return 0;
    }

    string F(float v) => v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    // RelicRarity enum: int → 名前
    var relicRarityById = new Dictionary<int, string>();
    foreach (var th in mr.TypeDefinitions)
    {
        var td = mr.GetTypeDefinition(th);
        if (mr.GetString(td.Name) != "RelicRarity") continue;
        foreach (var fh in td.GetFields())
        {
            var f = mr.GetFieldDefinition(fh);
            var fn = mr.GetString(f.Name);
            if (fn == "value__") continue;
            var c = f.GetDefaultValue(); if (c.IsNil) continue;
            relicRarityById[mr.GetBlobReader(mr.GetConstant(c).Value).ReadInt32()] = fn;
        }
        break;
    }

    // --- カード基本価格（MerchantCardEntry.GetCost）---
    // ldc.i4 のうち価格（>=10）を降順に並べ Rare > Uncommon > Common に割り当て。
    // 無色マークアップは末尾の ldc.r4（×1.15）。
    var (cardInts, cardFloats) = ScanConsts(MIL("MerchantCardEntry", "GetCost"));
    var cardPrices = cardInts.Where(v => v >= 10).Distinct().OrderByDescending(v => v).ToList();
    float colorlessMarkup = cardFloats.Count > 0 ? cardFloats.Max() : 1f;
    // カード変動率（CalcCost の ldc.r4 ペア）・セール率（_cost / N）
    var cardCalc = MIL("MerchantCardEntry", "CalcCost");
    var cardVarFloats = ScanConsts(cardCalc).Floats;
    int cardSaleDiv = FindDivisor(cardCalc);

    // --- ポーション基本価格（MerchantPotionEntry.GetCost）+ 変動率（CalcCost）---
    var potionPrices = ScanConsts(MIL("MerchantPotionEntry", "GetCost")).Ints
        .Where(v => v >= 10).Distinct().OrderByDescending(v => v).ToList();
    var potionVarFloats = ScanConsts(MIL("MerchantPotionEntry", "CalcCost")).Floats;

    // --- レリック基本価格（RelicModel.get_MerchantCost のスイッチ）+ 変動率（MerchantRelicEntry.CalcCost）---
    // get_Rarity の値を直接 switch する（index = RelicRarity の int 値）。
    // ジャンプテーブルの各 case body 先頭 ldc.i4 が基本価格。Common/Uncommon/Rare/Shop のみ採用。
    var relicBase = new Dictionary<string, int>(StringComparer.Ordinal);
    var rmIl = MIL("RelicModel", "get_MerchantCost");
    for (int i = 0; i + 4 < rmIl.Length; i++)
    {
        if (rmIl[i] != 0x45) continue;                       // switch
        int count = rmIl[i+1] | (rmIl[i+2]<<8) | (rmIl[i+3]<<16) | (rmIl[i+4]<<24);
        int tableStart = i + 5;
        int afterTable = tableStart + count * 4;
        for (int r = 0; r < count && afterTable + 4 <= rmIl.Length; r++)
        {
            int off = BitConverter.ToInt32(rmIl, tableStart + r * 4);
            int target = afterTable + off;
            if (target < 0 || target >= rmIl.Length) continue;
            var (val, _) = ReadLdcI4(rmIl, target);
            if (val.HasValue && relicRarityById.TryGetValue(r, out var rn)
                && rn is "Common" or "Uncommon" or "Rare" or "Shop")
                relicBase[rn] = val.Value;
        }
        break;
    }
    var relicVarFloats = ScanConsts(MIL("MerchantRelicEntry", "CalcCost")).Floats;

    // --- カード除去コスト（GetValueIfAscension(threshold, normal, ascension)）---
    var baseCostInts = ScanConsts(MIL("MerchantCardRemovalEntry", "get_BaseCost")).Ints;
    var priceIncInts = ScanConsts(MIL("MerchantCardRemovalEntry", "get_PriceIncrease")).Ints;

    // 価格を Rare/Uncommon/Common の3スロットへ（>=3件なら先頭3つ）
    string RarityMap(List<int> prices) =>
        prices.Count >= 3
            ? $"{{ \"Rare\": {prices[0]}, \"Uncommon\": {prices[1]}, \"Common\": {prices[2]} }}"
            : "{ }";

    var relicBaseJson = string.Join(", ",
        new[] { "Common", "Uncommon", "Rare", "Shop" }
            .Where(relicBase.ContainsKey)
            .Select(k => $"\"{k}\": {relicBase[k]}"));

    var sb = new System.Text.StringBuilder();
    sb.AppendLine("{");
    sb.AppendLine($"  \"card\": {{");
    sb.AppendLine($"    \"base\": {RarityMap(cardPrices)},");
    sb.AppendLine($"    \"colorlessMarkup\": {F(colorlessMarkup)},");
    sb.AppendLine($"    \"saleMultiplier\": {(cardSaleDiv > 0 ? F(1f / cardSaleDiv) : "1")},");
    sb.AppendLine($"    \"variance\": {{ \"min\": {(cardVarFloats.Count > 0 ? F(cardVarFloats.Min()) : "1")}, \"max\": {(cardVarFloats.Count > 0 ? F(cardVarFloats.Max()) : "1")} }}");
    sb.AppendLine($"  }},");
    sb.AppendLine($"  \"potion\": {{");
    sb.AppendLine($"    \"base\": {RarityMap(potionPrices)},");
    sb.AppendLine($"    \"variance\": {{ \"min\": {(potionVarFloats.Count > 0 ? F(potionVarFloats.Min()) : "1")}, \"max\": {(potionVarFloats.Count > 0 ? F(potionVarFloats.Max()) : "1")} }}");
    sb.AppendLine($"  }},");
    sb.AppendLine($"  \"relic\": {{");
    sb.AppendLine($"    \"base\": {{ {relicBaseJson} }},");
    sb.AppendLine($"    \"variance\": {{ \"min\": {(relicVarFloats.Count > 0 ? F(relicVarFloats.Min()) : "1")}, \"max\": {(relicVarFloats.Count > 0 ? F(relicVarFloats.Max()) : "1")} }}");
    sb.AppendLine($"  }},");
    sb.AppendLine($"  \"cardRemoval\": {{");
    sb.AppendLine($"    \"ascensionThreshold\": {(baseCostInts.Count >= 3 ? baseCostInts[0] : 0)},");
    sb.AppendLine($"    \"baseCost\": {(baseCostInts.Count >= 3 ? baseCostInts[1] : 0)},");
    sb.AppendLine($"    \"baseCostAscension\": {(baseCostInts.Count >= 3 ? baseCostInts[2] : 0)},");
    sb.AppendLine($"    \"priceIncrease\": {(priceIncInts.Count >= 3 ? priceIncInts[1] : 0)},");
    sb.AppendLine($"    \"priceIncreaseAscension\": {(priceIncInts.Count >= 3 ? priceIncInts[2] : 0)}");
    sb.AppendLine($"  }}");
    sb.AppendLine("}");

    var merchantOutPath = Path.Combine(outDirM, "merchant_prices.json");
    WriteJson(merchantOutPath, sb.ToString());
    Console.Error.WriteLine($"Merchant prices: card={RarityMap(cardPrices)} colorless×{F(colorlessMarkup)} " +
        $"sale÷{cardSaleDiv} | potion={RarityMap(potionPrices)} | relic={{{relicBaseJson}}} | " +
        $"removal base={(baseCostInts.Count >= 3 ? baseCostInts[1] : 0)}(+{(priceIncInts.Count >= 3 ? priceIncInts[1] : 0)}/use)");
    Console.WriteLine(merchantOutPath);
}

// ---- helpers ----

// get_CanonicalKeywords の IL を解析してキーワード int 値のリストを返す
// パターンA (1件): ldc.i4 N + newobj + ret
// パターンB (N件): ldc.i4 count + newarr(5byte) + [dup + ldc_idx + ldc_val + stelem.i4]* + newobj + ret
// パターンC (InitializeArray): 未対応 (0xD0 で始まるケース) → 空リストを返す
static List<int> ParseCanonicalKeywords(byte[] il)
{
    var result = new List<int>();
    if (il.Length < 2) return result;

    int pos = 0;
    var (first, firstSize) = ReadLdcI4(il, pos);
    if (!first.HasValue) return result;
    pos += firstSize;

    if (pos >= il.Length) return result;

    if (il[pos] == 0x73) // newobj → パターンA: single keyword
    {
        result.Add(first.Value);
        return result;
    }

    if (il[pos] == 0x8D) // newarr → パターンB: keyword array
    {
        pos += 5; // newarr opcode (1) + type token (4)
        int count = first.Value;

        for (int elem = 0; elem < count && pos < il.Length; elem++)
        {
            if (pos < il.Length && il[pos] == 0x25) pos++; // dup
            // index
            var (idx, idxSize) = ReadLdcI4(il, pos);
            if (!idx.HasValue) break;
            pos += idxSize;
            // value
            var (val, valSize) = ReadLdcI4(il, pos);
            if (!val.HasValue) break;
            pos += valSize;
            result.Add(val.Value);
            if (pos < il.Length && il[pos] == 0x9E) pos++; // stelem.i4
        }
    }

    return result;
}

static string CamelToUpperSnake(string name)
{
    // BeaconOfHope → BEACON_OF_HOPE
    var result = Regex.Replace(name, @"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", "_");
    return result.ToUpperInvariant();
}

// JSON 出力は .gitattributes (*.json eol=crlf) に合わせ CRLF で書き出す。
// 既存の '\n' 直書き出力を一括 CRLF 化し、extractor 実行後の EOL 差分(M 表示)再発を防ぐ。
static void WriteJson(string path, string content) =>
    File.WriteAllText(path, content.Replace("\r\n", "\n").Replace("\n", "\r\n"));

// UPPER_SNAKE → Title Case（例 KAISER_CRAB → Kaiser Crab）。loc に名前が無い場合のフォールバック。
static string SnakeToTitle(string snake) =>
    string.Join(' ', snake.Split('_')
        .Select(w => w.Length == 0 ? w : char.ToUpper(w[0]) + w[1..].ToLowerInvariant()));

// ローカライズ JSON の "{PREFIX}{suffix}" キー（PREFIX に追加のドット無し）を PREFIX→値 辞書にする。
static Dictionary<string, string> LoadLocBySuffix(string path, string suffix)
{
    var map = new Dictionary<string, string>(StringComparer.Ordinal);
    if (!File.Exists(path)) return map;
    using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
    foreach (var p in doc.RootElement.EnumerateObject())
    {
        if (!p.Name.EndsWith(suffix, StringComparison.Ordinal)) continue;
        var prefix = p.Name[..^suffix.Length];
        if (prefix.Contains('.')) continue;
        map[prefix] = p.Value.GetString() ?? "";
    }
    return map;
}

// 指定型の、名前が match するメソッドの IL を走査し、call/callvirt の MethodSpec 第一型引数から
// 参照型名（例: ジェネリック Add&lt;Axebot&gt;() の "Axebot"）を収集する。
// encounter→monster / act→event 抽出に使用（ancient_options 抽出と同じ手法）。
static List<string> CollectGenericArgRefs(MetadataReader mr, PEReader peReader, TypeDefinition td, Func<string, bool> match)
{
    var refs = new List<string>();
    foreach (var mh in td.GetMethods())
    {
        var md = mr.GetMethodDefinition(mh);
        if (!match(mr.GetString(md.Name))) continue;
        if (md.RelativeVirtualAddress == 0) continue;
        var il = peReader.GetMethodBody(md.RelativeVirtualAddress)?.GetILBytes();
        if (il == null) continue;
        for (int i = 0; i + 4 < il.Length; i++)
        {
            byte op = il[i];
            if (op is 0x28 or 0x6F) // call / callvirt
            {
                int tok = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                var tn = ResolveMethodSpecFirstArg(mr, tok);
                if (!string.IsNullOrEmpty(tn) && tn.Length > 2 && !tn.Contains('<'))
                    refs.Add(tn);
                i += 4;
            }
        }
    }
    return refs;
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

static string ResolveMethodName(MetadataReader mr, int token)
{
    try
    {
        var h = MetadataTokens.EntityHandle(token);
        if (h.Kind == HandleKind.MethodDefinition)
            return mr.GetString(mr.GetMethodDefinition((MethodDefinitionHandle)h).Name);
        if (h.Kind == HandleKind.MemberReference)
            return mr.GetString(mr.GetMemberReference((MemberReferenceHandle)h).Name);
    }
    catch { }
    return "";
}

static string ResolveFieldToken(MetadataReader mr, int token)
{
    try
    {
        var h = MetadataTokens.EntityHandle(token);
        if (h.Kind == HandleKind.FieldDefinition)
            return mr.GetString(mr.GetFieldDefinition((FieldDefinitionHandle)h).Name);
        if (h.Kind == HandleKind.MemberReference)
            return mr.GetString(mr.GetMemberReference((MemberReferenceHandle)h).Name);
    }
    catch { }
    return "";
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

// newobj のオペランド（.ctor トークン）から、生成される型の単純名を解決する。
// 例: newobj CunningPotion::.ctor() → "CunningPotion"。同一アセンブリ内は MethodDefinition、
// 外部参照は MemberReference(Parent=TypeReference/TypeDefinition) を辿る。
static string ResolveCtorDeclaringType(MetadataReader mr, int token)
{
    try
    {
        var h = MetadataTokens.EntityHandle(token);
        if (h.Kind == HandleKind.MethodDefinition)
        {
            var md = mr.GetMethodDefinition((MethodDefinitionHandle)h);
            return mr.GetString(mr.GetTypeDefinition(md.GetDeclaringType()).Name);
        }
        if (h.Kind == HandleKind.MemberReference)
        {
            var parent = mr.GetMemberReference((MemberReferenceHandle)h).Parent;
            if (parent.Kind == HandleKind.TypeDefinition)
                return mr.GetString(mr.GetTypeDefinition((TypeDefinitionHandle)parent).Name);
            if (parent.Kind == HandleKind.TypeReference)
                return mr.GetString(mr.GetTypeReference((TypeReferenceHandle)parent).Name);
        }
    }
    catch { }
    return "";
}
