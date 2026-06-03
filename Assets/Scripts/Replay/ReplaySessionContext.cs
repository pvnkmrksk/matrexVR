/// <summary>
/// Active replay session state shared by config loaders and pose appliers.
/// </summary>
public static class ReplaySessionContext
{
    public static bool IsActive { get; private set; }
    public static ReplaySessionArchive Archive { get; private set; }
    public static ReplaySceneOrchestrator Orchestrator { get; private set; }

    public static void Activate(ReplaySessionArchive archive, ReplaySceneOrchestrator orchestrator)
    {
        Archive = archive;
        Orchestrator = orchestrator;
        IsActive = true;
    }

    public static void Deactivate()
    {
        IsActive = false;
        Archive = null;
        Orchestrator = null;
    }
}
