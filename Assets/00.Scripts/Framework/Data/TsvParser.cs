public class TsvParser
{
    public static TsvTable Parse(string tsv)
    {
        TsvTable table = new();

        if (string.IsNullOrEmpty(tsv))
        {
            return table;
        }

        string normalized = tsv.Replace("\r\n", "\n").Replace("\r", "\n");
        string[] lines = normalized.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            table.AddRow(lines[i].Split('\t'));
        }

        return table;
    }

}
