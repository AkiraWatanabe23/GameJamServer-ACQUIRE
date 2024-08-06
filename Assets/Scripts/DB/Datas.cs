using System;

namespace Data
{
    /// <summary> アクセスのキーとなる基底データ </summary>
    [Serializable]
    public class AbstractData
    {
        public string UserID;
    }

    namespace Demo
    {
        [Serializable]
        public class DemoData : AbstractData
        {
            public string Name;
            public int Score;
        }
    }

    namespace Master
    {
        [Serializable]
        public class ScoreData : AbstractData
        {
            public int Score;
        }

        [Serializable]
        public class VersionData : AbstractData
        {
            public int Version;
        }
    }
}
