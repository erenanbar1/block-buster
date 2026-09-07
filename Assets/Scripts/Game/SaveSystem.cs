using System;
using System.Collections.Generic;
using BlockBlast.Core;
using UnityEngine;

namespace BlockBlast.Game
{
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int score;
        public int combo;
        public int boardSize;
        public int[] cells;
        public string[] trayShapeIds;   // empty string = consumed slot
        public int[] trayColors;
        public int generatorSeed;
    }

    /// <summary>
    /// Best score plus a full resume snapshot, both in PlayerPrefs so it survives the app
    /// being swiped away on a phone.
    /// </summary>
    public static class SaveSystem
    {
        const string BestKey = "bb.best";
        const string StateKey = "bb.state";
        const string MutedKey = "bb.muted";

        public static bool LoadMuted() => PlayerPrefs.GetInt(MutedKey, 0) == 1;

        public static void SaveMuted(bool muted)
        {
            PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static int LoadBest() => PlayerPrefs.GetInt(BestKey, 0);

        public static void SaveBest(int value)
        {
            PlayerPrefs.SetInt(BestKey, value);
            PlayerPrefs.Save();
        }

        public static bool HasSavedGame => PlayerPrefs.HasKey(StateKey);

        public static void SaveGame(SaveData data)
        {
            PlayerPrefs.SetString(StateKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static SaveData LoadGame()
        {
            if (!PlayerPrefs.HasKey(StateKey)) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(StateKey));
                return IsValid(data) ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BlockBlast] Discarding corrupt save: " + e.Message);
                return null;
            }
        }

        public static void ClearGame()
        {
            PlayerPrefs.DeleteKey(StateKey);
            PlayerPrefs.Save();
        }

        static bool IsValid(SaveData d)
        {
            if (d == null || d.cells == null || d.trayShapeIds == null || d.trayColors == null) return false;
            if (d.boardSize <= 0 || d.cells.Length != d.boardSize * d.boardSize) return false;
            if (d.trayShapeIds.Length != d.trayColors.Length) return false;
            foreach (var id in d.trayShapeIds)
                if (!string.IsNullOrEmpty(id) && PieceLibrary.ById(id) == null) return false;
            return true;
        }

        public static SaveData Capture(BoardModel board, IList<PieceInstance> tray, int score, int combo, int seed)
        {
            var data = new SaveData
            {
                score = score,
                combo = combo,
                boardSize = board.Size,
                cells = board.Snapshot(),
                trayShapeIds = new string[tray.Count],
                trayColors = new int[tray.Count],
                generatorSeed = seed
            };
            for (int i = 0; i < tray.Count; i++)
            {
                data.trayShapeIds[i] = tray[i]?.Shape.Id ?? string.Empty;
                data.trayColors[i] = tray[i]?.ColorIndex ?? 0;
            }
            return data;
        }
    }
}
