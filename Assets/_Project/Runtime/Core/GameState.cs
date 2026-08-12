namespace AddressablesSample.Game.Core
{
    public enum GameState
    {
        Uninitialized,
        LoadingStartupFallback,
        LoadingStartupPrefab,
        LoadingRound,
        Ready,
        FatalError,
        Disposed
    }
}
