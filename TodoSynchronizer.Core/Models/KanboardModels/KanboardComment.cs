using Newtonsoft.Json;

namespace TodoSynchronizer.Core.Models.KanboardModels
{
    /// <summary>
    /// Represents a Kanboard task comment.
    /// The <see cref="Comment"/> field is used to store the Canvas item URL
    /// for identity tracking (analogous to LinkedResource.WebUrl in MS Todo).
    /// </summary>
    public class KanboardComment
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("task_id")]
        public int TaskId { get; set; }

        [JsonProperty("user_id")]
        public int UserId { get; set; }

        [JsonProperty("date_creation")]
        public long DateCreation { get; set; }

        /// <summary>Comment body. Stores the Canvas item HtmlUrl for identity tracking.</summary>
        [JsonProperty("comment")]
        public string Comment { get; set; }
    }
}
