using UnityEngine;

namespace RedeevEditor.DungeonCreator
{
    public class Room : MonoBehaviour
    {
        [EnumFlags]
        public Direction connectionType;             
    }
}