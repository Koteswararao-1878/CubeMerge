using UnityEngine;

namespace CubeMerge
{
    /// <summary>
    /// Starts the game automatically when you press Play, in any scene (even an empty one).
    /// Delete this file if you'd rather add CubeGameManager to a GameObject yourself.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GameAssets.FindOne<CubeGameManager>() != null) return;
            var go = new GameObject("CubeMergeGame");
            go.AddComponent<CubeGameManager>();
        }
    }
}
