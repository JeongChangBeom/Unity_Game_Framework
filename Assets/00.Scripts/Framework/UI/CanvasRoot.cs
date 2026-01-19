using UnityEngine;

public class CanvasRoot : MonoBehaviour
{
    public Canvas Canvas { get; private set; }

    public Transform HudRoot { get; private set; }
    public Transform PopupRoot { get; private set; }
    public Transform OverlayRoot { get; private set; }

    private void Awake()
    {
        Canvas = GetComponent<Canvas>();
        if (Canvas == null)
        {
            Canvas = gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 0;
        }

        if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        HudRoot = CreateRoot("[HUDRoot]");
        PopupRoot = CreateRoot("[PopupRoot]");
        OverlayRoot = CreateRoot("[OverlayRoot]");
    }

    private Transform CreateRoot(string name)
    {
        GameObject go = new GameObject(name);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }
}