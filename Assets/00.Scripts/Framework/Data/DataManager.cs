public class DataManager : MonoSingleton<DataManager>    
{
    public DataTables Tables { get; private set; }

    protected override void OnInitialize()
    {
        Tables = new DataTables();
        Tables.Init();
    }
}
