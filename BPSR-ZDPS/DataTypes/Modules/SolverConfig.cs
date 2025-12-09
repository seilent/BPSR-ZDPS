using System.Text;

namespace BPSR_ZDPS.DataTypes.Modules
{
    public class SolverConfig
    {
        public List<int> Qualities = [];
        public List<StatPrio> StatPrioritys = [];

        public string SaveToString(bool asBase64 = false)
        {
            var sb = new StringBuilder();
            sb.Append("ZMO:");
            for (int i = 0; i < StatPrioritys.Count; i++)
            {
                var stat = $"{StatPrioritys[i].Id}-{StatPrioritys[i].MinLevel}";
                sb.Append($"{stat}{(StatPrioritys.Count - 1 == i ? "" : ",")}");
            }

            sb.Append("|");
            for (int i = 0; i < Qualities.Count; i++)
            {
                sb.Append($"{Qualities[i]}{(Qualities.Count - 1 == i ? "" : ",")}");
            }

            if (asBase64)
            {
                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
                return System.Convert.ToBase64String(plainTextBytes);
            }

            return sb.ToString();
        }

        public void FromString(string str)
        {
            try
            {
                if (str.StartsWith("ZMO:"))
                {
                    Qualities.Clear();
                    StatPrioritys.Clear();

                    var configParts = str.Substring(4).Split('|');
                    if (configParts.Length > 1)
                    {
                        var statPriorites = configParts[0].Split(",");
                        foreach (var stat in statPriorites)
                        {
                            var statParts = stat.Split("-");
                            var prio = new StatPrio()
                            {
                                Id = int.Parse(statParts[0]),
                                MinLevel = int.Parse(statParts[1])
                            };

                            StatPrioritys.Add(prio);
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }
    }
}
