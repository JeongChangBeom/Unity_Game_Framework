using UnityEngine;

public static class TableLoader
{
    public static T Load<T>(string resourcesPath) where T : ScriptableObject
    {
        return Resources.Load<T>(resourcesPath);
    }
}
