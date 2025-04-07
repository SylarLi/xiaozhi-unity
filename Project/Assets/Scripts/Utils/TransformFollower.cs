using UnityEngine;

namespace XiaoZhi.Unity
{
    public class TransformFollower : MonoBehaviour
    {
        [SerializeField] private Vector3 _position;
        [SerializeField] private Quaternion _rotation;
        
        private Transform _follower;

        public void SetFollower(Transform tr)
        {
            _follower = tr;
        }

        public void LateUpdate()
        {
            if (!_follower) return;
            var finalMatrix = transform.localToWorldMatrix * Matrix4x4.TRS(_position, _rotation, Vector3.one);
            _follower.position = finalMatrix.GetPosition();
            _follower.rotation = finalMatrix.rotation;
        }
    }
}