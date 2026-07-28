using System;
using GameFramework.SaveLoad;
using UnityEngine;

namespace GameFramework.Tests
{
    public sealed class SaveLoadTester : MonoBehaviour
    {
        private sealed class TestData
        {
            public int Score;
            public string PlayerName;
        }

        private const string Domain = "test";
        private const string Key = "sample";

        private string _log = "";
        private Vector2 _scroll;

        private SaveKey TestKey => SaveManager.Instance.Domain(Domain).Join(Key);

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 520, Screen.height - 40));
            GUILayout.Box("SaveLoad Tester");

            GUILayout.Label("Basic");

            if (GUILayout.Button("1) Save random sample data"))
            {
                TestData data = new TestData
                {
                    Score = UnityEngine.Random.Range(0, 1000),
                    PlayerName = "Tester"
                };

                SaveManager.Instance.Save(TestKey, data);
                Log($"Saved: score={data.Score}, name={data.PlayerName}");
            }

            if (GUILayout.Button("2) TryLoad"))
            {
                bool ok = SaveManager.Instance.TryLoad(TestKey, out TestData data);
                Log(ok ? $"Loaded: score={data.Score}, name={data.PlayerName}" : "No data found for key");
            }

            if (GUILayout.Button("3) LoadOrCreate (creates default if missing)"))
            {
                TestData data = SaveManager.Instance.LoadOrCreate(
                    TestKey,
                    () => new TestData { Score = -1, PlayerName = "Default" },
                    saveIfMissing: true);

                Log($"LoadOrCreate: score={data.Score}, name={data.PlayerName}");
            }

            if (GUILayout.Button("4) HasKey"))
            {
                Log("HasKey: " + SaveManager.Instance.HasKey(TestKey));
            }

            if (GUILayout.Button("5) Delete"))
            {
                SaveManager.Instance.Delete(TestKey);
                Log("Deleted key");
            }

            GUILayout.Space(10);
            GUILayout.Label("Flush");

            if (GUILayout.Button("6) Flush (force write to disk now)"))
            {
                SaveManager.Instance.Flush();
                Log("Flushed");
            }

            GUILayout.Space(10);
            GUILayout.Label("Backup / Restore (JsonFile, Es3 only -- no-op on PlayerPrefs/Memory)");

            if (GUILayout.Button("7) HasBackup"))
            {
                Log("HasBackup: " + SaveManager.Instance.HasBackup());
            }

            if (GUILayout.Button("8) BackupNow"))
            {
                SaveManager.Instance.BackupNow();
                Log("BackupNow called");
            }

            if (GUILayout.Button("9) RestoreFromBackup"))
            {
                bool restored = SaveManager.Instance.RestoreFromBackup();
                Log("RestoreFromBackup: " + restored);
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

        private void Log(string msg)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + " | " + msg;
            Debug.Log(line);
            _log = string.IsNullOrEmpty(_log) ? line : _log + "\n" + line;
        }
    }
}
