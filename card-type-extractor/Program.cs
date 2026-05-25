using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

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
var outPath = args.Length > 1 ? args[1] : defaultOut;
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
var rarities      = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var cardStats     = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
var starCostCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
// フェーズB: TypeSpec Var (Vulnerable, Doom 等) — DynamicVarSet の castclass トークンから逆引き
{
    var typeSpecToPropName = new Dictionary<int, string>();
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
                if (iv[i] != 0x74) continue;
                int castTok = iv[i+1]|(iv[i+2]<<8)|(iv[i+3]<<16)|(iv[i+4]<<24);
                if (MetadataTokens.EntityHandle(castTok).Kind == HandleKind.TypeSpecification)
                    typeSpecToPropName.TryAdd(castTok, mn[4..]);
                break;
            }
        }
        break;
    }
    foreach (var mrh in mr.MemberReferences)
    {
        var memberRef = mr.GetMemberReference(mrh);
        if (mr.GetString(memberRef.Name) != ".ctor") continue;
        if (memberRef.Parent.Kind != HandleKind.TypeSpecification) continue;
        int parentTok = MetadataTokens.GetToken(memberRef.Parent);
        if (!typeSpecToPropName.TryGetValue(parentTok, out var propName)) continue;
        varNameByCtorToken.TryAdd(MetadataTokens.GetToken(mr, mrh), propName);
    }
}

// System.Decimal..ctor(int) のトークン
const int decimalIntCtorToken = 0x0A001317;

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
    // パターン: ldc.i4 N → newobj Decimal..ctor → [ldc.i4 M] → newobj XxxVar..ctor
    foreach (var mh in typeDef.GetMethods())
    {
        var method = mr.GetMethodDefinition(mh);
        if (mr.GetString(method.Name) != "get_CanonicalVars") continue;
        if (method.RelativeVirtualAddress == 0) continue;
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        if (body == null) continue;
        var il = body.GetILBytes();

        int? lastInt = null;
        int? pendingDecimal = null;
        var canonFields = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < il.Length; )
        {
            byte op = il[i];
            if (op == 0x73 && i + 4 < il.Length) // newobj
            {
                int tok = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                if (tok == decimalIntCtorToken)
                {
                    pendingDecimal = lastInt;
                    lastInt = null;
                }
                else if (pendingDecimal.HasValue && varNameByCtorToken.TryGetValue(tok, out var vname))
                {
                    canonFields.TryAdd(vname, pendingDecimal.Value);
                    pendingDecimal = null;
                    lastInt = null;
                }
                i += 5;
                continue;
            }
            var (val, size) = ReadLdcI4(il, i);
            if (val.HasValue) lastInt = val;
            else if (op is not (0x02 or 0x25)) lastInt = null; // ldarg.0/dup はリセットしない
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

    // get_CanonicalVars → カードと同じパターン: ldc.i4 N → newobj Decimal → newobj XxxVar
    foreach (var mh in typeDef.GetMethods())
    {
        var method = mr.GetMethodDefinition(mh);
        if (mr.GetString(method.Name) != "get_CanonicalVars") continue;
        if (method.RelativeVirtualAddress == 0) continue;
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        if (body == null) continue;
        var il = body.GetILBytes();

        int? lastIntR = null;
        int? pendingDecimalR = null;
        var canonFields = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < il.Length; )
        {
            byte op = il[i];
            if (op == 0x73 && i + 4 < il.Length) // newobj
            {
                int tok = il[i+1]|(il[i+2]<<8)|(il[i+3]<<16)|(il[i+4]<<24);
                if (tok == decimalIntCtorToken)
                {
                    pendingDecimalR = lastIntR;
                    lastIntR = null;
                }
                else if (pendingDecimalR.HasValue && varNameByCtorToken.TryGetValue(tok, out var vname))
                {
                    canonFields.TryAdd(vname, pendingDecimalR.Value);
                    pendingDecimalR = null;
                    lastIntR = null;
                }
                i += 5;
                continue;
            }
            var (valR, sizeR) = ReadLdcI4(il, i);
            if (valR.HasValue) lastIntR = valR;
            else if (op is not (0x02 or 0x25)) lastIntR = null;
            i += sizeR;
        }
        if (canonFields.Count > 0)
            relicStats[relicId] = canonFields;
        break;
    }
}

