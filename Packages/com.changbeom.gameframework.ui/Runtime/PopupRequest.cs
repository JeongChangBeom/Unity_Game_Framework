using System;

namespace GameFramework.UI
{
    [Serializable]
    public class PopupRequest
    {
        public UIPopupBase prefab;
        public UIPopupBase instance;

        public EPopupPriority priority;
        public object payload;
        public bool unique;
        public int sequence;
        public Action<object> onResult;

        public Type PopupType
        {
            get
            {
                if (instance != null)
                {
                    return instance.GetType();
                }

                if (prefab != null)
                {
                    return prefab.GetType();
                }

                return null;
            }
        }
    }
}
