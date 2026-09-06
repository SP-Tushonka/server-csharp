namespace PMCGenerator.Models
{
    public class WeaponDetails
    {
        public WeaponDetails(string presetName, string id, string templateId)
        {
            Id = id;
            TemplateId = templateId;
            PresetName = presetName;
        }

        public string PresetName { get; set; }
        public string Id { get; set; }
        public string TemplateId { get; set; }
    }

    public class ModDetails
    {
        public ModDetails(string slotId, string id, string templateId, string parentId, string parentTemplateId)
        {
            SlotId = slotId;
            Id = id;
            TemplateId = templateId;
            ParentId = parentId;
            ParentTemplateId = parentTemplateId;
        }

        public string SlotId { get; set; }
        public string Id { get; set; }
        public string TemplateId { get; set; }
        public string ParentId { get; set; }
        public string ParentTemplateId { get; set; }
    }
}
