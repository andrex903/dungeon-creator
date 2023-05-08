using UnityEngine;

namespace RedeevEditor.DungeonCreator
{
    public class Room : MonoBehaviour
    {
        public Vector3 boundCenter;
        [EnumFlags]
        public Direction connections;             
    }
}