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

            /// <summary>"key:EnumName" 컬럼일 때만 설정됩니다(필드 타입은 계속 string).</summary>
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

                if (fieldName == "Id")
                {
                    error = "컬럼 이름 \"" + name + "\"는 sanitize 후 \"Id\"가 되어, 모든 테이블에 이미 고정으로 있는 Id 필드와 충돌합니다. 컬럼 이름을 다르게 바꿔주세요.";
                    return false;
                }

                for (int i = 0; i < columns.Count; i++)
                {
                    if (columns[i].columnName == name)
                    {
                        error = "중복 컬럼 이름: " + name;
                        return false;
                    }

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
            sb.AppendLine("        public int Id;");

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
            sb.AppendLine("    public Data Get(int id)");
            sb.AppendLine("    {");
            sb.AppendLine("        BuildCacheIfNeeded();");
            sb.AppendLine();
            sb.AppendLine("        Data d;");
            sb.AppendLine("        if (!_cache.TryGetValue(id, out d))");
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
            sb.AppendLine("            _cache[d.Id] = d;");
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
            sb.AppendLine("        HashSet<int> usedIds = new HashSet<int>();");
            sb.AppendLine();
            sb.AppendLine("        for (int r = 3; r < table.RowCount; r++)");
            sb.AppendLine("        {");
            sb.AppendLine("            string idText = table.GetCell(r, 0).Trim();");
            sb.AppendLine("            if (string.IsNullOrEmpty(idText))");
            sb.AppendLine("            {");
            sb.AppendLine("                continue;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            int id;");
            sb.AppendLine("            if (!int.TryParse(idText, out id))");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogWarning(\"[Table] id 파싱 실패: row=\" + (r + 1) + \", value=\" + idText);");
            sb.AppendLine("                continue;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            if (!usedIds.Add(id))");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogWarning(\"[Table] 중복 id 스킵: key=\" + id + \", row=\" + (r + 1));");
            sb.AppendLine("                continue;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            Data data = new Data();");
            sb.AppendLine("            data.Id = id;");

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
        }

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

        /// <summary>"key:EnumName" 컬럼들의 실제 시트 값으로 그 EnumName의 .cs 파일을
        /// outputDir(보통 테이블 클래스 스크립트와 같은 폴더)에 생성/갱신합니다. "선택 시트
        /// 생성"과 "선택 시트 갱신" 양쪽에서 호출해야 합니다.</summary>
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

        /// <summary>기존 멤버는 시트에서 지워졌더라도 순서/값을 보존하고, 새 값만 뒤에
        /// 붙입니다("None"은 항상 0번 예약이라 결과 목록에서 제외).</summary>
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

            Debug.Log("[TableClassGenerator] 생성됨: " + enumName + "(" + count + "개)");
        }

        /// <summary>공백/하이픈을 밑줄로 바꾸고, 영문/숫자/밑줄이 아닌 문자는 제거해서 유효한
        /// C# 식별자를 만듭니다. 숫자로 시작하면 앞에 밑줄을 붙입니다.</summary>
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

        /// <summary>프로젝트 코드에 이미 정의된 enum 타입을 이름으로 찾습니다. 없거나
        /// 이름이 모호하면 실패합니다(이 메서드는 enum을 생성하지 않음).</summary>
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
