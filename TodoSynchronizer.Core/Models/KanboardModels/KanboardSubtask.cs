using Newtonsoft.Json;

namespace TodoSynchronizer.Core.Models.KanboardModels
{
    /// <summary>
    /// Represents a Kanboard subtask. Maps to Canvas submission/grade info and comments.
    /// Status: 0 = todo, 1 = in-progress, 2 = done.
    /// </summary>
    public class KanboardSubtask
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("task_id")]
        public int TaskId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        /// <summary>0 = todo, 1 = in-progress, 2 = done.</summary>
        [JsonProperty("status")]
        public int Status { get; set; }

        [JsonProperty("user_id")]
        public int UserId { get; set; }

        [JsonProperty("time_estimated")]
        public float TimeEstimated { get; set; }

        [JsonProperty("time_spent")]
        public float TimeSpent { get; set; }
    }
}
