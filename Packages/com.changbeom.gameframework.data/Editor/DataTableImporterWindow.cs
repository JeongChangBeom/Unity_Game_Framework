using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace GameFramework.DataParsing.Editor
{
    public class DataTableImporterWindow : EditorWindow
    {
        [Serializable]
        private class SheetTabInfo
        {
            public bool selected;
            public string title;
            public int gid;

            public string className;
            public string scriptPath;

            public string assetPath;
            public string resourcesPath;

            public bool assetExists;

            /// <summary>다른 탭과 sanitize 후 className이 겹치는지 여부입니다 (예: "Item Table"과
            /// "ItemTable" 둘 다 -> "ItemTable"). 겹친 채로 생성하면 나중 탭이 먼저 만든 탭의
            /// 스크립트/에셋을 조용히 덮어쓰므로, 생성 단계에서 이 값을 보고 걸러냅니다.</summary>
            public bool duplicateClassName;
        }

        [Serializable]
        private class TabListWrapper
        {
            public List<SheetTabInfo> tabs = new List<SheetTabInfo>();
        }

        private const string PrefPrefix = "DataTableImporter_";

        private string _sheetUrl = "";
        private string _apiKey = "";
        private string _spreadsheetId = "";

        private string _generatedScriptFolder = "Assets/00.Scripts/GeneratedTables";
        private string _resourcesFolder = "Assets/Resources/GeneratedTables";

        private bool _overwriteScript = true;

        private Vector2 _scroll;
        private bool _isBusy;
        private string _status = "";

        private List<SheetTabInfo> _tabs = new List<SheetTabInfo>();

        [MenuItem("Game Framework/Data Parsing/DataTable Importer")]
        public static void Open()
        {
            GetWindow<DataTableImporterWindow>("DataTable Importer");
        }

        private void OnEnable()
        {
            LoadPrefs();
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Google Sheet", EditorStyles.boldLabel);
            _sheetUrl = EditorGUILayout.TextField("Sheet URL", _sheetUrl);
            _apiKey = EditorGUILayout.TextField("API Key", _apiKey);

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Generate Paths", EditorStyles.boldLabel);
            _generatedScriptFolder = EditorGUILayout.TextField("Table Script Folder", _generatedScriptFolder);
            _resourcesFolder = EditorGUILayout.TextField("Resources Folder", _resourcesFolder);
            _overwriteScript = EditorGUILayout.ToggleLeft("Overwrite .cs when exists", _overwriteScript);

            EditorGUILayout.Space(10);

            using (new EditorGUI.DisabledScope(_isBusy))
            {
                if (GUILayout.Button("시트 불러오기"))
                {
                    FetchTabs();
                }
            }

            EditorGUILayout.Space(8);

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }

            EditorGUILayout.Space(6);
            DrawTabs();

            EditorGUILayout.Space(10);

            using (new EditorGUI.DisabledScope(_isBusy || _tabs.Count == 0))
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("선택 시트 생성"))
                {
                    CreateSelected();
                }

                if (GUILayout.Button("선택 시트 갱신"))
                {
                    RefreshSelected();
                }

                if (GUILayout.Button("선택 시트 삭제"))
                {
                    DeleteSelected();
                }

                EditorGUILayout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck())
            {
                SavePrefs();
            }
        }

        private void DrawTabs()
        {
            EditorGUILayout.LabelField("Sheets", EditorStyles.boldLabel);

            if (_tabs.Count == 0)
            {
                EditorGUILayout.HelpBox("시트가 없습니다.", MessageType.None);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(320));

            for (int i = 0; i < _tabs.Count; i++)
            {
                SheetTabInfo t = _tabs[i];

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();

                bool newSelected = EditorGUILayout.Toggle(t.selected, GUILayout.Width(18));
                if (newSelected != t.selected)
                {
                    t.selected = newSelected;
                    SavePrefs();
                }

                EditorGUILayout.LabelField(t.title, GUILayout.MinWidth(160));
                EditorGUILayout.LabelField("gid: " + t.gid, GUILayout.Width(90));
                EditorGUILayout.LabelField(t.assetExists ? "asset: 있음" : "asset: 없음", GUILayout.Width(90));

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("Class: " + t.className);
                EditorGUILayout.LabelField("Script: " + t.scriptPath);
                EditorGUILayout.LabelField("Asset: " + t.assetPath);
                EditorGUILayout.LabelField("ResPath: " + t.resourcesPath);

                if (t.duplicateClassName)
                {
                    EditorGUILayout.HelpBox("다른 탭과 className이 겹칩니다. 시트 탭 이름을 바꾸기 전까지 생성에서 제외됩니다.", MessageType.Warning);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All"))
            {
                for (int i = 0; i < _tabs.Count; i++)
                {
                    _tabs[i].selected = true;
                }
                SavePrefs();
            }
            if (GUILayout.Button("Select None"))
            {
                for (int i = 0; i < _tabs.Count; i++)
                {
                    _tabs[i].selected = false;
                }
                SavePrefs();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void FetchTabs()
        {
            _sheetUrl = (_sheetUrl ?? "").Trim();
            _apiKey = (_apiKey ?? "").Trim();

            if (!GoogleSheetUtility.TryExtractSpreadsheetId(_sheetUrl, out _spreadsheetId))
            {
                _status = "Sheet URL에서 Spreadsheet ID 추출 실패";
                SavePrefs();
                return;
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                _status = "API Key가 비어있습니다.";
                SavePrefs();
                return;
            }

            _isBusy = true;
            _status = "시트 불러오는 중...";
            SavePrefs();

            UnityWebRequest req = GoogleSheetUtility.BuildTabsRequest(_spreadsheetId, _apiKey);

            EditorCoroutineUtility.StartCoroutine(RequestCoroutine(req, OnTabsResponse), this);
        }

        private void OnTabsResponse(bool ok, string text)
        {
            _isBusy = false;

            if (!ok)
            {
                _status = "시트 불러오기 실패: " + text;
                SavePrefs();
                return;
            }

            GoogleSheetUtility.TabsResponse resp;
            try
            {
                resp = JsonUtility.FromJson<GoogleSheetUtility.TabsResponse>(text);
            }
            catch (Exception e)
            {
                _status = "시트 JSON 파싱 실패: " + e.Message;
                SavePrefs();
                return;
            }

            Dictionary<string, bool> selectedMap = new Dictionary<string, bool>();
            for (int i = 0; i < _tabs.Count; i++)
            {
                SheetTabInfo old = _tabs[i];
                if (old != null && !string.IsNullOrEmpty(old.title))
                {
                    selectedMap[old.title] = old.selected;
                }
            }

            _tabs.Clear();

            EnsureFolder(_generatedScriptFolder);
            EnsureFolder(_resourcesFolder);

            if (resp == null || resp.sheets == null)
            {
                _status = "시트 목록이 비어있습니다.";
                SavePrefs();
                return;
            }

            for (int i = 0; i < resp.sheets.Length; i++)
            {
                var p = resp.sheets[i].properties;
                if (p == null)
                {
                    continue;
                }

                // 스크립트 파일명/클래스명/에셋 파일명이 전부 같은 className에서 나와야
                // DataManager.GetTable<T>()의 "Resources/GeneratedTables/{타입명}" 관례가
                // 항상 실제 저장된 .asset 파일명과 일치합니다 (따로 sanitize하면 어긋날 수 있음).
                string className = TableClassGenerator.ToSafeClassName(p.title);

                SheetTabInfo item = new SheetTabInfo();
                item.title = p.title;
                item.gid = p.sheetId;

                item.className = className;
                item.scriptPath = _generatedScriptFolder + "/" + className + ".cs";

                item.assetPath = _resourcesFolder + "/" + className + ".asset";
                item.resourcesPath = "GeneratedTables/" + className;

                item.assetExists = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(item.assetPath) != null;

                bool prevSelected;
                if (selectedMap.TryGetValue(item.title, out prevSelected))
                {
                    item.selected = prevSelected;
                }
                else
                {
                    item.selected = false;
                }

                _tabs.Add(item);
            }

            MarkDuplicateClassNames(_tabs);

            int duplicateCount = 0;
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].duplicateClassName)
                {
                    duplicateCount++;
                }
            }

            _status = "시트 로드 완료: " + _tabs.Count + "개";
            if (duplicateCount > 0)
            {
                _status += " (className이 겹치는 탭 " + duplicateCount + "개 - 아래 목록에서 확인, 생성 시 제외됩니다)";
            }

            SavePrefs();
            Repaint();
        }

        // 시트 탭 제목이 서로 달라도(예: "Item Table" vs "Item-Table") ToSafeClassName을
        // 거치면 같은 className으로 sanitize될 수 있습니다. 이 상태로 둘 다 생성하면
        // 나중 탭이 먼저 만든 탭의 .cs/.asset을 파일명이 같다는 이유로 조용히 덮어씁니다.
        private static void MarkDuplicateClassNames(List<SheetTabInfo> tabs)
        {
            Dictionary<string, int> countByClassName = new Dictionary<string, int>();

            for (int i = 0; i < tabs.Count; i++)
            {
                string name = tabs[i].className;
                countByClassName[name] = (countByClassName.TryGetValue(name, out int c) ? c : 0) + 1;
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                bool isDuplicate = countByClassName[tabs[i].className] > 1;
                tabs[i].duplicateClassName = isDuplicate;

                if (isDuplicate)
                {
                    Debug.LogError("[DataTableImporter] 시트 탭 \"" + tabs[i].title + "\"이(가) 다른 탭과 같은 className(\"" + tabs[i].className + "\")으로 변환됩니다. 시트 탭 이름을 다르게 바꾸기 전까지 이 탭은 생성 대상에서 제외됩니다.");
                }
            }
        }

        private void CreateSelected()
        {
            List<SheetTabInfo> targets = GetSelected();

            int skippedDuplicates = targets.RemoveAll(t => t.duplicateClassName);
            if (skippedDuplicates > 0)
            {
                Debug.LogWarning("[DataTableImporter] className이 겹치는 탭 " + skippedDuplicates + "개를 생성 대상에서 제외했습니다.");
            }

            if (targets.Count == 0)
            {
                _status = "선택된 시트가 없습니다.";
                SavePrefs();
                return;
            }

            _sheetUrl = (_sheetUrl ?? "").Trim();
            _apiKey = (_apiKey ?? "").Trim();

            if (!GoogleSheetUtility.TryExtractSpreadsheetId(_sheetUrl, out _spreadsheetId))
            {
                _status = "Spreadsheet ID 추출 실패";
                SavePrefs();
                return;
            }

            EnsureFolder(_generatedScriptFolder);
            EnsureFolder(_resourcesFolder);

            _isBusy = true;
            _status = "생성 중... (스크립트 생성 후 컴파일 대기 -> 에셋 생성/파싱)";
            SavePrefs();

            EditorCoroutineUtility.StartCoroutine(CreateCoroutine(targets), this);
        }

        private IEnumerator CreateCoroutine(List<SheetTabInfo> targets)
        {
            // try 안에 yield가 있으면 catch는 같이 못 쓰지만(C# 컴파일 제약), finally는
            // 됩니다. 루프 중간에 예외가 나도 ClearProgressBar/_isBusy 해제가 반드시
            // 실행되도록 해서, 안 그러면 진행바가 뜬 채로 멈추고 _isBusy가 영원히 true로
            // 남아 창의 모든 버튼이 막혀버리는 문제를 방지합니다.
            List<string> pending = new List<string>();

            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    SheetTabInfo tab = targets[i];

                    EditorUtility.DisplayProgressBar(
                        "DataTable Importer",
                        "TSV 다운로드 중: " + tab.title + " (" + (i + 1) + "/" + targets.Count + ")",
                        (float)(i + 1) / targets.Count);

                    UnityWebRequest req = GoogleSheetUtility.BuildTsvRequest(_spreadsheetId, tab.gid);
                    yield return req.SendWebRequest();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError("[DataTableImporter] TSV 다운로드 실패: " + tab.title + " / " + req.error);
                        continue;
                    }

                    string tsv = req.downloadHandler.text;

                    List<TableClassGenerator.ColumnInfo> cols;
                    string err;
                    if (!TableClassGenerator.TryExtractColumnsFromTsv(tsv, out cols, out err))
                    {
                        Debug.LogError("[DataTableImporter] 컬럼 분석 실패: " + tab.title + " / " + err);
                        continue;
                    }

                    if (File.Exists(tab.scriptPath) && !_overwriteScript)
                    {
                    }
                    else
                    {
                        TableClassGenerator.WriteTableScript(tab.scriptPath, tab.className, cols);
                    }

                    // WriteTableScript가 "key:" 컬럼의 Get(EnumName) 메서드를 이미 새로 쓴
                    // 스크립트에 넣었더라도, 그 EnumName 타입 자체는 아직 존재하지 않을 수
                    // 있습니다(첫 생성). 재컴파일 전에(AssetDatabase.Refresh() 이전) 여기서
                    // 미리 만들어둬야 컴파일이 깨지지 않습니다. 스키마가 그대로라
                    // WriteTableScript를 건너뛴 경우에도, 그 사이 시트 행이 바뀌었을 수
                    // 있으므로 항상 호출합니다.
                    TableClassGenerator.SyncKeyEnums(tsv, cols, Path.GetDirectoryName(tab.scriptPath));

                    string payloadLine = tab.className + "|" + tab.assetPath + "|" + EscapeForPrefs(tsv);
                    pending.Add(payloadLine);
                }

                if (pending.Count > 0)
                {
                    DataTableAssetBuilder.SetPending(pending);
                }

                AssetDatabase.Refresh();

                for (int i = 0; i < _tabs.Count; i++)
                {
                    _tabs[i].assetExists = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_tabs[i].assetPath) != null;
                }

                _status = "완료. 컴파일 후 Resources에 .asset이 생성됩니다.";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isBusy = false;
                SavePrefs();
                Repaint();
            }
        }

        private void RefreshSelected()
        {
            List<SheetTabInfo> targets = GetSelected();
            if (targets.Count == 0)
            {
                _status = "선택된 시트가 없습니다.";
                SavePrefs();
                return;
            }

            _sheetUrl = (_sheetUrl ?? "").Trim();
            _apiKey = (_apiKey ?? "").Trim();

            if (!GoogleSheetUtility.TryExtractSpreadsheetId(_sheetUrl, out _spreadsheetId))
            {
                _status = "Spreadsheet ID 추출 실패";
                SavePrefs();
                return;
            }

            _isBusy = true;
            _status = "갱신 중...";
            SavePrefs();

            EditorCoroutineUtility.StartCoroutine(RefreshCoroutine(targets), this);
        }

        private IEnumerator RefreshCoroutine(List<SheetTabInfo> targets)
        {
            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    SheetTabInfo tab = targets[i];

                    EditorUtility.DisplayProgressBar(
                        "DataTable Importer",
                        "갱신 중: " + tab.title + " (" + (i + 1) + "/" + targets.Count + ")",
                        (float)(i + 1) / targets.Count);

                    UnityWebRequest req = GoogleSheetUtility.BuildTsvRequest(_spreadsheetId, tab.gid);
                    yield return req.SendWebRequest();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError("[DataTableImporter] TSV 다운로드 실패: " + tab.title + " / " + req.error);
                        continue;
                    }

                    string tsv = req.downloadHandler.text;

                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(tab.assetPath);
                    if (asset == null)
                    {
                        Debug.LogWarning("[DataTableImporter] asset 없음(스킵): " + tab.assetPath);
                        continue;
                    }

                    List<TableClassGenerator.ColumnInfo> columns;
                    string schemaError;

                    if (!TableClassGenerator.TryExtractColumnsFromTsv(tsv, out columns, out schemaError))
                    {
                        Debug.LogError("[DataTableImporter] 컬럼 분석 실패: " + tab.title + " / " + schemaError);
                        continue;
                    }

                    Type dataType = asset.GetType().GetNestedType(
                        "Data",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                    if (dataType == null)
                    {
                        Debug.LogError("[DataTableImporter] Data 타입 없음: " + asset.GetType().FullName);
                        continue;
                    }

                    var fields = dataType.GetFields(
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Instance);

                    // RowKey를 제외한 나머지 필드를 선언 순서 그대로 사용합니다. .NET 명세는
                    // GetFields()의 반환 순서를 보장하지 않지만, 이 프레임워크가 생성하는
                    // 클래스는 항상 TableClassGenerator.WriteTableScript가 쓴 순서 그대로
                    // 컴파일되고 Mono/CoreCLR 둘 다 실제로는 선언 순서를 반환하므로 기댑니다.
                    List<System.Reflection.FieldInfo> orderedFields = new List<System.Reflection.FieldInfo>();
                    for (int f = 0; f < fields.Length; f++)
                    {
                        if (fields[f].Name != "RowKey")
                        {
                            orderedFields.Add(fields[f]);
                        }
                    }

                    bool schemaMismatch = columns.Count != orderedFields.Count;

                    if (!schemaMismatch)
                    {
                        for (int c = 0; c < columns.Count; c++)
                        {
                            // 이름만 비교하면 컬럼 순서가 바뀌거나 타입이 바뀐 경우를 놓칩니다.
                            // 이미 컴파일된 ParseFromTsv는 예전 컬럼 인덱스/타입으로 셀을
                            // 읽으므로, 순서가 바뀌면 엉뚱한 필드에 값이 들어가고 타입이
                            // 바뀌면 조용히 잘못 파싱됩니다. 둘 다 여기서 걸러냅니다.
                            if (orderedFields[c].Name != columns[c].fieldName ||
                                !ColumnTypeMatchesField(columns[c], orderedFields[c].FieldType))
                            {
                                schemaMismatch = true;
                                break;
                            }
                        }
                    }

                    if (schemaMismatch)
                    {
                        Debug.LogWarning(
                            "[DataTableImporter] 스키마가 서로 다릅니다: " + tab.title +
                            "\n열 추가/삭제/이름 변경이 있었다면 '선택 시트 생성'을 다시 실행하세요.");
                        continue;
                    }

                    var method = asset.GetType().GetMethod("ParseFromTsv");
                    if (method == null)
                    {
                        Debug.LogError("[DataTableImporter] ParseFromTsv 메서드 없음: " + asset.GetType().FullName);
                        continue;
                    }

                    method.Invoke(asset, new object[] { tsv });
                    EditorUtility.SetDirty(asset);

                    // "선택 시트 갱신"은 스키마가 그대로면 WriteTableScript를 다시 실행하지
                    // 않으므로(위 스키마 검증 참고), "key:" 컬럼의 enum 동기화는 여기서
                    // 별도로 호출해야 새로 추가/삭제된 행의 키 값이 반영됩니다.
                    TableClassGenerator.SyncKeyEnums(tsv, columns, Path.GetDirectoryName(tab.scriptPath));
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                for (int i = 0; i < _tabs.Count; i++)
                {
                    _tabs[i].assetExists = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_tabs[i].assetPath) != null;
                }

                _status = "갱신 완료.";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isBusy = false;
                SavePrefs();
                Repaint();
            }
        }

        // ColumnInfo가 기대하는 C# 타입과 이미 컴파일된 Data 클래스 필드의 실제 타입이
        // 일치하는지 확인합니다 (배열 여부 포함).
        private static bool ColumnTypeMatchesField(TableClassGenerator.ColumnInfo col, Type fieldType)
        {
            if (col.isArray != fieldType.IsArray)
            {
                return false;
            }

            Type elementType = col.isArray ? fieldType.GetElementType() : fieldType;
            if (elementType == null)
            {
                return false;
            }

            switch (col.type)
            {
                case EDataTableColumnType.Int:
                    return elementType == typeof(int);

                case EDataTableColumnType.Long:
                    return elementType == typeof(long);

                case EDataTableColumnType.Float:
                    return elementType == typeof(float);

                case EDataTableColumnType.Double:
                    return elementType == typeof(double);

                case EDataTableColumnType.Bool:
                    return elementType == typeof(bool);

                case EDataTableColumnType.String:
                    return elementType == typeof(string);

                case EDataTableColumnType.Enum:
                    return elementType.IsEnum && elementType.FullName != null &&
                           elementType.FullName.Replace('+', '.') == col.enumTypeFullName;

                default:
                    return false;
            }
        }

        private void DeleteSelected()
        {
            List<SheetTabInfo> targets = GetSelected();
            if (targets.Count == 0)
            {
                _status = "선택된 시트가 없습니다.";
                SavePrefs();
                return;
            }

            string msg = BuildDeleteConfirmMessage(targets);

            bool ok = EditorUtility.DisplayDialog(
                "삭제 확인",
                msg,
                "삭제",
                "취소");

            if (!ok)
            {
                _status = "삭제 취소.";
                SavePrefs();
                Repaint();
                return;
            }

            _isBusy = true;
            _status = "삭제 중...";
            SavePrefs();

            PerformDelete(targets);
        }

        private string BuildDeleteConfirmMessage(List<SheetTabInfo> targets)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("선택한 테이블을 삭제합니다.");
            sb.AppendLine("삭제 대상(.asset / .cs):");
            sb.AppendLine();

            int show = Mathf.Min(10, targets.Count);
            for (int i = 0; i < show; i++)
            {
                sb.AppendLine("- " + targets[i].title);
            }

            if (targets.Count > show)
            {
                sb.AppendLine("... (+" + (targets.Count - show) + ")");
            }

            return sb.ToString();
        }

        private void PerformDelete(List<SheetTabInfo> targets)
        {
            try
            {
                HashSet<int> deleteGids = new HashSet<int>();

                for (int i = 0; i < targets.Count; i++)
                {
                    SheetTabInfo tab = targets[i];

                    deleteGids.Add(tab.gid);

                    if (!string.IsNullOrEmpty(tab.assetPath))
                    {
                        AssetDatabase.DeleteAsset(tab.assetPath);
                    }

                    if (!string.IsNullOrEmpty(tab.scriptPath))
                    {
                        AssetDatabase.DeleteAsset(tab.scriptPath);
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                for (int i = _tabs.Count - 1; i >= 0; i--)
                {
                    if (deleteGids.Contains(_tabs[i].gid))
                    {
                        _tabs.RemoveAt(i);
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                _status = "삭제 완료.";
            }
            finally
            {
                _isBusy = false;
                SavePrefs();
                Repaint();
            }
        }

        private List<SheetTabInfo> GetSelected()
        {
            List<SheetTabInfo> list = new List<SheetTabInfo>();
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].selected)
                {
                    list.Add(_tabs[i]);
                }
            }
            return list;
        }

        private IEnumerator RequestCoroutine(UnityWebRequest req, Action<bool, string> onDone)
        {
            yield return req.SendWebRequest();

            string body = "";
            if (req.downloadHandler != null)
            {
                body = req.downloadHandler.text;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                string msg =
                    "error=" + req.error +
                    "\ncode=" + req.responseCode +
                    "\nbody=" + body;

                onDone(false, msg);
                yield break;
            }

            onDone(true, body);
        }

        private void EnsureFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder))
            {
                return;
            }

            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            string[] parts = assetFolder.Split('/');
            if (parts.Length == 0)
            {
                return;
            }

            string cur = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(cur, parts[i]);
                }

                cur = next;
            }
        }

        private string EscapeForPrefs(string tsv)
        {
            if (tsv == null)
            {
                return "";
            }

            // payloadLine이 "className|assetPath|tsv" 형태로 '|'를 구분자로 쓰기 때문에,
            // 시트 셀 내용에 들어있는 '|'를 공백으로 지워버리면 데이터가 조용히
            // 손상됩니다. 대신 역이스케이프 가능한 형태로 바꿔서 원본을 보존합니다.
            // '\'를 가장 먼저 이스케이프해야 이후 삽입하는 "\\n"/"\\p" 시퀀스와
            // 원본 데이터에 있던 문자를 구분할 수 있습니다.
            string s = tsv.Replace("\\", "\\\\");
            s = s.Replace("|", "\\p");
            s = s.Replace("\r\n", "\n").Replace("\r", "\n");

            s = s.Replace("\n", "\\n");
            return s;
        }

        // EditorPrefs 대신 EditorUserSettings를 씁니다. EditorPrefs는 이 머신에 설치된
        // 모든 Unity 프로젝트에 걸쳐 전역으로 공유되므로, 같은 PC에서 다른 프로젝트로
        // 전환하면 시트 URL/API Key가 서로 덮어써지거나 새어나갑니다. EditorUserSettings는
        // 프로젝트 로컬의 UserSettings/ 폴더에 저장되고(.gitignore에도 이미 제외되어 있음),
        // 값이 없으면 null을 반환하므로 GetConfig에서 기본값 처리를 대신합니다.
        private static string GetConfig(string key, string defaultValue)
        {
            string value = EditorUserSettings.GetConfigValue(PrefPrefix + key);
            return value ?? defaultValue;
        }

        private void LoadPrefs()
        {
            _sheetUrl = GetConfig("sheetUrl", _sheetUrl);
            _apiKey = GetConfig("apiKey", _apiKey);

            _generatedScriptFolder = GetConfig("genScriptFolder", _generatedScriptFolder);
            _resourcesFolder = GetConfig("resourcesFolder", _resourcesFolder);

            _overwriteScript = GetConfig("overwriteScript", _overwriteScript ? "true" : "false") == "true";

            _status = GetConfig("status", _status);

            string tabsJson = GetConfig("tabsJson", "");
            if (!string.IsNullOrEmpty(tabsJson))
            {
                try
                {
                    TabListWrapper wrapper = JsonUtility.FromJson<TabListWrapper>(tabsJson);
                    if (wrapper != null && wrapper.tabs != null)
                    {
                        _tabs = wrapper.tabs;
                    }
                }
                catch
                {
                }
            }
        }

        private void SavePrefs()
        {
            EditorUserSettings.SetConfigValue(PrefPrefix + "sheetUrl", _sheetUrl ?? "");
            EditorUserSettings.SetConfigValue(PrefPrefix + "apiKey", _apiKey ?? "");

            EditorUserSettings.SetConfigValue(PrefPrefix + "genScriptFolder", _generatedScriptFolder ?? "");
            EditorUserSettings.SetConfigValue(PrefPrefix + "resourcesFolder", _resourcesFolder ?? "");

            EditorUserSettings.SetConfigValue(PrefPrefix + "overwriteScript", _overwriteScript ? "true" : "false");

            TabListWrapper wrapper = new TabListWrapper();
            wrapper.tabs = _tabs;

            string tabsJson = JsonUtility.ToJson(wrapper);
            EditorUserSettings.SetConfigValue(PrefPrefix + "tabsJson", tabsJson);

            EditorUserSettings.SetConfigValue(PrefPrefix + "status", _status ?? "");
        }
    }
}
