using System;
using System.Text;
using GameFramework.DataParsing;
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

            if (GUILayout.Button("1) ItemTable.Get(rowKey)"))
            {
                ItemTable table = DataManager.Instance.GetTable<ItemTable>();
                Log(table, table?.Get(ParseRowKey()));
            }

            if (GUILayout.Button("2) MonsterTable.Get(rowKey)"))
            {
                MonsterTable table = DataManager.Instance.GetTable<MonsterTable>();
                Log(table, table?.Get(ParseRowKey()));
            }

            if (GUILayout.Button("3) QuestTable.Get(rowKey)"))
            {
                QuestTable table = DataManager.Instance.GetTable<QuestTable>();
                Log(table, table?.Get(ParseRowKey()));
            }

            if (GUILayout.Button("4) SoundTable.Get(rowKey)"))
            {
                SoundTable table = DataManager.Instance.GetTable<SoundTable>();

                if (table == null)
                {
                    Log("SoundTable 테이블을 찾을 수 없습니다");
                }
                else
                {
                    SoundTable.Data d = table.Get(ParseRowKey());
                    Log(d == null
                        ? $"SoundTable: key={ParseRowKey()}에 해당하는 행 없음 (테이블에 {table.Table.Count}개 행 존재)"
                        : $"SoundTable: fileName={d.fileName}, channel={d.channel}, volume={d.defaultVolume}, maxConcurrent={d.maxConcurrent}, loop={d.loop}");
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

        private void Log(ItemTable table, ItemTable.Data d)
        {
            if (table == null)
            {
                Log("ItemTable 테이블을 찾을 수 없습니다");
                return;
            }

            Log(d == null
                ? $"ItemTable: key={ParseRowKey()}에 해당하는 행 없음 (테이블에 {table.Table.Count}개 행 존재)"
                : $"ItemTable: name={d.name}, description={d.description}");
        }

        private void Log(MonsterTable table, MonsterTable.Data d)
        {
            if (table == null)
            {
                Log("MonsterTable 테이블을 찾을 수 없습니다");
                return;
            }

            Log(d == null
                ? $"MonsterTable: key={ParseRowKey()}에 해당하는 행 없음 (테이블에 {table.Table.Count}개 행 존재)"
                : $"MonsterTable: name={d.name}, description={d.description}");
        }

        private void Log(QuestTable table, QuestTable.Data d)
        {
            if (table == null)
            {
                Log("QuestTable 테이블을 찾을 수 없습니다");
                return;
            }

            Log(d == null
                ? $"QuestTable: key={ParseRowKey()}에 해당하는 행 없음 (테이블에 {table.Table.Count}개 행 존재)"
                : $"QuestTable: name={d.name}, description={d.description}");
        }

        private void Log(string msg)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + " | " + msg;
            Debug.Log(line);
            _log = string.IsNullOrEmpty(_log) ? line : _log + "\n" + line;
        }
    }
}
