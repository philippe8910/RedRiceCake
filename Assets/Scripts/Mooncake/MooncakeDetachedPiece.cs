using UnityEngine;

/// <summary>
/// 從印章脫離、掉到桌上讓玩家用手拿的那顆月餅。
///
/// 只是一個回頭指向來源站點的標記：烤盤格子收到它的時候要通知站點放行，
/// 站點才會開下一顆的提示。不掛這個的話，站點會一直以為手上還有月餅。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeDetachedPiece : MonoBehaviour
{
    [Tooltip("把這顆月餅蓋出來的站點；放上烤盤時要回報給它")]
    public MooncakeMoldStation source;

    /// <summary>烤盤格子收下之後呼叫：通知站點、然後把自己收掉。</summary>
    public void Consume()
    {
        if (source != null) source.ReleaseMooncake();

        // 用 SetActive 而不是 Destroy —— 站點下一輪還要重新用同一顆
        gameObject.SetActive(false);
    }
}
