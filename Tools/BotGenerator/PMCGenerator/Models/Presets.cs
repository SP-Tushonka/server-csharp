using System.Collections.Generic;

namespace PMCGenerator
{
    public class Presets
    {
        public Dictionary<string, Weapon> weaponbuilds { get; set; }
    }

    public class Weapon
    {
        public string id { get; set; }
        public string name { get; set; }
        public string root { get; set; }
        public List<Module> items { get; set; }
    }

    public class Module
    {
        public string _id { get; set; }
        public string _tpl { get; set; }
        public Dictionary<string, object> upd { get; set; }
        public string parentId { get; set; }
        public string slotId { get; set; }
    }

    public class Upd
    {
        public Dictionary<string, Repairable> Repairable { get; set; }
        public Dictionary<string, FireMode> FireMode { get; set; }
        public object Sight { get; set; }
    }

    public class Repairable
    {
        public int MaxDurability { get; set; }
        public int Durability { get; set; }
    }

    public class FireMode
    {
        public string fireMode { get; set; }
    }
}
