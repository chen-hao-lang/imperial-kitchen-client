using YooAsset;

namespace LiteGameFramework
{
    public class PatchManager : SingletonMono<PatchManager>
{
    private StateMachine machine;

    public void Init(string _packageNam, EPlayMode _ePlayMode)
    {
        machine = new StateMachine();

        machine.AddBlackboardData("PackageName", _packageNam);
        machine.AddBlackboardData("PlayMode", _ePlayMode);
        machine.AddBlackboardData("InitComplete", false);
        machine.AddBlackboardData("RequesetPackageMainFestComplete", false);
        machine.AddBlackboardData("UpdatePackageMainFestComplete", false);
        machine.AddBlackboardData("DownloadComplete", false);
        machine.AddBlackboardData("ClearCacheBundleComplete", false);
        machine.AddBlackboardData("PatchFailed", false);
        machine.AddBlackboardData("PatchErrorStep", "");
        machine.AddBlackboardData("PatchErrorMessage", "");
        machine.AddBlackboardData("MainSceneName", "Main");

        FSMInitializePackage fSMInitializePackage = new FSMInitializePackage(machine);
        FSMRequestPackageVersion fSMRequestPackageVersion = new FSMRequestPackageVersion(machine);
        FSMUpdatePackageMainFest fSMUpdatePackageMainFest = new FSMUpdatePackageMainFest(machine);
        FSMDownloadPackage fSMDownloadPackage = new FSMDownloadPackage(machine);
        FSMClearCacheBundle fSMClearCacheBundle = new FSMClearCacheBundle(machine);
        FSMStartGame fSMStartGame = new FSMStartGame(machine);
        FSMPatchFailed fSMPatchFailed = new FSMPatchFailed(machine);

        machine.AddTransition(fSMInitializePackage, fSMRequestPackageVersion, () =>
            machine.GetBlackboardData("InitComplete") is true);

        machine.AddTransition(fSMRequestPackageVersion, fSMUpdatePackageMainFest, () =>
            machine.GetBlackboardData("RequesetPackageMainFestComplete") is true
        );

        machine.AddTransition(fSMUpdatePackageMainFest, fSMDownloadPackage, () =>
            machine.GetBlackboardData("UpdatePackageMainFestComplete") is true);

        machine.AddTransition(fSMDownloadPackage, fSMClearCacheBundle, () =>
            machine.GetBlackboardData("DownloadComplete") is true);

        machine.AddTransition(fSMClearCacheBundle, fSMStartGame, () =>
            machine.GetBlackboardData("ClearCacheBundleComplete") is true);

        // 任何状态都可以在失败时切入失败状态
        machine.AddAnyTransition(fSMPatchFailed, () =>
            machine.GetBlackboardData("PatchFailed") is true);

        // 设置状态机的起始状态
        machine.SetState(fSMInitializePackage);
    }

    // Update is called once per frame
    void Update()
    {
        if (machine != null)
        {
            machine.Tick();
        }
    }
}
}