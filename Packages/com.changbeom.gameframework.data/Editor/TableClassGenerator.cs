using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameFramework.DataParsing.Editor
{
    public static class TableClassGenerator
    {
        public class ColumnInfo
        {
            public int colIndex;
            public string columnName;
            public string fieldName;
            public EDataTableColumnType type;
            public bool isArray;

            /// <summary>정규화된(fully-qualified) enum 타입 이름입니다 (type == Enum일 때만 설정됨).</summary>
            public string enumTypeFullName;

            /// <summary>"key:EnumName" 컬럼일 때만 설정됩니다. 필드 타입은 계속 string으로
            /// 남고(enum: 컬럼과 다름), 이 값이 있으면 WriteTableScript가 생성 클래스에
            /// Get(EnumName key) 오버로드를 추가하고, SyncKeyEnums가 이 컬럼의 실제 시트 값들로
            /// 그 EnumName의 .cs 파일을 자동으로 만들거나 갱신합니다.</summary>
            public string keyEnumName;
        }

        public static bool TryExtractColumnsFromTsv(string tsv, out List<ColumnInfo> columns, out string error)
        {
            columns = new List<ColumnInfo>();
            error = string.Empty;

            TsvTable table = TsvParser.Parse(tsv);

            if (table == null)
            {
                error = "TSV 파싱 결과 테이블이 null입니다.";
                return false;
            }

            if (table.RowCount < 4)
            {
                error = "행이 부족합니다.";
                return false;
            }

            int colCount = table.ColCount;

            for (int c = 1; c < colCount; c++)
            {
                string name = table.GetCell(0, c).Trim();

                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (name.StartsWith("~"))
                {
                    continue;
                }

                string rawTypeText = table.GetCell(2, c).Trim();

                bool isArray = rawTypeText.EndsWith("[]");
                string baseTypeText = isArray ? rawTypeText.Substring(0, rawTypeText.Length - 2) : rawTypeText;

                EDataTableColumnType type;
                string enumTypeFullName = null;
                string keyEnumName = null;

                if (baseTypeText.StartsWith("enum:", StringComparison.OrdinalIgnoreCase))
                {
                    // enum 타입 이름은 대소문자를 구분하므로, 이 분기에서는 소문자로 바꾸면 안 됩니다.
                    string enumTypeName = baseTypeText.Substring("enum:".Length).Trim();

                    Type resolvedType;
                    if (!TryFindEnumType(enumTypeName, out resolvedType, out error))
                    {
                        error += " (col=" + (c + 1) + ", name=" + name + ")";
                        return false;
                    }

                    type = EDataTableColumnType.Enum;
                    enumTypeFullName = resolvedType.FullName.Replace('+', '.');
                }
                else if (baseTypeText.StartsWith("key:", StringComparison.OrdinalIgnoreCase))
                {
                    // "enum:"과 달리 이 시점엔 enum 타입이 존재할 필요가 없습니다(오히려
                    // 존재하지 않는 게 정상인 첫 생성 경우가 있습니다). 필드는 계속 string으로
                    // 남습니다 - SyncKeyEnums가 이 컬럼의 실제 시트 값들로 그 enum을 만들거나
                    // 갱신합니다(WriteTableScript 참고).
                    keyEnumName = baseTypeText.Substring("key:".Length).Trim();

                    if (string.IsNullOrEmpty(keyEnumName))
                    {
                        error = "\"key:\" 뒤에 생성할 enum 이름이 필요합니다 (col=" + (c + 1) + ", name=" + name + ")";
                        return false;
                    }

                    if (isArray)
                    {
                        error = "\"key:\" 컬럼은 배열(\"[]\")을 지원하지 않습니다 (col=" + (c + 1) + ", name=" + name + ")";
                        return false;
                    }

                    for (int i = 0; i < columns.Count; i++)
                    {
                        if (columns[i].keyEnumName == keyEnumName)
                        {
                            error = "\"key:" + keyEnumName + "\"이(가) 이미 다른 컬럼(\"" + columns[i].columnName + "\")에서 쓰이고 있습니다. 한 enum 이름은 테이블당 한 컬럼에만 쓸 수 있습니다 (col=" + (c + 1) + ", name=" + name + ")";
                            return false;
                        }
                    }

                    type = EDataTableColumnType.String;
                }
                else if (!TryParseType(baseTypeText.ToLowerInvariant(), out type))
                {
                    error = "알 수 없는 타입: " + rawTypeText + " (col=" + (c + 1) + ", name=" + name + ")";
                    return false;
                }

                string fieldName = ToSafeFieldName(name);

                // 생성되는 Data 클래스는 컬럼 필드 옆에 RowKey 필드를 이미 고정으로
                // 선언합니다 (WriteTableScript 참고). 컬럼 이름이 sanitize 후 "RowKey"가
                // 되면 같은 클래스에 필드가 두 번 선언되어 생성 스크립트가 컴파일되지 않습니다.
                if (fieldName == "RowKey")
                {
                    error = "컬럼 이름 \"" + name + "\"는 sanitize 후 \"RowKey\"가 되어, 모든 테이블에 이미 고정으로 있는 RowKey 필드와 충돌합니다. 컬럼 이름을 다르게 바꿔주세요.";
                    return false;
                }

                for (int i = 0; i < columns.Count; i++)
                {
                    if (columns[i].columnName == name)
                    {
                        error = "중복 컬럼 이름: " + name;
                        return false;
                    }

                    // 컬럼 이름 자체는 달라도(예: "Item Name" vs "Item-Name") sanitize 후
                    // 필드 이름이 같아지면, 생성되는 클래스에 같은 이름의 필드가 두 번
                    // 선언되어 컴파일이 깨집니다. sanitize된 이름 기준으로도 검사합니다.
                    if (columns[i].fieldName == fieldName)
                    {
                        error = "컬럼 이름 \"" + columns[i].columnName + "\"와(과) \"" + name +
                                "\"가 같은 필드 이름(\"" + fieldName + "\")으로 변환됩니다. 컬럼 이름을 다르게 바꿔주세요.";
                        return false;
                    }
                }

                ColumnInfo info = new ColumnInfo();
                info.colIndex = c;
                info.columnName = name;
                info.fieldName = fieldName;
                info.type = type;
                info.isArray = isArray;
                info.enumTypeFullName = enumTypeFullName;
                info.keyEnumName = keyEnumName;

                columns.Add(info);
            }

            if (columns.Count == 0)
            {
                error = "유효한 컬럼이 없습니다.";
                return false;
            }

            return true;
        }

        public static void WriteTableScript(string scriptPath, string className, List<ColumnInfo> columns)
        {
            StringBuilder sb = new StringBuilder(16 * 1024);

            List<ColumnInfo> keyColumns = columns.FindAll(c => c.keyEnumName != null);

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using GameFramework.DataParsing;");
            sb.AppendLine();
            sb.AppendLine("// 자동 생성됨. 직접 편집하지 마세요.");
            sb.AppendLine("public class " + className + " : ScriptableObject");
            sb.AppendLine("{");
            sb.AppendLine("    [SerializeField] private List<Data> _table = new List<Data>();");
            sb.AppendLine("    public IReadOnlyList<Data> Table => _table;");
            sb.AppendLine();
            sb.AppendLine("    private Dictionary<int, Data> _cache;");
            sb.AppendLine("    private bool _cacheBuilt;");

            for (int i = 0; i < keyColumns.Count; i++)
            {
                sb.AppendLine("    private Dictionary<string, Data> _keyCache_" + keyColumns[i].fieldName + ";");
            }

            sb.AppendLine();
            sb.AppendLine("    [Serializable]");
            sb.AppendLine("    public class Data");
            sb.AppendLine("    {");
            sb.AppendLine("        public int RowKey;");

            for (int i = 0; i < columns.Count; i++)
            {
                ColumnInfo col = columns[i];
                string csType = col.type == EDataTableColumnType.Enum
                    ? (col.isArray ? col.enumTypeFullName + "[]" : col.enumTypeFullName)
                    : ToCsType(col.type, col.isArray);
                sb.AppendLine("        public " + csType + " " + col.fieldName + ";");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public Data Get(int rowKey)");
            sb.AppendLine("    {");
            sb.AppendLine("        BuildCacheIfNeeded();");
            sb.AppendLine();
            sb.AppendLine("        Data d;");
            sb.AppendLine("        if (!_cache.TryGetValue(rowKey, out d))");
            sb.AppendLine("        {");
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        return d;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private void BuildCacheIfNeeded()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_cacheBuilt && _cache != null)");
            sb.AppendLine("        {");
            sb.AppendLine("            return;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        _cache = new Dictionary<int, Data>();");
            sb.AppendLine();
            sb.AppendLine("        for (int i = 0; i < _table.Count; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            Data d = _table[i];");
            sb.AppendLine("            if (d == null)");
            sb.AppendLine("            {");
            sb.AppendLine("                continue;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            _cache[d.RowKey] = d;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        _cacheBuilt = true;");
            sb.AppendLine("    }");
            sb.AppendLine();

            for (int i = 0; i < keyColumns.Count; i++)
            {
                AppendKeyAccessor(sb, keyColumns[i]);
            }

            sb.AppendLine("    public void ParseFromTsv(string tsv)");
            sb.AppendLine("    {");
            sb.AppendLine("        _table.Clear();");
            sb.AppendLine("        _cacheBuilt = false;");
            sb.AppendLine("        _cache = null;");

            for (int i = 0; i < keyColumns.Count; i++)
            {
                sb.AppendLine("        _keyCache_" + keyColumns[i].fieldName + " = null;");
            }

            sb.AppendLine();
            sb.AppendLine("        TsvTable table = TsvParser.Parse(tsv);");
            sb.AppendLine("        if (table == null)");
            sb.AppendLine("        {");
            sb.AppendLine("            return;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        if (table.RowCount < 4)");
            sb.AppendLine("        {");
            sb.AppendLine("            return;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        HashSet<int> usedRowKeys = new HashSet<int>();");
            sb.AppendLine();
            sb.AppendLine("        for (int r = 3; r < table.RowCount; r++)");
            sb.AppendLine("        {");
            sb.AppendLine("            string rowKeyText = table.GetCell(r, 0).Trim();");
            sb.AppendLine("            if (string.IsNullOrEmpty(rowKeyText))");
            sb.AppendLine("            {");
            sb.AppendLine("                continue;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            int rowKey;");
            sb.AppendLine("            if (!int.TryParse(rowKeyText, out rowKey))");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogWarning(\"[Table] rowKey 파싱 실패: row=\" + (r + 1) + \", value=\" + rowKeyText);");
            sb.AppendLine("                continue;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            if (!usedRowKeys.Add(rowKey))");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogWarning(\"[Table] 중복 rowKey 스킵: key=\" + rowKey + \", row=\" + (r + 1));");
            sb.AppendLine("                continue;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            Data data = new Data();");
            sb.AppendLine("            data.RowKey = rowKey;");

            for (int i = 0; i < columns.Count; i++)
            {
                ColumnInfo col = columns[i];
                sb.AppendLine("            {");
                sb.AppendLine("                string raw = table.GetCell(r, " + col.colIndex + ").Trim();");
                AppendParseAssign(sb, "data", col, className);
                sb.AppendLine("            }");
            }

            sb.AppendLine();
            sb.AppendLine("            _table.Add(data);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            string dir = Path.GetDirectoryName(scriptPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(scriptPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(scriptPath);
        }

        // Get(EnumName key)는 항상 문자열 필드(col.fieldName) 값으로 조회합니다 - 필드
        // 타입은 절대 enum으로 바꾸지 않으므로(SyncKeyEnums 참고), "선택 시트 갱신"이
        // 스키마 불변 시 이 클래스를 재컴파일하지 않고도 계속 동작합니다.
        private static void AppendKeyAccessor(StringBuilder sb, ColumnInfo col)
        {
            string cache = "_keyCache_" + col.fieldName;
            string build = "BuildKeyCacheIfNeeded_" + col.fieldName;

            sb.AppendLine("    public Data Get(" + col.keyEnumName + " key)");
            sb.AppendLine("    {");
            sb.AppendLine("        " + build + "();");
            sb.AppendLine();
            sb.AppendLine("        Data d;");
            sb.AppendLine("        if (!" + cache + ".TryGetValue(key.ToString(), out d))");
            sb.AppendLine("        {");
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        return d;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private void " + build + "()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (" + cache + " != null)");
            sb.AppendLine("        {");
            sb.AppendLine("            return;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        " + cache + " = new Dictionary<string, Data>();");
            sb.AppendLine();
            sb.AppendLine("        for (int i = 0; i < _table.Count; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            Data d = _table[i];");
            sb.AppendLine("            if (d == null || string.IsNullOrEmpty(d." + col.fieldName + "))");
            sb.AppendLine("            {");
            sb.AppendLine("                continue;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            if (" + cache + ".ContainsKey(d." + col.fieldName + "))");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogWarning(\"[Table] 중복 " + col.fieldName + " 스킵: key=\" + d." + col.fieldName + ");");
            sb.AppendLine("                continue;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            " + cache + "[d." + col.fieldName + "] = d;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        /// <summary>
        /// "key:EnumName" 컬럼들의 실제 시트 값(행)을 읽어, 필요하면 그 EnumName의 .cs 파일을
        /// outputDir에 새로 쓰거나 갱신합니다. WriteTableScript(스키마 기반)와 달리 이 메서드는
        /// 행 값에만 의존하므로, 스키마가 그대로라 WriteTableScript가 실행되지 않는 "선택 시트
        /// 갱신"에서도 반드시 호출되어야 새로 추가/삭제된 행의 키 값이 enum에 반영됩니다
        /// (DataTableImporterWindow의 생성/갱신 두 경로 모두에서 호출). outputDir은 보통
        /// 테이블 클래스 스크립트와 같은 폴더를 넘깁니다.
        /// </summary>
        public static void SyncKeyEnums(string tsv, List<ColumnInfo> columns, string outputDir)
        {
            TsvTable table = TsvParser.Parse(tsv);
            if (table == null)
            {
                return;
            }

            for (int i = 0; i < columns.Count; i++)
            {
                ColumnInfo col = columns[i];
                if (col.keyEnumName == null)
                {
                    continue;
                }

                SyncOneKeyEnum(table, col, outputDir);
            }
        }

        private static void SyncOneKeyEnum(TsvTable table, ColumnInfo col, string outputDir)
        {
            List<string> rawValues = new List<string>();

            for (int r = 3; r < table.RowCount; r++)
            {
                string raw = table.GetCell(r, col.colIndex).Trim();

                if (!string.IsNullOrEmpty(raw))
                {
                    rawValues.Add(raw);
                }
            }

            if (!TryGetExistingGlobalEnumNames(col.keyEnumName, out string[] existingNames))
            {
                // 동일 이름의 enum이 여러 개 있어 모호합니다 - 로그는
                // TryGetExistingGlobalEnumNames가 이미 남겼으므로 여기서는 그냥 건너뜁니다.
                return;
            }

            List<string> ordered = BuildOrderedKeyNames(rawValues, existingNames);
            bool alreadyExists = existingNames.Length > 0;

            if (alreadyExists && SameMembers(existingNames, ordered))
            {
                return;
            }

            string path = outputDir.TrimEnd('/', '\\') + "/" + col.keyEnumName + ".cs";
            string content = BuildKeyEnumFileContent(col.keyEnumName, ordered);
            WriteKeyEnumFile(path, content, col.keyEnumName, ordered.Count);
        }

        /// <summary>이미 컴파일된 프로젝트 어셈블리들에서 같은 이름의 "전역 네임스페이스"
        /// enum을 찾습니다(key: enum은 항상 전역에 생성되므로, 다른 네임스페이스의 동명
        /// enum은 무시합니다 - 예: 마이그레이션 중 남아있는 구 GameFramework.SoundSystem.ESound와
        /// 헷갈리지 않도록). 한 번도 생성된 적 없으면(첫 실행) 못 찾는 게 정상이라 빈 배열을
        /// 반환합니다. 전역 네임스페이스에 동일 이름 enum이 여러 개면(있을 수 없어야 정상이지만)
        /// 어느 걸 "기존 값"으로 봐야 할지 알 수 없어 실패로 처리합니다.</summary>
        private static bool TryGetExistingGlobalEnumNames(string enumTypeName, out string[] names)
        {
            names = Array.Empty<string>();

            TypeCache.TypeCollection candidates = TypeCache.GetTypesDerivedFrom<Enum>();
            Type found = null;

            for (int i = 0; i < candidates.Count; i++)
            {
                Type t = candidates[i];

                if (t.Name != enumTypeName || t.Namespace != null)
                {
                    continue;
                }

                if (found != null)
                {
                    Debug.LogError("[TableClassGenerator] 전역 네임스페이스에 동일 이름의 enum이 여러 개입니다: " + enumTypeName);
                    return false;
                }

                found = t;
            }

            if (found != null)
            {
                names = Enum.GetNames(found);
            }

            return true;
        }

        /// <summary>새로 생성할 때 이름을 시트 순서 그대로 두면, enum은 값을 안 주면 선언
        /// 순서대로 0,1,2...가 매겨지기 때문에, 기존 멤버는 시트에서 지워졌더라도 순서/값을
        /// 그대로 보존하고(저장된 데이터/Inspector 참조가 조용히 다른 값을 가리키지 않도록)
        /// 새로 추가된 이름만 맨 뒤에 붙입니다. "None"은 항상 0번으로 별도 예약되어 있어
        /// 결과 목록에서 제외합니다(BuildKeyEnumFileContent가 항상 맨 앞에 붙임).</summary>
        private static List<string> BuildOrderedKeyNames(List<string> sheetValues, string[] existingNames)
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < existingNames.Length; i++)
            {
                string name = existingNames[i];

                if (name == "None" || !seen.Add(name))
                {
                    continue;
                }

                result.Add(name);
            }

            for (int i = 0; i < sheetValues.Count; i++)
            {
                string raw = sheetValues[i];
                string safe = MakeEnumName(raw);

                if (string.IsNullOrEmpty(safe))
                {
                    continue;
                }

                if (safe == "None")
                {
                    Debug.LogWarning("[TableClassGenerator] \"" + raw + "\"은(는) 예약된 이름 \"None\"과 겹쳐서 별도 멤버로 생성되지 않고 기존 None(0)과 합쳐집니다. 시트의 값을 바꿔주세요.");
                    continue;
                }

                if (safe != raw)
                {
                    // Get(enum)은 key.ToString()(=enum 멤버 이름)으로 원본 문자열 필드 값을
                    // 그대로 조회합니다. 여기서 이름이 sanitize로 바뀌면 둘이 달라져서 이
                    // 항목은 Get(enum)으로 절대 조회되지 않습니다 - 조용히 넘어가지 않고
                    // 바로 알려줍니다.
                    Debug.LogError("[TableClassGenerator] \"" + raw + "\"은(는) 유효한 식별자가 아니라서 enum 멤버는 \"" + safe + "\"로 생성되지만, Get(enum) 조회는 원본 값(\"" + raw + "\")으로 이루어지므로 이 항목은 조회되지 않습니다. 시트 값을 영문/숫자/밑줄로만 바꿔주세요.");
                }

                if (seen.Add(safe))
                {
                    result.Add(safe);
                }
            }

            return result;
        }

        private static bool SameMembers(string[] existingNames, List<string> ordered)
        {
            List<string> existingWithoutNone = new List<string>();

            for (int i = 0; i < existingNames.Length; i++)
            {
                if (existingNames[i] != "None")
                {
                    existingWithoutNone.Add(existingNames[i]);
                }
            }

            if (existingWithoutNone.Count != ordered.Count)
            {
                return false;
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                if (existingWithoutNone[i] != ordered[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildKeyEnumFileContent(string enumName, List<string> orderedNames)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("// 자동 생성됨. 직접 편집하지 마세요.");
            sb.AppendLine();
            sb.AppendLine("public enum " + enumName);
            sb.AppendLine("{");
            sb.AppendLine("    None = 0,");

            for (int i = 0; i < orderedNames.Count; i++)
            {
                sb.AppendLine("    " + orderedNames[i] + ",");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void WriteKeyEnumFile(string path, string content, string enumName, int count)
        {
            string dir = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, content, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);

            Debug.Log("[TableClassGenerator] 생성됨: " + enumName + "(" + count + "개)");
        }

        // MakeEnumName: 공백/하이픈을 밑줄로 바꾸고, 영문/숫자/밑줄이 아닌 문자는 제거해서
        // 유효한 C# 식별자를 만듭니다. 숫자로 시작하면 앞에 밑줄을 붙입니다. ToSafeFieldName과
        // 달리 여기서는 잘못된 문자를 "_"로 치환하지 않고 아예 제거합니다 - 결과가 원본과
        // 달라지면(BuildOrderedKeyNames에서) 에러로 알리는 쪽이 필드 이름 sanitize보다
        // 엄격해야 하기 때문입니다(치환하면 서로 다른 원본 값이 같은 이름으로 뭉개질 수 있음).
        private static string MakeEnumName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            string s = raw.Trim().Replace(" ", "_").Replace("-", "_");
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                bool ok =
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    (c == '_');

                if (ok)
                {
                    sb.Append(c);
                }
            }

            string result = sb.ToString();

            if (string.IsNullOrEmpty(result))
            {
                return null;
            }

            if (result[0] >= '0' && result[0] <= '9')
            {
                result = "_" + result;
            }

            return result;
        }

        private static void AppendParseAssign(StringBuilder sb, string dataVar, ColumnInfo col, string className)
        {
            string field = dataVar + "." + col.fieldName;

            if (col.type == EDataTableColumnType.Enum)
            {
                string enumMethod = col.isArray ? "ParseEnumArray" : "ParseEnum";
                sb.AppendLine("                " + field + " = TableValueParser." + enumMethod + "<" + col.enumTypeFullName + ">(raw, \"" + className + "." + col.fieldName + " row=\" + (r + 1));");
                return;
            }

            // ParseEnum 호출부와 동일한 방식으로, 생성되는 코드 안에 "클래스.필드 row=" + (r + 1)
            // 형태의 C# 문자열 연결식을 그대로 심어 넣습니다. contextLiteral 자체가 이미
            // 여는/닫는 큰따옴표를 포함한 완성된 식이므로 별도로 감쌀 필요가 없습니다.
            string contextLiteral = "\"" + className + "." + col.fieldName + " row=\" + (r + 1)";

            if (col.isArray)
            {
                string arrayMethod = col.type switch
                {
                    EDataTableColumnType.Int => "ParseIntArray",
                    EDataTableColumnType.Long => "ParseLongArray",
                    EDataTableColumnType.Float => "ParseFloatArray",
                    EDataTableColumnType.Double => "ParseDoubleArray",
                    EDataTableColumnType.Bool => "ParseBoolArray",
                    _ => "ParseStringArray",
                };

                if (col.type == EDataTableColumnType.String)
                {
                    sb.AppendLine("                " + field + " = TableValueParser." + arrayMethod + "(raw);");
                }
                else
                {
                    sb.AppendLine("                " + field + " = TableValueParser." + arrayMethod + "(raw, " + contextLiteral + ");");
                }

                return;
            }

            if (col.type == EDataTableColumnType.String)
            {
                sb.AppendLine("                " + field + " = raw;");
                return;
            }

            string scalarMethod = col.type switch
            {
                EDataTableColumnType.Int => "ParseInt",
                EDataTableColumnType.Long => "ParseLong",
                EDataTableColumnType.Float => "ParseFloat",
                EDataTableColumnType.Double => "ParseDouble",
                EDataTableColumnType.Bool => "ParseBool",
                _ => null,
            };

            string defaultLiteral = col.type switch
            {
                EDataTableColumnType.Long => "0L",
                EDataTableColumnType.Float => "0f",
                EDataTableColumnType.Double => "0.0",
                EDataTableColumnType.Bool => "false",
                _ => "0",
            };

            sb.AppendLine("                " + field + " = TableValueParser." + scalarMethod + "(raw, " + defaultLiteral + ", " + contextLiteral + ");");
        }

        /// <summary>
        /// 로드된 모든 어셈블리에서 짧은 이름으로 기존 enum 타입을 찾습니다.
        /// enum은 반드시 프로젝트 코드에 이미 정의되어 있어야 하며, 이 메서드는 절대
        /// enum을 생성하지 않습니다. 타입이 없거나 이름이 모호하면 (명확한 이유와 함께)
        /// 실패하여, 오타난 타입 참조가 조용히 깨진 테이블을 만드는 대신 테이블 생성
        /// 자체를 막습니다.
        /// </summary>
        private static bool TryFindEnumType(string enumTypeName, out Type foundType, out string error)
        {
            foundType = null;
            error = null;

            TypeCache.TypeCollection candidates = TypeCache.GetTypesDerivedFrom<Enum>();

            for (int i = 0; i < candidates.Count; i++)
            {
                Type t = candidates[i];

                if (t.Name != enumTypeName)
                {
                    continue;
                }

                if (foundType != null)
                {
                    foundType = null;
                    error = "동일 이름의 enum 타입이 여러 개입니다: " + enumTypeName + " (네임스페이스로 구분되는 타입이 여러 개 있는지 확인하세요)";
                    return false;
                }

                foundType = t;
            }

            if (foundType == null)
            {
                error = "enum 타입을 찾을 수 없습니다: " + enumTypeName + " (먼저 C# 코드에 이 이름의 enum을 정의해야 합니다)";
                return false;
            }

            return true;
        }

        private static bool TryParseType(string typeText, out EDataTableColumnType type)
        {
            type = EDataTableColumnType.String;

            if (typeText == "int")
            {
                type = EDataTableColumnType.Int;
                return true;
            }

            if (typeText == "long")
            {
                type = EDataTableColumnType.Long;
                return true;
            }

            if (typeText == "float")
            {
                type = EDataTableColumnType.Float;
                return true;
            }

            if (typeText == "double")
            {
                type = EDataTableColumnType.Double;
                return true;
            }

            if (typeText == "string")
            {
                type = EDataTableColumnType.String;
                return true;
            }

            if (typeText == "bool")
            {
                type = EDataTableColumnType.Bool;
                return true;
            }

            return false;
        }

        private static string ToCsType(EDataTableColumnType type, bool isArray)
        {
            string baseType = type switch
            {
                EDataTableColumnType.Int => "int",
                EDataTableColumnType.Long => "long",
                EDataTableColumnType.Float => "float",
                EDataTableColumnType.Double => "double",
                EDataTableColumnType.Bool => "bool",
                _ => "string",
            };

            return isArray ? baseType + "[]" : baseType;
        }

        public static string ToSafeClassName(string tabName)
        {
            if (string.IsNullOrEmpty(tabName))
            {
                return "Table";
            }

            List<string> parts = new List<string>();
            string cur = "";

            for (int i = 0; i < tabName.Length; i++)
            {
                char ch = tabName[i];

                bool isAlphaNum =
                    (ch >= 'a' && ch <= 'z') ||
                    (ch >= 'A' && ch <= 'Z') ||
                    (ch >= '0' && ch <= '9');

                if (isAlphaNum)
                {
                    cur += ch;
                }
                else
                {
                    if (!string.IsNullOrEmpty(cur))
                    {
                        parts.Add(cur);
                        cur = "";
                    }
                }
            }

            if (!string.IsNullOrEmpty(cur))
            {
                parts.Add(cur);
            }

            if (parts.Count == 0)
            {
                return "Table";
            }

            string result = "";
            for (int i = 0; i < parts.Count; i++)
            {
                string p = parts[i];

                if (string.IsNullOrEmpty(p))
                {
                    continue;
                }

                string head = p.Substring(0, 1).ToUpperInvariant();
                string tail = p.Length > 1 ? p.Substring(1) : "";
                result += head + tail;
            }

            if (result.Length > 0)
            {
                char first = result[0];
                if (first >= '0' && first <= '9')
                {
                    result = "T" + result;
                }
            }

            // 시트 탭 이름이 나중에 게임플레이 클래스(예: Monster, Item)와 겹치는 것을
            // 원천적으로 막기 위해, 생성되는 클래스 이름에는 항상 Table 접미사를 붙입니다.
            // 이미 Table로 끝나는 이름(예: "ItemTable")에는 중복으로 붙이지 않습니다.
            if (!result.EndsWith("Table", StringComparison.Ordinal))
            {
                result += "Table";
            }

            return result;
        }

        private static string ToSafeFieldName(string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
            {
                return "field";
            }

            string s = "";
            for (int i = 0; i < columnName.Length; i++)
            {
                char ch = columnName[i];

                bool ok =
                    (ch >= 'a' && ch <= 'z') ||
                    (ch >= 'A' && ch <= 'Z') ||
                    (ch >= '0' && ch <= '9') ||
                    ch == '_';

                if (ok)
                {
                    s += ch;
                }
                else
                {
                    s += "_";
                }
            }

            if (string.IsNullOrEmpty(s))
            {
                s = "field";
            }

            if (s.Length > 0)
            {
                char first = s[0];
                if (first >= '0' && first <= '9')
                {
                    s = "_" + s;
                }
            }

            if (CSharpKeywords.Contains(s))
            {
                s = "_" + s;
            }

            return s;
        }

        // 컬럼 이름이 int/new/string 같은 C# 예약어와 그대로 겹치면 생성된 필드 선언이
        // 컴파일 에러가 나므로, 일부(class/namespace/public/private)만이 아니라 전체
        // 예약어 목록과 대조합니다.
        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
            "checked", "class", "const", "continue", "decimal", "default", "delegate",
            "do", "double", "else", "enum", "event", "explicit", "extern", "false",
            "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
            "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
            "new", "null", "object", "operator", "out", "override", "params", "private",
            "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
            "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
        };
    }
}
