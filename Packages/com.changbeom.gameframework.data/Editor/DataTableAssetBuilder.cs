using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameFramework.DataParsing.Editor
{
    [InitializeOnLoad]
    public static class DataTableAssetBuilder
    {
        private const string PendingKey = "GameFrameworkData_PendingCreate";
        private static bool _scheduled;

        static DataTableAssetBuilder()
        {
            EditorApplication.delayCall += TryCreatePendingAssets;
        }

        public static void SetPending(List<string> payloadLines)
        {
            string newJoined = string.Join("\n", payloadLines);

            // EditorPrefs가 아니라 SessionState를 쓰는 이유: 이 데이터는 새로 생성된
            // 스크립트가 재컴파일되면서 발생하는 domain reload만 버티면 되는 일회성
            // 데이터이기 때문입니다 -- EditorPrefs는 이 머신의 모든 프로젝트에 걸쳐
            // 전역으로 유지되므로, 여기에는 범위가 맞지 않습니다.
            //
            // _isBusy는 AssetDatabase.Refresh() 호출이 "리턴"하면 바로 풀리지만, 실제
            // 컴파일(도메인 리로드)은 비동기로 뒤늦게 끝납니다. 그 사이 창에서 "선택 시트
            // 생성"이 한 번 더 실행되면 TryCreatePendingAssets가 아직 첫 배치를 못
            // 꺼내갔을 수 있으므로(EditorApplication.isCompiling 대기 중), 여기서 그냥
            // 덮어쓰면 첫 배치의 pending 데이터가 통째로 사라집니다. 기존 값이 남아있으면
            // 지우지 않고 이어붙입니다.
            string existing = SessionState.GetString(PendingKey, "");
            string joined = string.IsNullOrEmpty(existing) ? newJoined : existing + "\n" + newJoined;

            SessionState.SetString(PendingKey, joined);

            if (!_scheduled)
            {
                _scheduled = true;
                EditorApplication.delayCall += TryCreatePendingAssets;
            }
        }

        private static void TryCreatePendingAssets()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryCreatePendingAssets;
                return;
            }

            _scheduled = false;

            string joined = SessionState.GetString(PendingKey, "");
            SessionState.EraseString(PendingKey);

            if (string.IsNullOrEmpty(joined))
            {
                return;
            }

            string[] lines = joined.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('|');
                if (parts.Length < 3)
                {
                    continue;
                }

                string className = parts[0];
                string assetPath = parts[1];
                string escapedTsv = parts[2];
                string tsv = UnescapePendingTsv(escapedTsv);

                Type t = FindScriptableObjectTypeByName(className);
                if (t == null)
                {
                    Debug.LogError("[GameFramework.DataParsing] 타입을 찾지 못했습니다: " + className);
                    continue;
                }

                ScriptableObject asset = AssetDatabase.LoadAssetAtPath(assetPath, t) as ScriptableObject;
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance(t);
                    AssetDatabase.CreateAsset(asset, assetPath);
                }

                var method = t.GetMethod("ParseFromTsv");
                if (method == null)
                {
                    // 여기서 조용히 넘어가면 asset은 생성됐지만 데이터가 하나도 채워지지
                    // 않은 채로 남아, 원인을 알기 어렵습니다. RefreshCoroutine의 동일한
                    // 실패 케이스(DataTableImporterWindow.cs)와 같은 방식으로 로그를 남깁니다.
                    Debug.LogError("[GameFramework.DataParsing] ParseFromTsv 메서드 없음: " + t.FullName);
                    continue;
                }

                try
                {
                    method.Invoke(asset, new object[] { tsv });
                    EditorUtility.SetDirty(asset);
                }
                catch (Exception e)
                {
                    // 여기서 예외가 그대로 터지면 SessionState는 이미 지워진 뒤라(위에서
                    // EraseString 완료) 이 항목뿐 아니라 아직 처리 못한 나머지 항목까지
                    // 전부 재시도 기회 없이 유실됩니다. 이 항목만 건너뛰고 나머지는
                    // 계속 진행합니다.
                    Debug.LogError("[GameFramework.DataParsing] " + className + " ParseFromTsv 실행 중 예외: " + e);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // DataTableImporterWindow.EscapeForPrefs가 만든 이스케이프(\\ -> \, \p -> |, \n -> 줄바꿈)를
        // 되돌립니다. '|'를 구분자로 쓰는 payload 형식상 원본 시트 데이터의 '|'/줄바꿈이
        // 그대로 남아있으면 안 되므로, 인코딩과 반드시 짝을 맞춰야 합니다.
        private static string UnescapePendingTsv(string escaped)
        {
            StringBuilder sb = new StringBuilder(escaped.Length);

            for (int i = 0; i < escaped.Length; i++)
            {
                char c = escaped[i];

                if (c == '\\' && i + 1 < escaped.Length)
                {
                    char next = escaped[i + 1];

                    if (next == 'n')
                    {
                        sb.Append('\n');
                        i++;
                        continue;
                    }

                    if (next == 'p')
                    {
                        sb.Append('|');
                        i++;
                        continue;
                    }

                    if (next == '\\')
                    {
                        sb.Append('\\');
                        i++;
                        continue;
                    }
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        private static Type FindScriptableObjectTypeByName(string className)
        {
            var types = TypeCache.GetTypesDerivedFrom<ScriptableObject>();

            Type found = null;

            for (int i = 0; i < types.Count; i++)
            {
                Type t = types[i];

                if (t.Name != className)
                {
                    continue;
                }

                if (found != null)
                {
                    Debug.LogError("[GameFramework.DataParsing] 동일 이름 ScriptableObject 타입이 여러 개입니다: " + className);
                    return null;
                }

                found = t;
            }

            return found;
        }
    }
}
