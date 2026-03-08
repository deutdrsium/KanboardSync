using Newtonsoft.Json;
using System;

namespace TodoSynchronizer.Core.Models.KanboardModels
{
    public class KanboardTask
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>Unix timestamp. 0 means no due date.</summary>
        [JsonProperty("date_due")]
        public long DateDue { get; set; }

        [JsonProperty("date_creation")]
        public long DateCreation { get; set; }

        [JsonProperty("date_modification")]
        public long DateModification { get; set; }

        [JsonProperty("project_id")]
        public int ProjectId { get; set; }

        [JsonProperty("column_id")]
        public int ColumnId { get; set; }

        /// <summary>Category id; 0 means no category.</summary>
        [JsonProperty("category_id")]
        public int CategoryId { get; set; }

        [JsonProperty("color_id")]
        public string ColorId { get; set; }

        /// <summary>1 = active (open), 0 = inactive (closed).</summary>
        [JsonProperty("is_active")]
        public int IsActive { get; set; }

        [JsonProperty("nb_comments")]
        public int NbComments { get; set; }

        [JsonProperty("nb_subtasks")]
        public int NbSubtasks { get; set; }

        [JsonProperty("reference")]
        public string Reference { get; set; }
    }
}
