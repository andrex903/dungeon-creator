#if UNITY_EDITOR
using UnityEngine;

namespace RedeevEditor.DungeonCreator
{
    public class Room : MonoBehaviour
    {
        [HideInInspector]
        public Vector3 boundCenter;
        [EnumFlags]
        public Direction connections;
    }
}
#endif