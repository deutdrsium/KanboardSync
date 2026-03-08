using Newtonsoft.Json;

namespace TodoSynchronizer.Core.Models.KanboardModels
{
    public class KanboardProject
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>1 = active, 0 = inactive.</summary>
        [JsonProperty("is_active")]
        public int IsActive { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }
}
