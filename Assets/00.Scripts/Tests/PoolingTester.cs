using System;
using System.Collections.Generic;
using GameFramework.Pooling;
using UnityEngine;

namespace GameFramework.Tests
{
    public sealed class PoolingTester : MonoBehaviour
    {
        private GameObject _testPrefab;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private string _log = "";
        private Vector2 _scroll;

        private void Awake()
        {
            // Instantiate() works on any GameObject reference, not just a Prefab asset,
            // so this tester needs zero manual setup (no dragging a prefab in).
            _testPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _testPrefab.name = "PoolingTestCube";
            _testPrefab.transform.SetParent(transform, false);
            _testPrefab.SetActive(false);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 480, Screen.height - 40));
            GUILayout.Box("Pooling Tester");

            GUILayout.Label($"Active spawned: {_spawned.Count}");

            if (GUILayout.Button("1) Spawn"))
            {
                Vector3 pos = UnityEngine.Random.insideUnitSphere * 3f;
                GameObject go = PoolManager.Instance.Spawn(_testPrefab, pos, Quaternion.identity);

                if (go != null)
                {
                    _spawned.Add(go);
                    Log($"Spawned id={go.GetInstanceID()} at {pos}");
                }
            }

            if (GUILayout.Button("2) Despawn last (watch id -- reused on next Spawn)"))
            {
                if (_spawned.Count == 0)
                {
                    Log("Nothing to despawn");
                }
                else
                {
                    GameObject go = _spawned[_spawned.Count - 1];
                    _spawned.RemoveAt(_spawned.Count - 1);
                    PoolManager.Instance.Despawn(go);
                    Log($"Despawned id={go.GetInstanceID()}");
                }
            }

            if (GUILayout.Button("3) Despawn all"))
            {
                int count = _spawned.Count;

                for (int i = 0; i < _spawned.Count; i++)
                {
                    PoolManager.Instance.Despawn(_spawned[i]);
                }

                _spawned.Clear();
                Log($"Despawned all ({count})");
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
