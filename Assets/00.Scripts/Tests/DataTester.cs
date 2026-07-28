using System;
using System.Text;
using GameFramework.Data;
using UnityEngine;

namespace GameFramework.Tests
{
    public sealed class DataTester : MonoBehaviour
    {
        private string _rowKeyText = "1";
        private string _log = "";
        private Vector2 _scroll;

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 520, Screen.height - 40));
            GUILayout.Box("Data Tester");

            GUILayout.BeginHorizontal();
            GUILayout.Label("RowKey:", GUILayout.Width(60));
            _rowKeyText = GUILayout.TextField(_rowKeyText, GUILayout.Width(80));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (GUILayout.Button("1) Item.Get(rowKey)"))
            {
                Item table = DataManager.Instance.GetTable<Item>();
                Log(table, table?.Get(ParseRowKey()));
            }

            if (GUILayout.Button("2) Monster.Get(rowKey)"))
            {
                Monster table = DataManager.Instance.GetTable<Monster>();
                Log(table, table?.Get(ParseRowKey()));
            }

            if (GUILayout.Button("3) Quest.Get(rowKey)"))
            {
                Quest table = DataManager.Instance.GetTable<Quest>();
                Log(table, table?.Get(ParseRowKey()));
            }

            if (GUILayout.Button("4) Sound.Get(rowKey)"))
            {
                Sound table = DataManager.Instance.GetTable<Sound>();

                if (table == null)
                {
                    Log("Sound table not found");
                }
                else
                {
                    Sound.Data d = table.Get(ParseRowKey());
                    Log(d == null
                        ? $"Sound: no row for key={ParseRowKey()} (table has {table.Table.Count} rows)"
                        : $"Sound: fileName={d.fileName}, channel={d.channel}, volume={d.defaultVolume}, maxConcurrent={d.maxConcurrent}, loop={d.loop}");
                }
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Clear Log"))
            {
                _log = "";
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(280));
            GUILayout.TextArea(_log);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private int ParseRowKey()
        {
            int.TryParse(_rowKeyText, out int key);
            return key;
        }

        private void Log(Item table, Item.Data d)
        {
            if (table == null)
            {
                Log("Item table not found");
                return;
            }

            Log(d == null
                ? $"Item: no row for key={ParseRowKey()} (table has {table.Table.Count} rows)"
                : $"Item: name={d.name}, description={d.description}");
        }

        private void Log(Monster table, Monster.Data d)
        {
            if (table == null)
            {
                Log("Monster table not found");
                return;
            }

            Log(d == null
                ? $"Monster: no row for key={ParseRowKey()} (table has {table.Table.Count} rows)"
                : $"Monster: name={d.name}, description={d.description}");
        }

        private void Log(Quest table, Quest.Data d)
        {
            if (table == null)
            {
                Log("Quest table not found");
                return;
            }

            Log(d == null
                ? $"Quest: no row for key={ParseRowKey()} (table has {table.Table.Count} rows)"
                : $"Quest: name={d.name}, description={d.description}");
        }

        private void Log(string msg)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + " | " + msg;
            Debug.Log(line);
            _log = string.IsNullOrEmpty(_log) ? line : _log + "\n" + line;
        }
    }
}
