namespace ZombiCar.Core
{
    /// <summary>The achievements: what each is for and its coin reward (claimed in the menu's "ДОСТИЖЕНИЯ").</summary>
    public static class Achievements
    {
        public readonly struct Info
        {
            public readonly string Id, Title, Description;
            public readonly int Reward;

            public Info(string id, string title, string description, int reward)
            {
                Id = id;
                Title = title;
                Description = description;
                Reward = reward;
            }
        }

        public static readonly Info[] All =
        {
            new Info(MetaProgress.MadmanAchievement, "БЕЗУМЕЦ!", "Пройди все 30 волн на сложности «ХАРДКОР»", 676),
        };

        public static Info Get(string id)
        {
            foreach (Info info in All)
                if (info.Id == id)
                    return info;
            return default;
        }
    }
}