// relic_rarities.json 出力 → StS2Shared/Resources/
var relicRaritiesOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "relic_rarities.json");
Console.Error.WriteLine($"Extracted {relicRarities.Count} relic rarity mappings.");
var relicRarityLines = relicRarities.OrderBy(kv => kv.Key)
    .Select(kv => $"  \"{kv.Key}\": \"{kv.Value}\"");
File.WriteAllText(relicRaritiesOutPath, "{\n" + string.Join(",\n", relicRarityLines) + "\n}\n");
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
File.WriteAllText(relicStatsOutPath, "{\n" + string.Join(",\n", relicStatsEntries) + "\n}\n");
Console.WriteLine(relicStatsOutPath);

// card_characters.json 出力
var charsOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "card_characters.json");
Console.Error.WriteLine($"Extracted {cardCharacters.Count} card character mappings.");
var charLines = cardCharacters.OrderBy(kv => kv.Key)
    .Select(kv => $"  \"{kv.Key}\": \"{kv.Value}\"");
File.WriteAllText(charsOutPath, "{\n" + string.Join(",\n", charLines) + "\n}\n");
Console.WriteLine(charsOutPath);

// card_types.json 出力
Console.Error.WriteLine($"Extracted {results.Count} card type mappings.");
var jsonLines = results.OrderBy(kv => kv.Key)
    .Select(kv => $"  \"{kv.Key}\": \"{kv.Value}\"");
File.WriteAllText(outPath, "{\n" + string.Join(",\n", jsonLines) + "\n}\n");
Console.WriteLine(outPath);

// card_costs.json 出力
var costsOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "card_costs.json");
Console.Error.WriteLine($"Extracted {costs.Count} card cost mappings.");
var costLines = costs.OrderBy(kv => kv.Key)
    .Select(kv => $"  \"{kv.Key}\": {kv.Value}");
File.WriteAllText(costsOutPath, "{\n" + string.Join(",\n", costLines) + "\n}\n");
Console.WriteLine(costsOutPath);

// card_rarities.json 出力
var raritiesOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "card_rarities.json");
Console.Error.WriteLine($"Extracted {rarities.Count} card rarity mappings.");
var rarityLines = rarities.OrderBy(kv => kv.Key)
    .Select(kv => $"  \"{kv.Key}\": \"{kv.Value}\"");
File.WriteAllText(raritiesOutPath, "{\n" + string.Join(",\n", rarityLines) + "\n}\n");
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
File.WriteAllText(statsOutPath, "{\n" + string.Join(",\n", statsEntries) + "\n}\n");
Console.WriteLine(statsOutPath);

// card_star_costs.json 出力 (Starをコストとして消費するカードの ID リスト)
var starCostsOutPath = Path.Combine(Path.GetDirectoryName(outPath)!, "card_star_costs.json");
Console.Error.WriteLine($"Extracted {starCostCards.Count} star-cost card mappings.");
var starCostLines = starCostCards.OrderBy(id => id).Select(id => $"  \"{id}\"");
File.WriteAllText(starCostsOutPath, "[\n" + string.Join(",\n", starCostLines) + "\n]\n");
Console.WriteLine(starCostsOutPath);

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
    File.WriteAllText(optionsOutPath, "{\n" + string.Join(",\n", ancientEntries) + "\n}\n");
    Console.Error.WriteLine($"Extracted Ancient options for {ancientOptions.Count} Ancients.");
    Console.WriteLine(optionsOutPath);
}

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
