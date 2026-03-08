using Newtonsoft.Json;

namespace TodoSynchronizer.Core.Models.KanboardModels
{
    public class KanboardCategory
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("project_id")]
        public int ProjectId { get; set; }

        [JsonProperty("color_id")]
        public string ColorId { get; set; }
    }
}
