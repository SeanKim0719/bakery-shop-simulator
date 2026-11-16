using UnityEngine;

namespace BakeryShop.World
{
    /// <summary>
    /// 씬에 놓인 가구의 고정 ID (예: oven.01). 세이브와 해금이 이 ID로 가구를 찾는다.
    /// 오브젝트 이름이나 계층 위치로 찾지 않는 이유: 씬을 정리하다 이름을 바꾸면 세이브가 깨진다.
    /// </summary>
    public sealed class FurnitureId : MonoBehaviour
    {
        [SerializeField] private string _id = "";

        public string Id => _id;

        /// <summary>씬 빌더가 ID를 정한다.</summary>
        internal void SetId(string id)
        {
            _id = id ?? "";
        }
    }
}
