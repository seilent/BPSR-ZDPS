namespace BPSR_ZDPS.DataTypes
{
    public class SceneEventDungeonConfigTable
    {
        public Dictionary<string, SceneEventDungeonConfig> Data = [];
    }

    public class SceneEventDungeonConfig
    {
        public int DungeonId { get; set; }
        public int BaseRatio { get; set; }
        public int LimitTime { get; set; }
        public List<List<int>> ScoreRank { get; set; }
    }
}
