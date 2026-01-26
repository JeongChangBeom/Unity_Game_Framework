using UnityEngine;

public sealed class TimeBootstrap : MonoBehaviour
{
    public static TimeManager Time { get; private set; }

    private void Awake()
    {
        if (Time != null)
        {
            return;
        }

        TimeFrameworkConfig config = TimeFrameworkConfig.DefaultUtc();
        IMonotonicClock mono = new StopwatchMonotonicClock();

        Time = new TimeManager(config, SaveManager.Instance, mono);
    }

    private void OnApplicationPause(bool pause)
    {
        if (Time == null)
        {
            return;
        }

        if (pause)
        {
            Time.OnAppPause();
        }
        else
        {
            Time.OnAppResume();
        }
    }
}
