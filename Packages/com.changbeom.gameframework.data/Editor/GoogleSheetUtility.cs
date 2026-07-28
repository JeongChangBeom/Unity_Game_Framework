using System;
using System.Text.RegularExpressions;
using UnityEngine.Networking;

namespace GameFramework.DataParsing.Editor
{
    public static class GoogleSheetUtility
    {
        [Serializable]
        public class TabsResponse
        {
            public SheetWrapper[] sheets;
        }

        [Serializable]
        public class SheetWrapper
        {
            public SheetProperties properties;
        }

        [Serializable]
        public class SheetProperties
        {
            public int sheetId;
            public string title;
        }

        public static bool TryExtractSpreadsheetId(string sheetUrl, out string spreadsheetId)
        {
            spreadsheetId = string.Empty;

            if (string.IsNullOrEmpty(sheetUrl))
            {
                return false;
            }

            Match match = Regex.Match(sheetUrl, @"/spreadsheets/d/([a-zA-Z0-9-_]+)");
            if (!match.Success)
            {
                return false;
            }

            spreadsheetId = match.Groups[1].Value;
            return true;
        }

        public static UnityWebRequest BuildTabsRequest(string spreadsheetId, string apiKey)
        {
            string url =
                "https://sheets.googleapis.com/v4/spreadsheets/" + spreadsheetId +
                "?fields=sheets(properties(sheetId,title))&key=" + apiKey;

            return UnityWebRequest.Get(url);
        }

        public static UnityWebRequest BuildTsvRequest(string spreadsheetId, int gid)
        {
            string url =
                "https://docs.google.com/spreadsheets/d/" + spreadsheetId +
                "/export?format=tsv&gid=" + gid;

            return UnityWebRequest.Get(url);
        }
    }
}
