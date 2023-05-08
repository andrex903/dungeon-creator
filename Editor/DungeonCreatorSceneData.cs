#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedeevEditor.DungeonCreator
{
    public class DungeonCreatorSceneData : MonoBehaviour
    {
        public Room selectedRoom;
        public bool useRooms = false;

        public BlockData blockData;
        public List<DungeonBlock> blocks = new();
        public DungeonBlock activeBlock = null;

        public bool useGrid = false;
        public int gridSize = 2;
        public bool checkConnections = false;
        public bool scaleRoom = true;

        public BoundsSource boundsSource = BoundsSource.FirstChild;
        public string customChildName = "";

        public List<Room> placeholders = new();
        public List<Room> rooms = new();
        public int prefabIndex = 0;

        [NonSerialized]
        public bool isEditing = false;

        private void OnDrawGizmos()
        {
            if (!isEditing || activeBlock == null) return;

            if (useGrid && isEditing) DrawGrid(gridSize, activeBlock.matrix.scale);

            if (selectedRoom)
            {
                Color old = Gizmos.color;
                Gizmos.color = new(1f, 0.6f, 0f, 0.6f);
                Gizmos.DrawCube(activeBlock.matrix.GetCenter(selectedRoom.transform.position), activeBlock.matrix.scale * Vector3.one);
                Gizmos.color = old;
            }
        }

        private void DrawGrid(int gridSize, float unitSize)
        {
            float gridByUnit = gridSize * unitSize;

            Vector3 gridPos = Vector3.zero;
            Vector3 gridRight = Vector3.right;
            Vector3 gridForward = Vector3.forward;

            Color oldCol = Gizmos.color;
            Gizmos.color = new Color(oldCol.r, oldCol.g, oldCol.b, 0.5f);

            Vector3 forPos = gridPos + gridForward * gridByUnit;
            Vector3 negForPos = gridPos + gridForward * -gridByUnit;
            Vector3 horzPos = gridPos + gridRight * gridByUnit;
            Vector3 negHorzPos = gridPos + gridRight * -gridByUnit;
            for (int i = 0; i <= gridSize; i++)
            {
                if (i == 0)
                {
                    Gizmos.color = new Color(0.3f, 0.3f, 1f, 1f);
                    Gizmos.DrawLine(negForPos, forPos);
                    Gizmos.color = new Color(1f, 0f, 0f, 1f);
                    Gizmos.DrawLine(negHorzPos, horzPos);
                    Gizmos.color = new Color(oldCol.r, oldCol.g, oldCol.b, 0.3f);
                }
                else
                {
                    Gizmos.DrawLine(negForPos + gridRight * i * unitSize, forPos + gridRight * i * unitSize);
                    Gizmos.DrawLine(negForPos + gridRight * -i * unitSize, forPos + gridRight * -i * unitSize);

                    Gizmos.DrawLine(negHorzPos + gridForward * i * unitSize, horzPos + gridForward * i * unitSize);
                    Gizmos.DrawLine(negHorzPos + gridForward * -i * unitSize, horzPos + gridForward * -i * unitSize);
                }
            }

            Gizmos.color = new Color(0.3f, 1f, 1f, 0.1f);
            Gizmos.DrawCube(Vector3.zero, new(gridByUnit * 2, 0.1f, gridByUnit * 2));

            Gizmos.color = oldCol;
        }

        #region Blocks

        public void ActivateBlock(DungeonBlock block)
        {
            DeactivateAllBlocks();
            block.Activate();
            activeBlock = block;
        }

        public void DeactivateAllBlocks()
        {
            foreach (var block in blocks) block.Deactivate();
            selectedRoom = null;
            activeBlock = null;
        }

        public DungeonBlock CreateNewBlock(string name)
        {
            DungeonBlock block = new() { name = name };
            blocks.Add(block);
            block.transform = new GameObject(name).transform;
            return block;
        }

        public void RemoveBlock(DungeonBlock block)
        {
            blocks.Remove(block);
            if (block.transform) DestroyImmediate(block.transform.gameObject);
        }

        #endregion
    }
}
#endif