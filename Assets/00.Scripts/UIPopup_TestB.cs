using GameFramework.UISystem;
using UnityEngine;
using UnityEngine.UI;

public class UIPopup_TestB : UIPopupBase
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
        Debug.Log("[UIPopup_TestB] 팝업 열림");
    }

    protected override void PlayCloseAnimation()
    {
        Debug.Log("[UIPopup_TestB] 닫기 애니메이션 시작");
        Invoke(nameof(FinishClose), _closeDelay);
    }

    private void FinishClose()
    {
        Debug.Log("[UIPopup_TestB] 닫기 애니메이션 종료");
        CompleteClose();
    }

    public override void OnBeforeReturnToPool()
    {
        base.OnBeforeReturnToPool();
        Debug.Log("[UIPopup_TestB] 풀로 반환");
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
