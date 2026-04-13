using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    [Serializable]
    public class DungeonState
    {
        public string LocationId;
        public Vector3 PlayerPosition;
        public float PlayerRotationY;
        public float CameraOrbitAngle;
        public float CameraDistance;
        public HashSet<int> VisitedRooms = new();
        public HashSet<int> TriggeredEncounters = new();
        public int RoomCount;
        public int BossRoomIndex = -1;
        public bool BossDefeated;

        public void VisitRoom(int roomIndex) => VisitedRooms.Add(roomIndex);
        public void TriggerEncounter(int encounterIndex) => TriggeredEncounters.Add(encounterIndex);
        public bool IsCleared => BossDefeated;
    }
}
