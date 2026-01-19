using System.Collections.Generic;

public class TsvTable
{
    private readonly List<string[]> _rows = new();

    public int RowCount => _rows.Count;

    public int ColCount
    {
        get
        {
            int max = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null && _rows[i].Length > max)
                {
                    max = _rows[i].Length;
                }
            }
            return max;
        }
    }

    public void AddRow(string[] row)
    {
        _rows.Add(row);
    }

    public string GetCell(int row, int col)
    {
        if (row < 0 || row >= _rows.Count)
        {
            return "";
        }

        string[] r = _rows[row];

        if (r == null || col < 0 || col >= r.Length)
        {
            return "";
        }

        return r[col] ?? "";
    }
}