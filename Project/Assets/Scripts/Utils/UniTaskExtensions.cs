using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace XiaoZhi.Unity
{
    public static class UniTaskExtensions
    {
        public static UniTask<bool>.Awaiter GetAwaiter(this Tweener tweener)
        {
            var completionSource = new UniTaskCompletionSource<bool>();
            tweener.OnComplete(() => completionSource.TrySetResult(true));
            tweener.OnKill(() => completionSource.TrySetResult(false));
            return completionSource.Task.GetAwaiter();
        }
    }
}