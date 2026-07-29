using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;

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
                else if (!TryParseType(baseTypeText.ToLowerInvariant(), out type))
                {
                    error = "알 수 없는 타입: " + rawTypeText + " (col=" + (c + 1) + ", name=" + name + ")";
                    return false;
                }

                for (int i = 0; i < columns.Count; i++)
                {
                    if (columns[i].columnName == name)
                    {
                        error = "중복 컬럼 이름: " + name;
                        return false;
                    }
                }

                ColumnInfo info = new ColumnInfo();
                info.colIndex = c;
                info.columnName = name;
                info.fieldName = ToSafeFieldName(name);
                info.type = type;
                info.isArray = isArray;
                info.enumTypeFullName = enumTypeFullName;

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
            sb.AppendLine("    public void ParseFromTsv(string tsv)");
            sb.AppendLine("    {");
            sb.AppendLine("        _table.Clear();");
            sb.AppendLine("        _cacheBuilt = false;");
            sb.AppendLine("        _cache = null;");
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

        private static void AppendParseAssign(StringBuilder sb, string dataVar, ColumnInfo col, string className)
        {
            string field = dataVar + "." + col.fieldName;

            if (col.type == EDataTableColumnType.Enum)
            {
                string enumMethod = col.isArray ? "ParseEnumArray" : "ParseEnum";
                sb.AppendLine("                " + field + " = TableValueParser." + enumMethod + "<" + col.enumTypeFullName + ">(raw, \"" + className + "." + col.fieldName + " row=\" + (r + 1));");
                return;
            }

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

                sb.AppendLine("                " + field + " = TableValueParser." + arrayMethod + "(raw);");
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

            sb.AppendLine("                " + field + " = TableValueParser." + scalarMethod + "(raw, " + defaultLiteral + ");");
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

            return result;
        }

        public static string ToSafeAssetFileName(string tabName)
        {
            if (string.IsNullOrEmpty(tabName))
            {
                return "Table";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            string s = tabName;

            for (int i = 0; i < invalid.Length; i++)
            {
                s = s.Replace(invalid[i], '_');
            }

            return s;
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

            if (s == "class" || s == "namespace" || s == "public" || s == "private")
            {
                s = "_" + s;
            }

            return s;
        }
    }
}
