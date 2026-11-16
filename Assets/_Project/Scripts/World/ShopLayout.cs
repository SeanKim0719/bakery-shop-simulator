using System;
using System.Collections.Generic;
using UnityEngine;

namespace BakeryShop.World
{
    /// <summary>
    /// 매장의 지점 좌표. 손님(입장, 줄, 진열대 앞)과 배송이 쓴다.
    /// 좌표의 원본은 Data/shop_layout.json이고, 씬 빌더가 읽어서 여기에 채운다.
    /// 지점을 빈 오브젝트(Transform)로 두지 않은 이유: 씬에서 실수로 끌어 옮기면 평면도와 어긋난다.
    /// </summary>
    public sealed class ShopLayout : MonoBehaviour
    {
        /// <summary>진열대 하나의 칸 위치와, 칸마다 손님이 서는 자리.</summary>
        [Serializable]
        public struct ShelfPoints
        {
            public string ShelfId;

            /// <summary>칸 위에 빵이 놓이는 자리 (칸의 중심, 진열 면의 높이).</summary>
            public Vector3[] SlotPositions;

            /// <summary>그 칸을 보려고 손님이 서는 바닥 위의 자리.</summary>
            public Vector3[] StandPoints;
        }

        [SerializeField] private Vector3 _playerStart;
        [SerializeField] private Vector3 _deliveryPoint;
        [SerializeField] private Vector3 _entryPoint;
        [SerializeField] private Vector3[] _spawnPoints = new Vector3[0];
        [SerializeField] private Vector3[] _queuePoints = new Vector3[0];
        [SerializeField] private ShelfPoints[] _shelves = new ShelfPoints[0];

        public Vector3 PlayerStart => _playerStart;

        /// <summary>주문한 박스가 놓이는 자리 (문밖 인도).</summary>
        public Vector3 DeliveryPoint => _deliveryPoint;

        /// <summary>손님이 문을 지나 매장 안에 처음 서는 자리.</summary>
        public Vector3 EntryPoint => _entryPoint;

        public int SpawnPointCount => _spawnPoints.Length;

        /// <summary>줄의 자리 수. 0번이 계산 위치다.</summary>
        public int QueuePointCount => _queuePoints.Length;

        /// <summary>손님이 생기고 사라지는 자리. 0: 서쪽, 1: 동쪽.</summary>
        public Vector3 SpawnPoint(int index)
        {
            return At(_spawnPoints, index, nameof(index));
        }

        /// <summary>0: 계산 위치, 1 이상: 줄.</summary>
        public Vector3 QueuePoint(int index)
        {
            return At(_queuePoints, index, nameof(index));
        }

        public int SlotCount(string shelfId)
        {
            return Find(shelfId).SlotPositions.Length;
        }

        /// <summary>그 칸을 보려고 손님이 서는 자리.</summary>
        public Vector3 SlotStandPoint(string shelfId, int slotIndex)
        {
            return At(Find(shelfId).StandPoints, slotIndex, nameof(slotIndex));
        }

        /// <summary>칸 위에 빵이 놓이는 자리.</summary>
        public Vector3 SlotPosition(string shelfId, int slotIndex)
        {
            return At(Find(shelfId).SlotPositions, slotIndex, nameof(slotIndex));
        }

        /// <summary>씬 빌더가 좌표를 채운다.</summary>
        internal void Configure(
            Vector3 playerStart,
            Vector3 deliveryPoint,
            Vector3 entryPoint,
            Vector3[] spawnPoints,
            Vector3[] queuePoints,
            ShelfPoints[] shelves)
        {
            _playerStart = playerStart;
            _deliveryPoint = deliveryPoint;
            _entryPoint = entryPoint;
            _spawnPoints = spawnPoints ?? new Vector3[0];
            _queuePoints = queuePoints ?? new Vector3[0];
            _shelves = shelves ?? new ShelfPoints[0];
        }

        private ShelfPoints Find(string shelfId)
        {
            for (int i = 0; i < _shelves.Length; i++)
            {
                if (_shelves[i].ShelfId == shelfId)
                {
                    return _shelves[i];
                }
            }

            throw new KeyNotFoundException("진열대 ID '" + shelfId + "'가 매장 배치에 없습니다.");
        }

        private static Vector3 At(Vector3[] points, int index, string parameterName)
        {
            if (index < 0 || index >= points.Length)
            {
                throw new ArgumentOutOfRangeException(parameterName, "지점 번호 " + index + "가 범위(0 ~ " + (points.Length - 1) + ")를 벗어납니다.");
            }

            return points[index];
        }

        // 선택했을 때 Scene 뷰에 지점을 그린다. 평면도와 눈으로 비교할 수 있다.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_playerStart, 0.2f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_deliveryPoint, 0.25f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_entryPoint, 0.2f);
            foreach (Vector3 point in _spawnPoints)
            {
                Gizmos.DrawWireSphere(point, 0.3f);
            }

            foreach (Vector3 point in _queuePoints)
            {
                Gizmos.DrawWireSphere(point, 0.15f);
            }

            foreach (ShelfPoints shelf in _shelves)
            {
                Gizmos.color = Color.cyan;
                foreach (Vector3 point in shelf.StandPoints)
                {
                    Gizmos.DrawWireSphere(point, 0.12f);
                }

                Gizmos.color = Color.magenta;
                foreach (Vector3 point in shelf.SlotPositions)
                {
                    Gizmos.DrawWireCube(point, new Vector3(0.5f, 0.02f, 0.7f));
                }
            }
        }
    }
}
