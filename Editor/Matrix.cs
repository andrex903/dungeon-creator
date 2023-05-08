using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedeevEditor.DungeonCreator
{
    [Serializable]
    public class Matrix
    {
        public float scale = 1f;
        public List<Element> elements;

        public Matrix(float scale)
        {
            this.scale = scale;
            elements = new();
        }

        public Matrix Clone()
        {
            Matrix clone = new(scale);
            foreach (var element in elements)
            {
                Element newElement = new()
                {
                    i = element.i,
                    j = element.j,
                    connections = element.connections
                };

                clone.elements.Add(newElement);
            }
            return clone;
        }

        public Element Get(Vector3 point)
        {
            return Get(GetIndex(point.x), GetIndex(point.z));
        }

        public Element Get(Vector3 point, Direction direction)
        {
            int i = GetIndex(point.x);
            int j = GetIndex(point.z);

            return direction switch
            {
                Direction.North => Get(i, j + 1),
                Direction.South => Get(i, j - 1),
                Direction.East => Get(i + 1, j),
                Direction.West => Get(i - 1, j),
                _ => null,
            };
        }

        public bool TryGet(Vector3 worldPoint, out Element element)
        {
            element = Get(worldPoint);
            return element != null;
        }

        public Element Get(int i, int j)
        {
            for (int k = 0; k < elements.Count; k++)
            {
                if (i == elements[k].i && j == elements[k].j) return elements[k];
            }
            return null;
        }

        public void Add(Element element, Vector3 point)
        {
            element.i = GetIndex(point.x);
            element.j = GetIndex(point.z);
            elements.Add(element);
        }

        public void Remove(Element element)
        {
            elements.Remove(element);
        }

        private int GetIndex(float value)
        {
            return Mathf.FloorToInt(value / scale);
        }

        public int IndexOf(Vector3 worldPoint)
        {
            if (TryGet(worldPoint, out Element element)) return elements.IndexOf(element);
            return -1;
        }

        public Vector3 GetCenter(Vector3 point)
        {
            return GetCenter(GetIndex(point.x), GetIndex(point.z));
        }

        public Vector3 GetCenter(int i, int j)
        {
            return new((i + 0.5f) * scale, 0f, (j + 0.5f) * scale);
        }

        public Direction GetRequiredDirections(Vector3 point)
        {
            Direction requiredDirections = 0;

            CheckDirection(Direction.North);
            CheckDirection(Direction.South);
            CheckDirection(Direction.East);
            CheckDirection(Direction.West);

            return requiredDirections;

            void CheckDirection(Direction direction)
            {
                Element element = Get(point, direction);
                if (element != null && element.connections.Has(GetMatchingConnectionType(direction)))
                {
                    requiredDirections = requiredDirections.Add(direction);
                }
            }
        }

        public Direction GetForbiddenDirections(Vector3 point)
        {
            Direction forbiddenDirections = 0;

            CheckDirection(Direction.North);
            CheckDirection(Direction.South);
            CheckDirection(Direction.East);
            CheckDirection(Direction.West);

            return forbiddenDirections;

            void CheckDirection(Direction direction)
            {
                Element element = Get(point, direction);
                if (element != null && !element.connections.Has(GetMatchingConnectionType(direction)))
                {
                    forbiddenDirections = forbiddenDirections.Add(direction);
                }
            }
        }

        private Direction GetMatchingConnectionType(Direction type)
        {
            return type switch
            {
                Direction.North => Direction.South,
                Direction.South => Direction.North,
                Direction.East => Direction.West,
                Direction.West => Direction.East,
                _ => Direction.South,
            };
        }
    }

    [Serializable]
    public class Element
    {
        public int i = 0;
        public int j = 0;        
        public Direction connections;
    }
}