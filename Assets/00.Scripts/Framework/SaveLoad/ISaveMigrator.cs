public interface ISaveMigrator
{
    int FromVersion { get; }
    int ToVersion { get; }

    void Migrate(ISaveProvider provider);
}
