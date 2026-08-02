using System.Collections.Generic;
using System.Text;

namespace GameFramework.DataParsing
{
    public class TsvParser
    {
        /// <summary>
        /// Google Sheets가 TSV로 내보낼 때 탭/줄바꿈/큰따옴표가 포함된 셀을 큰따옴표로
        /// 감싸는 RFC4180 스타일 인용을 씁니다. 단순 Split('\n')+Split('\t')로는 이런
        /// 셀 하나만 있어도 그 뒤의 모든 행/열이 밀려버리므로, 한 글자씩 상태를 추적하며 파싱합니다.
        /// </summary>
        public static TsvTable Parse(string tsv)
        {
            TsvTable table = new();

            if (string.IsNullOrEmpty(tsv))
            {
                return table;
            }

            List<string> row = new();
            StringBuilder field = new();
            bool inQuotes = false;

            int i = 0;
            int length = tsv.Length;

            while (i < length)
            {
                char c = tsv[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < length && tsv[i + 1] == '"')
                        {
                            field.Append('"');
                            i += 2;
                            continue;
                        }

                        inQuotes = false;
                        i++;
                        continue;
                    }

                    if (c == '\r')
                    {
                        field.Append('\n');
                        i++;
                        if (i < length && tsv[i] == '\n')
                        {
                            i++;
                        }
                        continue;
                    }

                    field.Append(c);
                    i++;
                    continue;
                }

                // 따옴표는 필드의 첫 글자일 때만 인용 시작으로 취급합니다 (RFC4180 관례).
                if (c == '"' && field.Length == 0)
                {
                    inQuotes = true;
                    i++;
                    continue;
                }

                if (c == '\t')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    i++;
                    continue;
                }

                bool isRowEnd = c == '\n';

                if (c == '\r')
                {
                    isRowEnd = true;
                    if (i + 1 < length && tsv[i + 1] == '\n')
                    {
                        i++;
                    }
                }

                if (isRowEnd)
                {
                    row.Add(field.ToString());
                    field.Clear();
                    table.AddRow(row.ToArray());
                    row = new List<string>();
                    i++;
                    continue;
                }

                field.Append(c);
                i++;
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                table.AddRow(row.ToArray());
            }

            return table;
        }
    }
}
