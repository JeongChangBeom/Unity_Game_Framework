using UnityEngine;
using UnityEngine.UI;

public class UIPopup_TestA : UIPopupBase
{
    [SerializeField] private Button _closeButton;
    [SerializeField] private float _closeDelay = 0.25f;

    private void Awake()
    {
        if (_closeButton == null)
        {
            _closeButton = GetComponentInChildren<Button>(true);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(CloseSelf);
        }
    }

    public override void OnOpen(object payload)
    {
        base.OnOpen(payload);
        Debug.Log("Popup A Open");
    }

    protected override void PlayCloseAnimation()
    {
        Debug.Log("Popup A Close Animation Start");
        Invoke(nameof(FinishClose), _closeDelay);
    }

    private void FinishClose()
    {
        Debug.Log("Popup A Close Animation End");
        CompleteClose();
    }

    public override void OnBeforeReturnToPool()
    {
        base.OnBeforeReturnToPool();
        Debug.Log("Popup A ReturnToPool");
    }

    private void OnDestroy()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(CloseSelf);
        }

        CancelInvoke();
    }
}
