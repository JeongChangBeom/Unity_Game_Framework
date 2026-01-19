using UnityEngine;

// AUTO-GENERATED. DO NOT EDIT.
public class DataTables
{
    public Sound Sound { get; private set; }

    public void Init()
    {
        Sound = TableLoader.Load<Sound>("GeneratedTables/Sound");
    }
}
